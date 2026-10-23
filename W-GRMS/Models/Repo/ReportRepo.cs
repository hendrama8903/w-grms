using GRMS.Common.Repo;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GRMS.Models.Repo
{
    // Report CKB (konversi desktop GRMS/Report, tanpa THPS):
    //   Goods Receiving History (frmGoodsReceiveHistoryCKB), Outstanding Order (frmOutstandingOrderCKB),
    //   Summary Delay Delivery (frmSummaryDelayDeliveryCKB).
    // Tanggal order CKB (SOCPRD) disimpan varchar yyyyMMdd dan dibandingkan sebagai string
    // (setara dbo.ChangeToDate di desktop, tapi index SOCPRD tetap terpakai).
    public class ReportRepo : BaseRepo
    {
        private ReportRepo() { }

        #region Singleton
        private static ReportRepo instance = null;
        public static ReportRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ReportRepo();
                }
                return instance;
            }
        }
        #endregion

        private const int REPORT_TIMEOUT_SECONDS = 300;

        private const string ORDER_JOIN = @"
            FROM [dbo].[OLKKBNORD] A WITH (NOLOCK)
            INNER JOIN [dbo].[OLKPAM] B WITH (NOLOCK)
                ON A.SUPSCD = B.SUPSCD AND A.SUPPCD = B.SUPPCD AND A.SUPSHD = B.SUPSHD AND A.PLCPCD = B.PLCPCD
               AND A.RACRAC = B.RACRAC AND A.PAMPNO = B.PAMPNO AND A.PAMPCK = B.PAMPCK
            INNER JOIN [dbo].[OLKSUP] C WITH (NOLOCK)
                ON A.SUPSCD = C.SUPSCD AND A.SUPPCD = C.SUPPCD AND A.SUPSHD = C.SUPSHD";

        public const string FILTER_ACTUAL_RECEIVE = "ACTUAL_RECEIVE";
        public const string FILTER_ACTUAL_INPUT = "ACTUAL_INPUT";
        public const string FILTER_PLAN_RECEIVE = "PLAN_RECEIVE";

        // Riwayat penerimaan per part. filterBy: tanggal receive aktual (TRANSDATE), tanggal input (INPUTDATE)
        // atau rencana receive (SOCPRD). Rentang INPUTDATE dibuat inklusif sampai akhir hari "to".
        public DataTable GoodsReceiveHistory(string plantCode, string filterBy, DateTime from, DateTime to, DBContext db)
        {
            string where;
            switch (filterBy)
            {
                case FILTER_ACTUAL_INPUT:
                    where = "A.INPUTDATE >= @FROM_DT AND A.INPUTDATE < DATEADD(DAY, 1, @TO_DT)";
                    break;
                case FILTER_PLAN_RECEIVE:
                    where = "C.SOCPRD >= @FROM_YMD AND C.SOCPRD <= @TO_YMD";
                    break;
                default:
                    where = "A.TRANSDATE >= @FROM_DT AND A.TRANSDATE <= @TO_DT";
                    break;
            }

            db.Database.SetCommandTimeout(REPORT_TIMEOUT_SECONDS);
            return QueryTable(db, @"
                SELECT SUM(B.QTY) AS QTY, A.NOSEQ, A.DELIVERYNOTE, A.SURATJALAN, B.PARTNO, D.PAMPNM,
                       dbo.ChangeToDate(C.SOCPRD) AS PLANRECEIVE, A.TRANSDATE AS ACTUALRECEIVE,
                       E.SUPSCD, E.SUPPCD, E.SUPNAM, D.PAMUNO, C.SUPSHD, C.RACRAC, A.INPUTDATE,
                       CONVERT(VARCHAR(8), A.INPUTDATE, 114) AS ACTUALTIME, A.INPUTUSER, B.FILE_ID
                FROM [dbo].[TB_T_RECEIVE_H] A WITH (NOLOCK)
                INNER JOIN [dbo].[TB_T_RECEIVE_D] B WITH (NOLOCK) ON A.NOSEQ = B.NOSEQ
                INNER JOIN [dbo].[OLKKBNORD] C WITH (NOLOCK) ON C.SOCDNO = A.DELIVERYNOTE AND C.PAMPNO = B.PARTNO AND C.KBNKCD = B.KEYCODE
                INNER JOIN [dbo].[OLKPAM] D WITH (NOLOCK)
                    ON C.SUPSCD = D.SUPSCD AND C.SUPPCD = D.SUPPCD AND C.SUPSHD = D.SUPSHD AND C.PLCPCD = D.PLCPCD
                   AND C.RACRAC = D.RACRAC AND C.PAMPNO = D.PAMPNO AND C.PAMPCK = D.PAMPCK
                INNER JOIN [dbo].[OLKSUP] E WITH (NOLOCK) ON C.SUPSCD = E.SUPSCD AND C.SUPPCD = E.SUPPCD AND C.SUPSHD = E.SUPSHD
                WHERE " + where + @" AND C.PLCPCD = @PLCPCD
                GROUP BY A.DELIVERYNOTE, A.NOSEQ, A.SURATJALAN, B.PARTNO, D.PAMPNM, A.TRANSDATE, dbo.ChangeToDate(C.SOCPRD),
                         E.SUPSCD, E.SUPPCD, E.SUPNAM, D.PAMUNO, C.SUPSHD, C.RACRAC, A.INPUTDATE, A.INPUTUSER, B.FILE_ID
                ORDER BY A.INPUTDATE, A.NOSEQ, D.PAMUNO",
                VarChar("@PLCPCD", plantCode),
                new SqlParameter("@FROM_DT", SqlDbType.DateTime) { Value = from.Date },
                new SqlParameter("@TO_DT", SqlDbType.DateTime) { Value = to.Date },
                VarChar("@FROM_YMD", from.ToString("yyyyMMdd")),
                VarChar("@TO_YMD", to.ToString("yyyyMMdd")));
        }

        // Order yang belum diterima (RCVSTS 0 / NULL) per rencana receive
        public DataTable OutstandingOrder(string plantCode, DateTime from, DateTime to, DBContext db)
        {
            db.Database.SetCommandTimeout(REPORT_TIMEOUT_SECONDS);
            return QueryTable(db, @"
                SELECT A.SOCDNO, A.SOCPRT, A.SOCPOCS, A.PAMPNO, B.PAMPNM, B.PAMUNO, SUM(A.KBNORDTPC) AS KBNORDTPC,
                       dbo.ChangeToDate(A.SOCPRD) AS SOCPRD, A.SUPSCD, A.SUPPCD, C.SUPNAM, A.SUPSHD, A.RACRAC
                " + ORDER_JOIN + @"
                WHERE A.SOCPRD >= @FROM AND A.SOCPRD <= @TO
                  AND (A.RCVSTS = 0 OR A.RCVSTS IS NULL)
                  AND A.PLCPCD = @PLCPCD
                GROUP BY A.SOCDNO, A.SOCPOCS, A.SOCPRT, A.PAMPNO, B.PAMPNM, B.PAMUNO, dbo.ChangeToDate(A.SOCPRD),
                         A.SUPSCD, A.SUPPCD, C.SUPNAM, A.SUPSHD, A.RACRAC
                ORDER BY A.SUPSCD, A.SUPPCD, B.PAMUNO",
                VarChar("@PLCPCD", plantCode),
                VarChar("@FROM", from.ToString("yyyyMMdd")),
                VarChar("@TO", to.ToString("yyyyMMdd")));
        }

        // DN sisa pertama (DN#1) = pengiriman yang terlambat / kurang. Desktop tanpa filter plant;
        // di web plant opsional (kosong = semua plant).
        public DataTable SummaryDelayDelivery(string plantCode, DateTime from, DateTime to, DBContext db)
        {
            db.Database.SetCommandTimeout(REPORT_TIMEOUT_SECONDS);
            return QueryTable(db, @"
                SELECT A.SOCDNO, A.PAMPNO, A.SOCPRT, B.PAMPNM, B.PAMUNO, SUM(A.KBNORDTPC) AS KBNORDTPC,
                       dbo.ChangeToDate(A.SOCPRD) AS SOCPRD, A.SUPSCD, A.SUPPCD, C.SUPNAM, A.SUPSHD, A.RACRAC, A.PLCPCD
                " + ORDER_JOIN + @"
                WHERE A.SOCPRD >= @FROM AND A.SOCPRD <= @TO
                  AND A.SOCDNO LIKE '%#1'
                  AND (@PLCPCD IS NULL OR A.PLCPCD = @PLCPCD)
                GROUP BY A.SOCDNO, A.PAMPNO, B.PAMPNM, B.PAMUNO, dbo.ChangeToDate(A.SOCPRD), A.SUPSCD, A.SUPPCD, C.SUPNAM,
                         A.SUPSHD, A.RACRAC, A.SOCPRT, A.PLCPCD
                ORDER BY A.SUPSCD, A.SUPPCD, B.PAMUNO",
                VarChar("@PLCPCD", string.IsNullOrEmpty(plantCode) ? null : plantCode),
                VarChar("@FROM", from.ToString("yyyyMMdd")),
                VarChar("@TO", to.ToString("yyyyMMdd")));
        }
    }
}
