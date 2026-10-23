using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    // Parts Master (OLKPAM) & Supplier Master (OLKSUP) CKB
    public class MasterDataRepo : BaseRepo
    {
        private MasterDataRepo() { }

        #region Singleton
        private static MasterDataRepo instance = null;
        public static MasterDataRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MasterDataRepo();
                }
                return instance;
            }
        }
        #endregion

        // Update dari linked server CKB bisa lama, jangan pakai timeout default 30 detik
        private const int SYNC_TIMEOUT_SECONDS = 600;

        #region Parts Master
        public IList<PartsMaster> SearchParts(string plantCode, DBContext db)
        {
            string query = @"
                SELECT SUPSCD, SUPPCD, SUPSHD, PLCPCD, RACRAC, PAMPNO, PAMPCK, PAMPNM, PAMBSC, PAMUNO
                FROM [dbo].[OLKPAM]
                WHERE PLCPCD = @PLCPCD
                ORDER BY SUPSCD, SUPPCD, SUPSHD, PAMPNO";

            return db.PartsMaster.FromSqlRaw<PartsMaster>(query, new SqlParameter("@PLCPCD", CheckNullValue(plantCode))).ToList();
        }

        public DBResult DeletePart(PartsMaster data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Parts Master (CKB)", "Delete", "MasterDataRepo.DeletePart", loginUser, () =>
            {
                int rows = db.Database.ExecuteSqlRaw(@"
                    DELETE FROM [dbo].[OLKPAM]
                    WHERE SUPSCD = @SUPSCD AND SUPPCD = @SUPPCD AND SUPSHD = @SUPSHD AND PLCPCD = @PLCPCD
                      AND RACRAC = @RACRAC AND PAMPNO = @PAMPNO AND PAMPCK = @PAMPCK",
                    new SqlParameter("@SUPSCD", CheckNullValue(data.SUPSCD)),
                    new SqlParameter("@SUPPCD", CheckNullValue(data.SUPPCD)),
                    new SqlParameter("@SUPSHD", CheckNullValue(data.SUPSHD)),
                    new SqlParameter("@PLCPCD", CheckNullValue(data.PLCPCD)),
                    new SqlParameter("@RACRAC", CheckNullValue(data.RACRAC)),
                    new SqlParameter("@PAMPNO", CheckNullValue(data.PAMPNO)),
                    new SqlParameter("@PAMPCK", CheckNullValue(data.PAMPCK)));

                if (rows == 0) return Fail("Data not found");

                return Ok("Successfully deleted part " + data.PAMPNO + " (supplier " + data.SUPSCD + "-" + data.SUPPCD + "-" + data.SUPSHD
                    + ", plant " + data.PLCPCD + ", receiving " + data.RACRAC + ", pcs/kbn " + data.PAMPCK + ")");
            });
        }

        // Tarik part baru dari database CKB plant - hanya insert yang belum ada (sama seperti desktop)
        public DBResult SyncParts(CkbSourceOptions plant, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Parts Master (CKB)", "Update", "MasterDataRepo.SyncParts", loginUser, () =>
            {
                if (string.IsNullOrEmpty(plant.PartSource))
                {
                    return Fail("Plant " + plant.PlantCode + " (" + plant.PlantName + ") has no CKB source to update from");
                }

                string query = @"
                    INSERT INTO [dbo].[OLKPAM] (SUPSCD, SUPPCD, SUPSHD, PLCPCD, RACRAC, PAMPNO, PAMPCK, PAMPNM, PAMBSC, PAMUNO)
                    SELECT S.SUPSCD, S.SUPPCD, S.SUPSHD, S.PLCPCD, S.RACRAC, S.PAMPNO, S.PAMPCK, S.PAMPNM, S.PAMBSC, S.PAMUNO
                    FROM " + CheckSource(plant.PartSource) + @".OLKPAM S
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [dbo].[OLKPAM] T
                        WHERE T.SUPSCD = S.SUPSCD AND T.SUPPCD = S.SUPPCD AND T.SUPSHD = S.SUPSHD AND T.PLCPCD = S.PLCPCD
                          AND T.RACRAC = S.RACRAC AND T.PAMPNO = S.PAMPNO AND T.PAMPCK = S.PAMPCK)";

                db.Database.SetCommandTimeout(SYNC_TIMEOUT_SECONDS);
                int rows = db.Database.ExecuteSqlRaw(query);

                return Ok("Update finished: " + rows + " new part(s) added");
            });
        }
        #endregion

        #region Supplier Master
        public IList<SupplierMaster> SearchSuppliers(DBContext db)
        {
            string query = @"
                SELECT SUPSCD, SUPPCD, SUPSHD, SUPNAM
                FROM [dbo].[OLKSUP]
                ORDER BY SUPSCD, SUPPCD, SUPSHD";

            return db.SupplierMaster.FromSqlRaw<SupplierMaster>(query).ToList();
        }

        public DBResult DeleteSupplier(SupplierMaster data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Supplier Master (CKB)", "Delete", "MasterDataRepo.DeleteSupplier", loginUser, () =>
            {
                int rows = db.Database.ExecuteSqlRaw(@"
                    DELETE FROM [dbo].[OLKSUP]
                    WHERE SUPSCD = @SUPSCD AND SUPPCD = @SUPPCD AND SUPSHD = @SUPSHD",
                    new SqlParameter("@SUPSCD", CheckNullValue(data.SUPSCD)),
                    new SqlParameter("@SUPPCD", CheckNullValue(data.SUPPCD)),
                    new SqlParameter("@SUPSHD", CheckNullValue(data.SUPSHD)));

                if (rows == 0) return Fail("Data not found");

                return Ok("Successfully deleted supplier " + data.SUPSCD + "-" + data.SUPPCD + "-" + data.SUPSHD);
            });
        }

        // Tarik supplier baru dari semua database CKB (desktop menjalankan semua sumber sekaligus).
        // Sumber yang gagal (mis. linked server tidak tersedia) dicatat lalu dilewati, sumber lain tetap jalan.
        public DBResult SyncSuppliers(IEnumerable<CkbSourceOptions> plants, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Supplier Master (CKB)", "Update", "MasterDataRepo.SyncSuppliers", loginUser, () =>
            {
                int total = 0;
                List<string> errors = new List<string>();

                var sources = plants.Where(x => !string.IsNullOrEmpty(x.SupplierSource))
                    .GroupBy(x => x.SupplierSource)
                    .Select(g => new { Source = g.Key, Plant = g.First().PlantName });

                db.Database.SetCommandTimeout(SYNC_TIMEOUT_SECONDS);
                foreach (var src in sources)
                {
                    try
                    {
                        string query = @"
                            INSERT INTO [dbo].[OLKSUP] (SUPSCD, SUPPCD, SUPSHD, SUPNAM)
                            SELECT S.SUPSCD, S.SUPPCD, S.SUPSHD, S.SUPNAM
                            FROM " + CheckSource(src.Source) + @".OLKSUP S
                            WHERE NOT EXISTS (
                                SELECT 1 FROM [dbo].[OLKSUP] T
                                WHERE T.SUPSCD = S.SUPSCD AND T.SUPPCD = S.SUPPCD AND T.SUPSHD = S.SUPSHD)";

                        total += db.Database.ExecuteSqlRaw(query);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(src.Plant + ": " + ex.Message);
                    }
                }

                if (errors.Count > 0)
                {
                    return Fail(total + " new supplier(s) added, but some sources failed - " + string.Join("; ", errors));
                }

                return Ok("Update finished: " + total + " new supplier(s) added");
            });
        }
        #endregion
    }
}
