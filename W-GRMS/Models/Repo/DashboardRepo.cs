using GRMS.Common.Repo;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GRMS.Models.Repo
{
    // Ringkasan Dashboard (CKB). Semua angka dihitung terhadap tanggal acuan (default hari ini)
    // dan plant terpilih; "semua plant" = semua plant CKB di appsettings (GRN THPS tidak ikut).
    // Filter hanya memakai kolom ber-index: TB_T_RECEIVE_H.TRANSDATE/INPUTDATE dan OLKKBNORD.SOCPRD.
    public class DashboardRepo : BaseRepo
    {
        private DashboardRepo() { }

        #region Singleton
        private static DashboardRepo instance = null;
        public static DashboardRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DashboardRepo();
                }
                return instance;
            }
        }
        #endregion

        private const int OVERDUE_DAYS = 30;
        private const int TREND_DAYS = 14;

        // "@P0, @P1, ..." untuk daftar plant
        private static string PlantList(IList<string> plants, List<SqlParameter> param)
        {
            List<string> names = new List<string>();
            foreach (string code in plants)
            {
                string name = "@P" + names.Count;
                names.Add(name);
                param.Add(VarChar(name, code));
            }
            return string.Join(", ", names);
        }

        private static List<SqlParameter> BaseParams(DateTime asOf)
        {
            DateTime day = asOf.Date;
            DateTime monthStart = new DateTime(day.Year, day.Month, 1);
            return new List<SqlParameter>
            {
                new SqlParameter("@DAY", SqlDbType.Date) { Value = day },
                new SqlParameter("@MONTH_START", SqlDbType.Date) { Value = monthStart },
                new SqlParameter("@TREND_START", SqlDbType.Date) { Value = day.AddDays(-(TREND_DAYS - 1)) },
                VarChar("@DAY_YMD", day.ToString("yyyyMMdd")),
                VarChar("@MONTH_START_YMD", monthStart.ToString("yyyyMMdd")),
                VarChar("@OVERDUE_START_YMD", day.AddDays(-OVERDUE_DAYS).ToString("yyyyMMdd")),
                VarChar("@YESTERDAY_YMD", day.AddDays(-1).ToString("yyyyMMdd"))
            };
        }

        public DataTable Summary(IList<string> plants, DateTime asOf, DBContext db)
        {
            List<SqlParameter> param = BaseParams(asOf);
            string inList = PlantList(plants, param);

            return QueryTable(db, @"
                SELECT
                    (SELECT COUNT(*) FROM [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK)
                     WHERE H.TRANSDATE = @DAY AND H.SYSTEM IN (" + inList + @")) AS GRN_TODAY,
                    (SELECT ISNULL(SUM(D.QTY), 0) FROM [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK)
                     INNER JOIN [dbo].[TB_T_RECEIVE_D] D WITH (NOLOCK) ON D.NOSEQ = H.NOSEQ
                     WHERE H.TRANSDATE = @DAY AND H.SYSTEM IN (" + inList + @")) AS QTY_TODAY,
                    (SELECT COUNT(*) FROM [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK)
                     WHERE H.TRANSDATE >= @MONTH_START AND H.TRANSDATE <= @DAY AND H.SYSTEM IN (" + inList + @")) AS GRN_MONTH,
                    (SELECT COUNT(DISTINCT A.SOCDNO) FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
                     WHERE A.SOCPRD = @DAY_YMD AND ISNULL(A.RCVSTS, 0) = 0 AND A.PLCPCD IN (" + inList + @")) AS DUE_TODAY,
                    (SELECT COUNT(DISTINCT A.SOCDNO) FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
                     WHERE A.SOCPRD >= @OVERDUE_START_YMD AND A.SOCPRD <= @YESTERDAY_YMD
                       AND ISNULL(A.RCVSTS, 0) = 0 AND A.PLCPCD IN (" + inList + @")) AS OVERDUE,
                    (SELECT COUNT(DISTINCT A.SOCDNO) FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
                     WHERE A.SOCPRD >= @MONTH_START_YMD AND A.SOCPRD <= @DAY_YMD
                       AND A.SOCDNO LIKE '%#1' AND A.PLCPCD IN (" + inList + @")) AS DELAYED_MONTH,
                    (SELECT MAX(H.TRANSDATE) FROM [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK)
                     WHERE H.TRANSDATE <= @DAY AND H.SYSTEM IN (" + inList + @")) AS LAST_RECEIVE",
                param.ToArray());
        }

        // Jumlah GRN per hari, 14 hari sampai tanggal acuan (hari tanpa GRN tetap muncul dengan 0)
        public DataTable DailyTrend(IList<string> plants, DateTime asOf, DBContext db)
        {
            List<SqlParameter> param = BaseParams(asOf);
            string inList = PlantList(plants, param);

            return QueryTable(db, @"
                WITH days AS (
                    SELECT @TREND_START AS DAY
                    UNION ALL
                    SELECT DATEADD(DAY, 1, DAY) FROM days WHERE DAY < @DAY
                ),
                grn AS (
                    SELECT H.TRANSDATE, COUNT(*) AS GRN_COUNT
                    FROM [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK)
                    WHERE H.TRANSDATE >= @TREND_START AND H.TRANSDATE <= @DAY AND H.SYSTEM IN (" + inList + @")
                    GROUP BY H.TRANSDATE
                )
                SELECT D.DAY, ISNULL(G.GRN_COUNT, 0) AS GRN_COUNT
                FROM days D
                LEFT JOIN grn G ON G.TRANSDATE = D.DAY
                ORDER BY D.DAY
                OPTION (MAXRECURSION 100)",
                param.ToArray());
        }

        // ---------- Detail (klik kartu / batang grafik) ----------

        // Daftar GRN dengan TRANSDATE di antara from - to (inklusif) + total qty per GRN
        public DataTable GrnList(IList<string> plants, DateTime from, DateTime to, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter("@FROM", SqlDbType.Date) { Value = from.Date },
                new SqlParameter("@TO", SqlDbType.Date) { Value = to.Date }
            };
            string inList = PlantList(plants, param);

            return QueryTable(db, @"
                SELECT H.NOSEQ, H.DELIVERYNOTE, H.SURATJALAN, H.TRANSDATE, H.SYSTEM AS PLCPCD,
                       H.SUPPLIER_CODE AS SUPSCD, H.SUPPLIER_NAME AS SUPNAM,
                       (SELECT ISNULL(SUM(D.QTY), 0) FROM [dbo].[TB_T_RECEIVE_D] D WITH (NOLOCK) WHERE D.NOSEQ = H.NOSEQ) AS QTY,
                       H.INPUTUSER, H.INPUTDATE
                FROM [dbo].[TB_T_RECEIVE_H] H WITH (NOLOCK)
                WHERE H.TRANSDATE >= @FROM AND H.TRANSDATE <= @TO AND H.SYSTEM IN (" + inList + @")
                ORDER BY H.TRANSDATE DESC, H.NOSEQ DESC",
                param.ToArray());
        }

        // Daftar DN (dikelompokkan per DN) dari OLKKBNORD. kind: due (jatuh tempo tanggal acuan, belum diterima),
        // overdue (30 hari terakhir s.d. kemarin, belum diterima), delayed (DN sisa #1 bulan ini),
        // supplier (belum diterima 30 hari terakhir s.d. tanggal acuan untuk satu supplier, sama dengan grafik)
        public DataTable DnList(string kind, IList<string> plants, DateTime asOf, string supplierCode, DBContext db)
        {
            List<SqlParameter> param = BaseParams(asOf);
            string inList = PlantList(plants, param);

            string where;
            switch (kind)
            {
                case "due":
                    where = "A.SOCPRD = @DAY_YMD AND ISNULL(A.RCVSTS, 0) = 0";
                    break;
                case "overdue":
                    where = "A.SOCPRD >= @OVERDUE_START_YMD AND A.SOCPRD <= @YESTERDAY_YMD AND ISNULL(A.RCVSTS, 0) = 0";
                    break;
                case "delayed":
                    where = "A.SOCPRD >= @MONTH_START_YMD AND A.SOCPRD <= @DAY_YMD AND A.SOCDNO LIKE '%#1'";
                    break;
                case "supplier":
                    where = "A.SOCPRD >= @OVERDUE_START_YMD AND A.SOCPRD <= @DAY_YMD AND ISNULL(A.RCVSTS, 0) = 0 AND A.SUPSCD = @SUPSCD";
                    param.Add(VarChar("@SUPSCD", CheckNullValue(supplierCode)));
                    break;
                default:
                    throw new ArgumentException("Unknown detail type " + kind);
            }

            return QueryTable(db, @"
                SELECT A.SOCDNO, MAX(A.SOCDNOORI) AS SOCDNOORI, MAX(A.PLCPCD) AS PLCPCD, A.SUPSCD, MAX(C.SUPNAM) AS SUPNAM,
                       MIN(A.SOCPRD) AS SOCPRD, MIN(A.SOCPRT) AS SOCPRT,
                       COUNT(*) AS ITEMS, SUM(A.KBNORDTPC) AS ORDER_QTY, SUM(A.RMNQTY) AS REMAINING_QTY,
                       MAX(CASE WHEN ISNULL(A.RCVSTS, 0) = 1 THEN 1 ELSE 0 END) AS RECEIVED
                FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
                LEFT JOIN [dbo].[OLKSUP] C WITH (NOLOCK) ON C.SUPSCD = A.SUPSCD AND C.SUPPCD = A.SUPPCD AND C.SUPSHD = A.SUPSHD
                WHERE " + where + @" AND A.PLCPCD IN (" + inList + @")
                GROUP BY A.SOCDNO, A.SUPSCD
                ORDER BY MIN(A.SOCPRD), A.SOCDNO",
                param.ToArray());
        }

        // Supplier dengan DN belum diterima terbanyak (jatuh tempo s.d. tanggal acuan, 30 hari terakhir)
        public DataTable OutstandingBySupplier(IList<string> plants, DateTime asOf, DBContext db)
        {
            List<SqlParameter> param = BaseParams(asOf);
            string inList = PlantList(plants, param);

            return QueryTable(db, @"
                SELECT TOP 10 A.SUPSCD, MAX(C.SUPNAM) AS SUPNAM, COUNT(DISTINCT A.SOCDNO) AS DN_COUNT
                FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
                LEFT JOIN [dbo].[OLKSUP] C WITH (NOLOCK) ON C.SUPSCD = A.SUPSCD AND C.SUPPCD = A.SUPPCD AND C.SUPSHD = A.SUPSHD
                WHERE A.SOCPRD >= @OVERDUE_START_YMD AND A.SOCPRD <= @DAY_YMD
                  AND ISNULL(A.RCVSTS, 0) = 0 AND A.PLCPCD IN (" + inList + @")
                GROUP BY A.SUPSCD
                ORDER BY DN_COUNT DESC, A.SUPSCD",
                param.ToArray());
        }
    }
}
