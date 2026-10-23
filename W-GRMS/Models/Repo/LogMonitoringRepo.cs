using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GRMS.Models.Repo
{
    public class LogMonitoringRepo : BaseRepo
    {
        private LogMonitoringRepo() { }

        #region Singleton
        private static LogMonitoringRepo instance = null;
        public static LogMonitoringRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LogMonitoringRepo();
                }
                return instance;
            }
        }
        #endregion

        private const string HEADER_COLUMNS = @"
            H.PROCESS_ID, H.MODULE, H.[FUNCTION], H.START_DT, H.END_DT, H.PROCESS_STATUS,
            H.CREATED_DT, H.CREATED_BY, H.CHANGED_DT, H.CHANGED_BY";

        public IList<LogHeader> Search(LogHeader data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@START_DT", CheckNullValue(data.START_DT?.Date) ),
                new SqlParameter ( "@END_DT", CheckNullValue(data.END_DT?.Date) ),
                new SqlParameter ( "@PROCESS_ID", CheckNullValue(data.PROCESS_ID) ),
                new SqlParameter ( "@MODULE", LikeValue(data.MODULE) ),
                new SqlParameter ( "@FUNCTION", LikeValue(data.FUNCTION) ),
                new SqlParameter ( "@CREATED_BY", LikeValue(data.CREATED_BY) ),
                new SqlParameter ( "@PROCESS_STATUS", LikeValue(data.PROCESS_STATUS) )
            };

            string select = @"
                SELECT ROW_NUMBER() OVER (ORDER BY H.PROCESS_ID DESC) AS RowNumber, " + HEADER_COLUMNS + @"
                FROM [dbo].[TB_R_LOG_H] H
                WHERE (@START_DT IS NULL OR @END_DT IS NULL
                       OR (CAST(H.START_DT AS DATE) >= @START_DT AND CAST(H.END_DT AS DATE) <= @END_DT))
                  AND (@PROCESS_ID IS NULL OR H.PROCESS_ID = @PROCESS_ID)
                  AND (@MODULE IS NULL OR H.MODULE LIKE @MODULE)
                  AND (@FUNCTION IS NULL OR H.[FUNCTION] LIKE @FUNCTION)
                  AND (@CREATED_BY IS NULL OR H.CREATED_BY LIKE @CREATED_BY)
                  AND (@PROCESS_STATUS IS NULL OR H.PROCESS_STATUS LIKE @PROCESS_STATUS)";

            string query = Paged(select, param, PageNumber, PageSize);
            return db.LogHeader.FromSqlRaw<LogHeader>(query, param.ToArray()).ToList();
        }

        public LogHeader GetByKey(LogHeader data, DBContext db)
        {
            string query = "SELECT " + HEADER_COLUMNS + " FROM [dbo].[TB_R_LOG_H] H WHERE H.PROCESS_ID = @PROCESS_ID";
            return db.LogHeader.FromSqlRaw<LogHeader>(query, new SqlParameter("@PROCESS_ID", CheckNullValue(data.PROCESS_ID)))
                .AsEnumerable().FirstOrDefault();
        }

        public IList<LogDetail> SearchDetail(LogDetail data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@PROCESS_ID", CheckNullValue(data.PROCESS_ID) )
            };

            string select = @"
                SELECT ROW_NUMBER() OVER (ORDER BY D.SEQ_NO ASC) AS RowNumber,
                    D.PROCESS_ID, D.SEQ_NO, D.MESSAGE_TYPE, D.MESSAGE_CONTENT, D.LOCATION,
                    D.CREATED_DT, D.CREATED_BY, D.CHANGED_DT, D.CHANGED_BY
                FROM [dbo].[TB_R_LOG_D] D
                WHERE (@PROCESS_ID IS NULL OR D.PROCESS_ID = @PROCESS_ID)";

            string query = Paged(select, param, PageNumber, PageSize);
            return db.LogDetail.FromSqlRaw<LogDetail>(query, param.ToArray()).ToList();
        }

        // Satu baris per MODULE (proses terakhir), untuk pilihan filter
        public IList<LogHeader> GetListModule(LogHeader data, DBContext db, int? PageNumber, int? PageSize)
        {
            return GetDistinctBy("MODULE", data.MODULE, db, PageNumber, PageSize);
        }

        // Satu baris per FUNCTION (proses terakhir), untuk pilihan filter
        public IList<LogHeader> GetListFunction(LogHeader data, DBContext db, int? PageNumber, int? PageSize)
        {
            return GetDistinctBy("[FUNCTION]", data.FUNCTION, db, PageNumber, PageSize);
        }

        private IList<LogHeader> GetDistinctBy(string column, string filter, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@FILTER", LikeValue(filter) )
            };

            string select = @"
                SELECT ROW_NUMBER() OVER (ORDER BY H." + column + @" ASC) AS RowNumber, " + HEADER_COLUMNS + @"
                FROM [dbo].[TB_R_LOG_H] H
                JOIN (SELECT MAX(L.PROCESS_ID) AS PROCESS_IDS FROM [dbo].[TB_R_LOG_H] L GROUP BY L." + column + @") B
                    ON H.PROCESS_ID = B.PROCESS_IDS
                WHERE (@FILTER IS NULL OR H." + column + " LIKE @FILTER)";

            string query = Paged(select, param, PageNumber, PageSize);
            return db.LogHeader.FromSqlRaw<LogHeader>(query, param.ToArray()).ToList();
        }

        // PROCESS_STATUS : 0 = Start, 1 = Process, 2 = Finish, 3 = Finish with error, 4 = Abnormal
        // MESSAGE_TYPE   : ERR, INF, WRN
        public long StartLog(LogHeader data, string location, string loginUser, DBContext db)
        {
            SqlParameter processID = new SqlParameter("@PROCESS_ID", SqlDbType.BigInt) { Direction = ParameterDirection.Output };

            List<SqlParameter> param = new List<SqlParameter>
            {
                processID,
                new SqlParameter ( "@MODULE", CheckNullValue(data.MODULE) ),
                new SqlParameter ( "@FUNCTION", CheckNullValue(data.FUNCTION) ),
                new SqlParameter ( "@LOCATION", CheckNullValue(location) ),
                new SqlParameter ( "@LOGIN_USER", string.IsNullOrEmpty(loginUser) ? "system" : loginUser )
            };

            string query = @"
                SELECT @PROCESS_ID = ISNULL(MAX(PROCESS_ID) + 1, 1) FROM [dbo].[TB_R_LOG_H] WITH (UPDLOCK, HOLDLOCK);

                INSERT INTO [dbo].[TB_R_LOG_H] (PROCESS_ID, MODULE, [FUNCTION], START_DT, END_DT, PROCESS_STATUS, CREATED_BY, CREATED_DT)
                VALUES (@PROCESS_ID, @MODULE, @FUNCTION, SYSDATETIME(), SYSDATETIME(), '0', @LOGIN_USER, SYSDATETIME());

                INSERT INTO [dbo].[TB_R_LOG_D] (PROCESS_ID, SEQ_NO, MESSAGE_TYPE, MESSAGE_CONTENT, LOCATION, CREATED_BY, CREATED_DT)
                VALUES (@PROCESS_ID, 1, 'INF', 'Process Start', @LOCATION, @LOGIN_USER, SYSDATETIME());";

            db.Database.ExecuteSqlRaw(query, param.ToArray());

            return Convert.ToInt64(processID.Value);
        }

        public string WriteLog(long processid, string processStatus, string messageType, string messageContent, string location, string loginUser, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@PROCESS_ID", processid ),
                new SqlParameter ( "@PROCESS_STATUS", CheckNullValue(processStatus) ),
                new SqlParameter ( "@MESSAGE_TYPE", CheckNullValue(messageType) ),
                new SqlParameter ( "@MESSAGE_CONTENT", CheckNullValue(messageContent) ),
                new SqlParameter ( "@LOCATION", CheckNullValue(location) ),
                new SqlParameter ( "@LOGIN_USER", string.IsNullOrEmpty(loginUser) ? "system" : loginUser )
            };

            string query = @"
                DECLARE @SEQ INT;
                SELECT @SEQ = ISNULL(MAX(SEQ_NO) + 1, 1) FROM [dbo].[TB_R_LOG_D] WHERE PROCESS_ID = @PROCESS_ID;

                INSERT INTO [dbo].[TB_R_LOG_D] (PROCESS_ID, SEQ_NO, MESSAGE_TYPE, MESSAGE_CONTENT, LOCATION, CREATED_BY, CREATED_DT)
                VALUES (@PROCESS_ID, @SEQ, @MESSAGE_TYPE, @MESSAGE_CONTENT, @LOCATION, @LOGIN_USER, SYSDATETIME());

                UPDATE [dbo].[TB_R_LOG_H]
                SET PROCESS_STATUS = @PROCESS_STATUS, END_DT = SYSDATETIME(), CHANGED_BY = @LOGIN_USER, CHANGED_DT = SYSDATETIME()
                WHERE PROCESS_ID = @PROCESS_ID;

                IF @PROCESS_STATUS IN ('2', '3', '4')
                    INSERT INTO [dbo].[TB_R_LOG_D] (PROCESS_ID, SEQ_NO, MESSAGE_TYPE, MESSAGE_CONTENT, LOCATION, CREATED_BY, CREATED_DT)
                    VALUES (@PROCESS_ID, @SEQ + 1, 'INF', 'Process Finish', @LOCATION, @LOGIN_USER, SYSDATETIME());";

            try
            {
                db.Database.ExecuteSqlRaw(query, param.ToArray());
            }
            catch
            {
                // Gagal tulis log tidak boleh menggagalkan proses utama (perilaku sp_WriteLog GRMS)
                return "false";
            }

            return "true";
        }
    }
}
