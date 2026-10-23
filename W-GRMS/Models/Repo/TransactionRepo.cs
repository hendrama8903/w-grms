using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;

namespace GRMS.Models.Repo
{
    // Transaction CKB (konversi desktop GRMS/Transaction, tanpa THPS):
    //   Order Data (frmGetGoodsReceiveCKB), Goods Receiving (frmGoodsReceivingCKB),
    //   Delivery Note Printing (frmDeliveryNotePrinting),
    //   Data SOC (olksoc_y230) + data cetak DN / GRN (pengganti SP printDN / printGRN cabang CKB).
    //
    // Catatan trigger yang sudah ada di database GRMS (tidak diubah):
    //   INSERT_TB_T_RECEIVE_D / DELETE_TB_T_RECEIVE_D mengurangi / mengembalikan OLKKBNORD.RMNQTY,
    //   UpdateOLKKBNRD (AFTER UPDATE OLKKBNORD) membaca INSERTED sebagai satu baris. Karena itu detail
    //   receive WAJIB di-insert satu statement per baris, sama seperti desktop.
    public class TransactionRepo : BaseRepo
    {
        private TransactionRepo() { }

        #region Singleton
        private static TransactionRepo instance = null;
        public static TransactionRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new TransactionRepo();
                }
                return instance;
            }
        }
        #endregion

        private const int REMOTE_TIMEOUT_SECONDS = 600;

        // Trigger INSERT/DELETE_TB_T_RECEIVE_D meng-update OLKKBNORD lewat KBNKCD/SOCDNO yang tidak ber-index
        // (scan jutaan baris per baris detail), jadi simpan/hapus GRN bisa jauh lebih lama dari 30 detik default.
        private const int RECEIVE_TIMEOUT_SECONDS = 300;

        // Order (OLKKBNORD) + part (OLKPAM) + supplier (OLKSUP) CKB
        private const string ORDER_JOIN = @"
            FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
            INNER JOIN [dbo].[OLKPAM] B WITH (NOLOCK)
                ON A.SUPSCD = B.SUPSCD AND A.SUPPCD = B.SUPPCD AND A.SUPSHD = B.SUPSHD AND A.PLCPCD = B.PLCPCD
               AND A.RACRAC = B.RACRAC AND A.PAMPNO = B.PAMPNO AND A.PAMPCK = B.PAMPCK
            INNER JOIN [dbo].[OLKSUP] C WITH (NOLOCK)
                ON A.SUPSCD = C.SUPSCD AND A.SUPPCD = C.SUPPCD AND A.SUPSHD = C.SUPSHD";

        // Database CKB plant diakses lewat linked server (PartSource), bukan koneksi langsung:
        // server CKB masih SQL Server 2000 yang tidak didukung Microsoft.Data.SqlClient.
        private static string CkbSource(CkbSourceOptions plant)
        {
            if (string.IsNullOrEmpty(plant.PartSource))
            {
                throw new InvalidOperationException("Plant " + plant.PlantCode + " (" + plant.PlantName + ") has no CKB source configured");
            }
            return CheckSource(plant.PartSource);
        }

        // Tanggal order CKB disimpan sebagai varchar yyyyMMdd
        private static object YMD(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("yyyyMMdd") : DBNull.Value;
        }

        #region Order Data
        public DataTable SearchOrders(string plantCode, DateTime from, DateTime to, DBContext db)
        {
            string query = @"
                SELECT A.SUPSCD, A.SUPPCD, A.SUPSHD, C.SUPNAM, A.PLCPCD, A.RACRAC, A.SOCPOD, A.SOCPOT, A.SOCPOCS, A.SOCDNO,
                       A.PAMPNO, B.PAMPNM, A.PAMPCK, A.KBNKCD, A.KBNKNO, A.KBNORDOTP, A.KBNORDTKO, A.KBNORDTPC, A.RMNQTY,
                       A.SOCPRD, A.SOCPRT, A.SOCPRCS, A.SOCDNOORI, B.PAMUNO, A.RCVSTS
                " + ORDER_JOIN + @"
                WHERE A.PLCPCD = @PLCPCD AND A.SOCPRD >= @FROM AND A.SOCPRD <= @TO
                ORDER BY A.SOCPRD, A.SOCDNO, A.PAMPNO";

            return QueryTable(db, query,
                VarChar("@PLCPCD", CheckNullValue(plantCode)),
                VarChar("@FROM", YMD(from)),
                VarChar("@TO", YMD(to)));
        }

        public DBResult DeleteOrder(string socdno, string socpod, string socpot, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Order Data (CKB)", "Delete", "TransactionRepo.DeleteOrder", loginUser, () =>
            {
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_T_RECEIVE_H] WHERE DELIVERYNOTE = @DN", VarChar("@DN", CheckNullValue(socdno))))
                {
                    return Fail("Cannot delete DN " + socdno + ", data has been received");
                }

                int rows = db.Database.ExecuteSqlRaw(@"
                    DELETE FROM [dbo].[OLKKBNORD] WHERE SOCDNO = @SOCDNO AND SOCPOD = @SOCPOD AND SOCPOT = @SOCPOT",
                    VarChar("@SOCDNO", CheckNullValue(socdno)),
                    VarChar("@SOCPOD", CheckNullValue(socpod)),
                    VarChar("@SOCPOT", CheckNullValue(socpot)));

                if (rows == 0) return Fail("Data not found");
                return Ok("Successfully deleted DN " + socdno + " (" + rows + " row(s))");
            });
        }

        // Tarik order baru (GRMS = 0) dari database CKB plant ke OLKKBNORD GRMS, lalu tandai GRMS = 1 di CKB.
        // Per baris seperti desktop: baris yang gagal insert (mis. sudah ada) tidak ditandai di CKB.
        // progress (opsional) diisi selama proses supaya layar bisa menampilkan jumlah data & progres.
        public DBResult DownloadOrders(CkbSourceOptions plant, string loginUser, DBContext db, OrderDownloadProgress progress = null)
        {
            return RunWithLog(db, "Order Data (CKB)", "Download", "TransactionRepo.DownloadOrders", loginUser, () =>
            {
                string src = CkbSource(plant);
                db.Database.SetCommandTimeout(REMOTE_TIMEOUT_SECONDS);

                DataTable source = QueryTable(db, @"
                    SELECT A.SUPSCD, A.SUPPCD, A.SUPSHD, A.PLCPCD, A.RACRAC, B.SOCAOCD, B.SOCAOCT, B.SOCAOCS, A.SOCDNO,
                           A.PAMPNO, A.PAMPCK, A.KBNKCD, A.KBNKNO, A.KBNORDOTP, A.KBNORDTKO, A.KBNORDTPC,
                           B.SOCPRD, B.SOCPRT, B.SOCPRCS
                    FROM " + src + @".OLKKBNORD AS A
                    INNER JOIN " + src + @".OLKSOC AS B
                        ON A.SUPSCD = B.SUPSCD AND A.SUPPCD = B.SUPPCD AND A.SUPSHD = B.SUPSHD AND A.PLCPCD = B.PLCPCD
                       AND A.RACRAC = B.RACRAC AND A.SOCPOD = B.SOCPOD AND A.SOCPOT = B.SOCPOT AND A.SOCPOCS = B.SOCPOCS
                    WHERE A.GRMS = 0 AND A.SOCDNO <> '1Way'");

                if (progress != null)
                {
                    progress.Total = source.Rows.Count;
                    progress.Stage = "save";
                }

                int inserted = 0, failed = 0;
                string firstError = null;

                foreach (DataRow dr in source.Rows)
                {
                    try
                    {
                        db.Database.ExecuteSqlRaw(@"
                            INSERT INTO [dbo].[OLKKBNORD] (SUPSCD, SUPPCD, SUPSHD, PLCPCD, RACRAC, SOCPOD, SOCPOT, SOCPOCS, SOCDNO,
                                PAMPNO, PAMPCK, KBNKCD, KBNKNO, KBNORDOTP, KBNORDTKO, KBNORDTPC, RMNQTY, SOCPRD, SOCPRT, SOCPRCS, SOCDNOORI)
                            VALUES (@SUPSCD, @SUPPCD, @SUPSHD, @PLCPCD, @RACRAC, @SOCPOD, @SOCPOT, @SOCPOCS, @SOCDNO,
                                @PAMPNO, @PAMPCK, @KBNKCD, @KBNKNO, @KBNORDOTP, @KBNORDTKO, @KBNORDTPC, @KBNORDTPC, @SOCPRD, @SOCPRT, @SOCPRCS, '')",
                            new SqlParameter("@SUPSCD", dr["SUPSCD"]),
                            new SqlParameter("@SUPPCD", dr["SUPPCD"]),
                            new SqlParameter("@SUPSHD", dr["SUPSHD"]),
                            VarChar("@PLCPCD", dr["PLCPCD"]),
                            new SqlParameter("@RACRAC", dr["RACRAC"]),
                            VarChar("@SOCPOD", dr["SOCAOCD"]),
                            VarChar("@SOCPOT", dr["SOCAOCT"]),
                            new SqlParameter("@SOCPOCS", dr["SOCAOCS"]),
                            VarChar("@SOCDNO", dr["SOCDNO"]),
                            new SqlParameter("@PAMPNO", dr["PAMPNO"]),
                            new SqlParameter("@PAMPCK", dr["PAMPCK"]),
                            new SqlParameter("@KBNKCD", dr["KBNKCD"]),
                            new SqlParameter("@KBNKNO", dr["KBNKNO"]),
                            new SqlParameter("@KBNORDOTP", dr["KBNORDOTP"]),
                            new SqlParameter("@KBNORDTKO", dr["KBNORDTKO"]),
                            new SqlParameter("@KBNORDTPC", dr["KBNORDTPC"]),
                            new SqlParameter("@SOCPRD", dr["SOCPRD"]),
                            new SqlParameter("@SOCPRT", dr["SOCPRT"]),
                            new SqlParameter("@SOCPRCS", dr["SOCPRCS"]));
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        if (progress != null) progress.Failed = failed;
                        firstError ??= dr["SOCDNO"] + "/" + dr["PAMPNO"] + ": " + ex.Message;
                        continue;
                    }

                    // Tandai di CKB lewat linked server. Parameter string VARCHAR supaya filter dikirim ke server CKB
                    // (nvarchar membuat SQL Server menarik seluruh tabel remote dulu).
                    db.Database.ExecuteSqlRaw(@"
                        UPDATE " + src + @".OLKKBNORD SET GRMS = 1
                        WHERE SUPSCD = @SUPSCD AND SUPPCD = @SUPPCD AND SUPSHD = @SUPSHD AND PLCPCD = @PLCPCD AND RACRAC = @RACRAC
                          AND SOCDNO = @SOCDNO AND PAMPNO = @PAMPNO AND PAMPCK = @PAMPCK AND KBNKCD = @KBNKCD",
                        VarChar("@SUPSCD", dr["SUPSCD"]),
                        VarChar("@SUPPCD", dr["SUPPCD"]),
                        VarChar("@SUPSHD", dr["SUPSHD"]),
                        VarChar("@PLCPCD", dr["PLCPCD"]),
                        VarChar("@RACRAC", dr["RACRAC"]),
                        VarChar("@SOCDNO", dr["SOCDNO"]),
                        VarChar("@PAMPNO", dr["PAMPNO"]),
                        new SqlParameter("@PAMPCK", dr["PAMPCK"]),
                        VarChar("@KBNKCD", dr["KBNKCD"]));
                    inserted++;
                    if (progress != null) progress.Inserted = inserted;
                }

                string message = "Download finished: " + inserted + " order row(s) added";
                if (failed > 0)
                {
                    return Fail(message + ", " + failed + " row(s) failed. First error - " + firstError);
                }
                return Ok(message);
            });
        }
        #endregion

        #region Goods Receiving
        // Detail DN yang belum diterima, untuk layar Goods Receiving (input baru)
        public DBResult GetDnForReceive(string dn, DBContext db)
        {
            // DN yang sama dengan lebih dari satu PO berarti data order ganda - desktop menolak
            bool duplicate = Exists(db, @"
                SELECT 1 FROM (SELECT DISTINCT SOCDNO, SOCPOD, SOCPOT FROM [dbo].[OLKKBNORD] WHERE SOCDNO = @DN) B
                GROUP BY SOCDNO HAVING COUNT(1) > 1", VarChar("@DN", CheckNullValue(dn)));
            if (duplicate)
            {
                return Fail("System has found duplicate data for this DN");
            }

            DataTable dt = QueryTable(db, @"
                SELECT A.PLCPCD, A.SOCDNO, A.PAMPNO AS PARTNO, dbo.ChangeToDate(A.SOCPRD) AS SOCPRD, B.PAMPNM,
                       A.KBNKCD AS KEYCODE, A.KBNKNO AS KANBANNO, A.KBNORDTPC, A.KBNORDTPC AS QTY, A.SUPSCD, C.SUPNAM, B.PAMUNO
                " + ORDER_JOIN + @"
                WHERE A.SOCDNO = @DN AND (A.RCVSTS <> 1 OR A.RCVSTS IS NULL)
                ORDER BY B.PAMUNO",
                VarChar("@DN", CheckNullValue(dn)));

            if (dt.Rows.Count == 0)
            {
                return Fail("Order data not found, or has been received");
            }

            return new DBResult(true, (object)ToRows(dt));
        }

        // Detail GRN yang sudah tersimpan
        public DataTable GetReceive(string noseq, DBContext db)
        {
            return QueryTable(db, @"
                SELECT A.NOSEQ, A.DELIVERYNOTE, A.TRANSDATE, A.SURATJALAN, C.SUPSCD, D.SUPNAM, C.PLCPCD, B.PARTNO, B.KANBANNO,
                       B.KEYCODE, E.PAMPNM, B.QTY, C.KBNORDTPC, E.PAMUNO, A.INPUTUSER, A.INPUTDATE
                FROM [dbo].[OLKSUP] D WITH (NOLOCK)
                INNER JOIN [dbo].[OLKKBNORD] C WITH (NOLOCK) ON D.SUPSCD = C.SUPSCD AND D.SUPPCD = C.SUPPCD AND D.SUPSHD = C.SUPSHD
                INNER JOIN [dbo].[TB_T_RECEIVE_H] A WITH (NOLOCK) ON C.SOCDNO = A.DELIVERYNOTE
                INNER JOIN [dbo].[TB_T_RECEIVE_D] B WITH (NOLOCK) ON A.NOSEQ = B.NOSEQ AND C.PAMPNO = B.PARTNO AND C.KBNKCD = B.KEYCODE
                INNER JOIN [dbo].[OLKPAM] E WITH (NOLOCK)
                    ON C.SUPSCD = E.SUPSCD AND C.SUPPCD = E.SUPPCD AND C.SUPSHD = E.SUPSHD AND C.PLCPCD = E.PLCPCD
                   AND C.RACRAC = E.RACRAC AND C.PAMPNO = E.PAMPNO AND C.PAMPCK = E.PAMPCK
                WHERE A.NOSEQ = @NOSEQ
                GROUP BY A.NOSEQ, A.DELIVERYNOTE, A.TRANSDATE, A.SURATJALAN, C.SUPSCD, D.SUPNAM, C.PLCPCD, B.PARTNO, B.KANBANNO,
                         B.KEYCODE, E.PAMPNM, B.QTY, C.KBNORDTPC, E.PAMUNO, A.INPUTUSER, A.INPUTDATE
                ORDER BY E.PAMUNO",
                VarChar("@NOSEQ", CheckNullValue(noseq)));
        }

        // Daftar GRN CKB per periode receive (lookup desktop frmLookupGRNCKB).
        // Desktop memuat 2 tahun sekaligus dengan join ke OLKKBNORD (jutaan baris, tanpa index SOCDNO);
        // di web dibatasi periode & plant CKB (TB_T_RECEIVE_H.SYSTEM = kode plant), supplier diambil dari header.
        public DataTable LookupReceive(DateTime from, DateTime to, IEnumerable<string> plantCodes, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter("@FROM", SqlDbType.Date) { Value = from.Date },
                new SqlParameter("@TO", SqlDbType.Date) { Value = to.Date }
            };

            List<string> plantParams = new List<string>();
            foreach (string code in plantCodes)
            {
                string name = "@P" + plantParams.Count;
                plantParams.Add(name);
                param.Add(VarChar(name, code));
            }
            if (plantParams.Count == 0) return new DataTable();

            return QueryTable(db, @"
                SELECT H.NOSEQ, H.DELIVERYNOTE, H.TRANSDATE, H.SURATJALAN, H.SYSTEM AS PLCPCD,
                       H.SUPPLIER_CODE AS SUPSCD, H.SUPPLIER_NAME AS SUPNAM, H.INPUTDATE, H.INPUTUSER
                FROM [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK)
                WHERE H.TRANSDATE >= @FROM AND H.TRANSDATE <= @TO
                  AND H.SYSTEM IN (" + string.Join(", ", plantParams) + @")
                ORDER BY H.INPUTDATE DESC", param.ToArray());
        }

        // DN sudah "closed" kalau DN sisa berikutnya (DN#n+1) sudah dibuat - GRN-nya tidak boleh diubah/dihapus
        public bool IsDnClosed(string dn, DBContext db)
        {
            return RemainingDnOf(dn, db) != null;
        }

        // Nomor DN sisa (DN#n+1) yang dibuat dari DN ini, atau null kalau belum ada
        public string RemainingDnOf(string dn, DBContext db)
        {
            if (string.IsNullOrEmpty(dn) || dn.Length < 10) return null;

            int next = 1;
            Match m = Regex.Match(dn, @"#(\d+)$");
            if (m.Success)
            {
                next = int.Parse(m.Groups[1].Value) + 1;
            }
            string nextDn = dn.Substring(0, 10) + "#" + next;

            bool exists = Exists(db, "SELECT 1 FROM [dbo].[OLKKBNORD] WITH (NOLOCK) WHERE SOCDNO = @DN", VarChar("@DN", nextDn));
            return exists ? nextDn : null;
        }

        public DBResult SaveReceive(GoodsReceiveSave data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Goods Receiving (CKB)", "Save", "TransactionRepo.SaveReceive", loginUser, () =>
            {
                if (string.IsNullOrEmpty(data.DELIVERYNOTE)) return Fail("Delivery Note should not be empty");
                if (data.TRANSDATE == null) return Fail("Receive date should not be empty");
                if (data.ITEMS == null || data.ITEMS.Count == 0) return Fail("Please select detail item");
                if (data.ITEMS.Any(x => x.QTY <= 0)) return Fail("Cannot receive 0 quantity");

                db.Database.SetCommandTimeout(RECEIVE_TIMEOUT_SECONDS);

                using var trx = db.Database.BeginTransaction();

                // Validasi ulang ke database (bukan percaya data dari browser): kanban harus milik DN ini,
                // belum diterima, dan qty tidak melebihi qty order
                DataTable open = QueryTable(db, @"
                    SELECT A.KBNKCD, A.PAMPNO, A.KBNKNO, A.KBNORDTPC, A.PLCPCD, A.SUPSCD, C.SUPNAM
                    " + ORDER_JOIN + @"
                    WHERE A.SOCDNO = @DN AND (A.RCVSTS <> 1 OR A.RCVSTS IS NULL)",
                    VarChar("@DN", data.DELIVERYNOTE));

                if (open.Rows.Count == 0) return Fail("Order data not found, or has been received");

                foreach (GoodsReceiveItem item in data.ITEMS)
                {
                    DataRow row = open.AsEnumerable().FirstOrDefault(r =>
                        r["KBNKCD"].ToString() == item.KEYCODE && r["PAMPNO"].ToString() == item.PARTNO);
                    if (row == null) return Fail("Kanban " + item.KEYCODE + " / part " + item.PARTNO + " is not open for DN " + data.DELIVERYNOTE);
                    if (item.QTY > Convert.ToInt32(row["KBNORDTPC"])) return Fail("Receive qty cannot exceed the remaining qty (part " + item.PARTNO + ")");
                    item.KANBANNO = row["KBNKNO"].ToString();
                }

                DataRow header = open.Rows[0];
                string noseq = NextReceiveNumber(db);

                db.Database.ExecuteSqlRaw(@"
                    INSERT INTO [dbo].[TB_T_RECEIVE_H] (NOSEQ, SYSTEM, DELIVERYNOTE, SURATJALAN, TRANSDATE, INPUTUSER, SUPPLIER_CODE, SUPPLIER_NAME)
                    VALUES (@NOSEQ, @SYSTEM, @DN, @SURATJALAN, @TRANSDATE, @INPUTUSER, @SUPPLIER_CODE, @SUPPLIER_NAME)",
                    VarChar("@NOSEQ", noseq),
                    new SqlParameter("@SYSTEM", header["PLCPCD"]),
                    VarChar("@DN", data.DELIVERYNOTE),
                    new SqlParameter("@SURATJALAN", CheckNullValue(data.SURATJALAN)),
                    new SqlParameter("@TRANSDATE", data.TRANSDATE.Value.Date),
                    new SqlParameter("@INPUTUSER", loginUser),
                    new SqlParameter("@SUPPLIER_CODE", header["SUPSCD"]),
                    new SqlParameter("@SUPPLIER_NAME", header["SUPNAM"]));

                // Satu statement per baris - lihat catatan trigger di atas class
                foreach (GoodsReceiveItem item in data.ITEMS)
                {
                    db.Database.ExecuteSqlRaw(@"
                        INSERT INTO [dbo].[TB_T_RECEIVE_D] (NOSEQ, PARTNO, KANBANNO, KEYCODE, QTY)
                        VALUES (@NOSEQ, @PARTNO, @KANBANNO, @KEYCODE, @QTY)",
                        VarChar("@NOSEQ", noseq),
                        new SqlParameter("@PARTNO", item.PARTNO),
                        new SqlParameter("@KANBANNO", CheckNullValue(item.KANBANNO)),
                        new SqlParameter("@KEYCODE", item.KEYCODE),
                        new SqlParameter("@QTY", item.QTY));
                }

                string newDn = CreateRemainingDn(data.DELIVERYNOTE, db);

                trx.Commit();

                string message = "Data has been saved: " + noseq + (newDn != null ? ". New Delivery Note " + newDn + " has been created" : "");
                return new DBResult(true, message) { data = new GoodsReceiveResult { NOSEQ = noseq, NEW_DN = newDn } };
            });
        }

        // GRN + yyMM + 6 digit urut per bulan. UPDLOCK supaya dua user yang simpan bersamaan tidak dapat nomor sama.
        private string NextReceiveNumber(DBContext db)
        {
            string period = DateTime.Now.ToString("yyMM");
            int last = ScalarInt(db, @"
                SELECT ISNULL(MAX(CONVERT(INT, SUBSTRING(NOSEQ, 8, 6))), 0) AS Value
                FROM [dbo].[TB_T_RECEIVE_H] WITH (UPDLOCK, HOLDLOCK)
                WHERE SUBSTRING(NOSEQ, 4, 4) = @PERIOD",
                VarChar("@PERIOD", period));

            return "GRN" + period + (last + 1).ToString("000000");
        }

        // Sisa qty yang belum diterima dipindah ke DN baru "<10 digit DN>#n" (desktop CreateNewDN).
        // Mengembalikan nomor DN baru, atau null kalau semua sudah diterima.
        private string CreateRemainingDn(string dn, DBContext db)
        {
            if (!Exists(db, "SELECT 1 FROM [dbo].[OLKKBNORD] WHERE SOCDNO = @DN AND RMNQTY > 0", VarChar("@DN", dn)))
            {
                return null;
            }

            string baseDn = dn.Length >= 10 ? dn.Substring(0, 10) : dn;
            int next = ScalarInt(db, @"
                SELECT ISNULL(MAX(CONVERT(INT, SUBSTRING(SOCDNO, 12, 2))), 0) + 1 AS Value
                FROM [dbo].[OLKKBNORD]
                WHERE SOCDNOORI = @DN AND RMNQTY > 0",
                VarChar("@DN", baseDn));
            string newDn = baseDn + "#" + next;

            db.Database.ExecuteSqlRaw(@"
                UPDATE [dbo].[OLKKBNORD] SET RCVSTS = 1 WHERE SOCDNO = @DN;

                INSERT INTO [dbo].[OLKKBNORD] (SUPSCD, SUPPCD, SUPSHD, PLCPCD, RACRAC, SOCPOD, SOCPOT, SOCPOCS, SOCDNO,
                    PAMPNO, PAMPCK, KBNKCD, KBNKNO, KBNORDOTP, KBNORDTKO, KBNORDTPC, RMNQTY, SOCPRD, SOCPRT, SOCPRCS, SOCDNOORI, QTYRCV, RCVSTS)
                SELECT SUPSCD, SUPPCD, SUPSHD, PLCPCD, RACRAC, SOCPOD, SOCPOT, SOCPOCS, @NEW_DN,
                    PAMPNO, PAMPCK, KBNKCD, KBNKNO, KBNORDOTP, KBNORDTKO, RMNQTY, RMNQTY, SOCPRD, SOCPRT, SOCPRCS, SUBSTRING(SOCDNO, 1, 10), 0, 0
                FROM [dbo].[OLKKBNORD]
                WHERE SOCDNO = @DN AND RMNQTY > 0;",
                VarChar("@DN", dn),
                VarChar("@NEW_DN", newDn));

            return newDn;
        }

        public DBResult DeleteReceive(string noseq, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Goods Receiving (CKB)", "Delete", "TransactionRepo.DeleteReceive", loginUser, () =>
            {
                DataTable dt = QueryTable(db, "SELECT DELIVERYNOTE FROM [dbo].[TB_T_RECEIVE_H] WHERE NOSEQ = @NOSEQ",
                    VarChar("@NOSEQ", CheckNullValue(noseq)));
                if (dt.Rows.Count == 0) return Fail("Data not found");

                db.Database.SetCommandTimeout(RECEIVE_TIMEOUT_SECONDS);

                string dn = dt.Rows[0]["DELIVERYNOTE"].ToString();
                if (IsDnClosed(dn, db))
                {
                    return Fail("Cannot delete " + noseq + ", the remaining Delivery Note has already been created");
                }

                using var trx = db.Database.BeginTransaction();
                db.Database.ExecuteSqlRaw(@"
                    DELETE FROM [dbo].[TB_T_RECEIVE_D] WHERE NOSEQ = @NOSEQ;
                    DELETE FROM [dbo].[TB_T_RECEIVE_H] WHERE NOSEQ = @NOSEQ;",
                    VarChar("@NOSEQ", noseq));
                trx.Commit();

                return Ok("Data " + noseq + " has been deleted");
            });
        }
        #endregion

        #region Delivery Note Printing
        // DN sisa (mengandung '#') yang bisa dicetak ulang (lookup desktop frmLookupOrderDataCKB).
        // Dibatasi periode receive plan (SOCPRD ber-index) - desktop men-scan seluruh OLKKBNORD.
        public DataTable LookupSplitDn(DateTime from, DateTime to, DBContext db)
        {
            return QueryTable(db, @"
                SELECT A.SOCDNO, B.SUPSCD, B.SUPNAM, dbo.ChangeToDate(A.SOCPRD) AS SOCPRD
                FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
                INNER JOIN [dbo].[OLKSUP] B WITH (NOLOCK) ON A.SUPSCD = B.SUPSCD AND A.SUPPCD = B.SUPPCD AND A.SUPSHD = B.SUPSHD
                WHERE A.SOCPRD >= @FROM AND A.SOCPRD <= @TO AND A.SOCDNO LIKE '%#%'
                GROUP BY A.SOCDNO, B.SUPSCD, B.SUPNAM, dbo.ChangeToDate(A.SOCPRD)
                ORDER BY B.SUPSCD, A.SOCDNO",
                VarChar("@FROM", YMD(from)),
                VarChar("@TO", YMD(to)));
        }

        public DataTable GetDnDetail(string dn, DBContext db)
        {
            return QueryTable(db, @"
                SELECT A.PLCPCD, A.SOCDNO, A.PAMPNO, dbo.ChangeToDate(A.SOCPRD) AS SOCPRD, B.PAMPNM, A.KBNKCD, A.KBNKNO,
                       A.KBNORDTPC, A.RMNQTY, A.RCVSTS, A.SUPSCD, C.SUPNAM, A.SOCDNOORI, B.PAMUNO
                " + ORDER_JOIN + @"
                WHERE A.SOCDNO = @DN
                ORDER BY B.PAMUNO",
                VarChar("@DN", CheckNullValue(dn)));
        }
        #endregion

        #region Print data
        // Data rptDN (cabang CKB SP printDN). OLKPLC di-LEFT JOIN: SP desktop memakai INNER JOIN sehingga
        // DN plant yang belum terdaftar di OLKPLC (mis. plant 7) tidak bisa dicetak sama sekali.
        public DataTable PrintDnData(string dn, DBContext db)
        {
            return QueryTable(db, @"
                SELECT A.SOCDNO, A.SUPSCD + '-' + A.SUPPCD AS SUPSCD, C.SUPNAM, A.PLCPCD + ISNULL('/' + O.PLCPNM, '') AS PLCPCD,
                       dbo.ChangeToDate(A.SOCPRD) AS SOCPRD, A.SOCDNOORI,
                       SUBSTRING(A.PAMPNO, 1, 5) + '-' + SUBSTRING(A.PAMPNO, 6, 5) + '-' + SUBSTRING(A.PAMPNO, 11, 2) AS PAMPNO,
                       SUM(A.KBNORDTPC) AS KBNORDTPC, A.PAMPCK, B.PAMPNM, B.PAMBSC, B.PAMUNO
                " + ORDER_JOIN + @"
                LEFT JOIN [dbo].[OLKPLC] O WITH (NOLOCK) ON O.PLCPCD = B.PLCPCD
                WHERE A.SOCDNO = @DN
                GROUP BY A.SOCDNO, A.SUPSCD + '-' + A.SUPPCD, C.SUPNAM, A.PLCPCD + ISNULL('/' + O.PLCPNM, ''), dbo.ChangeToDate(A.SOCPRD),
                         A.SOCDNOORI, A.PAMPNO, A.KBNORDTPC, B.PAMPNM, A.PAMPCK, B.PAMBSC, B.PAMUNO",
                VarChar("@DN", CheckNullValue(dn)));
        }

        // Data rptGRN (cabang CKB SP printGRN)
        public DataTable PrintGrnData(string noseq, DBContext db)
        {
            return QueryTable(db, @"
                SELECT H.NOSEQ, H.DELIVERYNOTE, H.TRANSDATE, H.SURATJALAN, K.SUPSCD, S.SUPNAM,
                       SUBSTRING(D.PARTNO, 1, 5) + '-' + SUBSTRING(D.PARTNO, 6, 5) + '-' + SUBSTRING(D.PARTNO, 11, 2) AS PARTNO,
                       P.PAMPNM, SUM(D.QTY) AS QTY, P.PAMUNO
                FROM [dbo].[OLKSUP] S
                INNER JOIN [dbo].[OLKKBNORD] K ON S.SUPSCD = K.SUPSCD AND S.SUPPCD = K.SUPPCD AND S.SUPSHD = K.SUPSHD
                INNER JOIN [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK) ON K.SOCDNO = H.DELIVERYNOTE
                INNER JOIN [dbo].[TB_T_RECEIVE_D] D WITH (NOLOCK) ON D.NOSEQ = H.NOSEQ AND K.PAMPNO = D.PARTNO AND K.KBNKCD = D.KEYCODE
                INNER JOIN [dbo].[OLKPAM] P WITH (NOLOCK)
                    ON K.SUPSCD = P.SUPSCD AND K.SUPPCD = P.SUPPCD AND K.SUPSHD = P.SUPSHD AND K.PLCPCD = P.PLCPCD
                   AND K.RACRAC = P.RACRAC AND K.PAMPNO = P.PAMPNO AND K.PAMPCK = P.PAMPCK
                WHERE H.NOSEQ = @NOSEQ
                GROUP BY H.NOSEQ, H.DELIVERYNOTE, H.TRANSDATE, H.SURATJALAN, K.SUPSCD, S.SUPNAM, D.PARTNO, P.PAMPNM, P.PAMUNO
                ORDER BY P.PAMUNO",
                VarChar("@NOSEQ", CheckNullValue(noseq)));
        }
        #endregion

        #region Data SOC
        // Data SOC (OLKSOC) dari database CKB plant (lewat linked server) untuk satu periode yyyyMM
        public DataTable GetSoc(CkbSourceOptions plant, string period, DBContext db)
        {
            db.Database.SetCommandTimeout(REMOTE_TIMEOUT_SECONDS);
            return QueryTable(db, "SELECT * FROM " + CkbSource(plant) + ".OLKSOC WHERE SOCPMO = @PERIOD",
                VarChar("@PERIOD", period));
        }
        #endregion
    }
}
