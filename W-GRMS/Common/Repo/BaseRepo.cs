using GRMS.Common.Models;
using GRMS.Models;
using GRMS.Models.DB;
using GRMS.Models.Repo;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Text.RegularExpressions;

namespace GRMS.Common.Repo
{
    // Semua SQL W-GRMS ditulis di repo (query berparameter), TIDAK memakai stored procedure/view
    // di database. Helper di sini menggantikan pola SP GRMS: paging ROW_NUMBER, filter LIKE dengan
    // wildcard '*', serta log proses TB_R_LOG_H/D untuk operasi simpan/hapus.
    public class BaseRepo
    {
        protected object CheckNullValue(int? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }
        protected object CheckNullValue(long? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }
        protected object CheckNullValue(Decimal? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }

        protected object CheckNullValue(string? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }

        protected object CheckNullValue(Guid? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }

        protected object CheckNullValue(DateTime? val)
        {
            if (!val.HasValue) return DBNull.Value;
            return val;
        }

        // Parameter VARCHAR. SqlParameter string default-nya NVARCHAR; dibandingkan dengan kolom
        // VARCHAR GRMS (OLKKBNORD dll) SQL Server mengonversi kolomnya sehingga index tidak terpakai
        // dan tabel jutaan baris di-scan. Pakai ini untuk filter di tabel besar.
        protected static SqlParameter VarChar(string name, object? value)
        {
            return new SqlParameter(name, SqlDbType.VarChar, 50) { Value = value ?? DBNull.Value };
        }

        // Wildcard '*' dari UI (konvensi GRMS) -> '%' untuk LIKE
        protected object LikeValue(string? val)
        {
            if (val == null) return DBNull.Value;
            return val.Replace("*", "%");
        }

        // Bungkus SELECT yang punya kolom "RowNumber" (ROW_NUMBER() OVER ...) dengan paging.
        // OFFSET/FETCH tidak dipakai karena compatibility level GRMS = 100.
        protected string Paged(string selectWithRowNumber, List<SqlParameter> param, int? pageNumber, int? pageSize, string orderBy = "RowNumber")
        {
            string query = "WITH data AS (" + selectWithRowNumber + ") SELECT * FROM data";
            if (pageNumber != null && pageSize != null)
            {
                query += " WHERE RowNumber > @PAGE_START AND RowNumber <= @PAGE_END";
                param.Add(new SqlParameter("@PAGE_START", pageSize.Value * (pageNumber.Value - 1)));
                param.Add(new SqlParameter("@PAGE_END", pageSize.Value * pageNumber.Value));
            }
            return query + " ORDER BY " + orderBy;
        }

        // Prefix linked server CKB disisipkan ke SQL sebagai nama objek (tidak bisa jadi parameter),
        // jadi wajib berformat persis [server].[database].[schema] dari appsettings "CkbSources".
        private static readonly Regex LINKED_SOURCE_PATTERN = new Regex(@"^\[[^\[\]]+\]\.\[[^\[\]]+\]\.\[[^\[\]]+\]$");

        protected static string CheckSource(string source)
        {
            if (string.IsNullOrEmpty(source) || !LINKED_SOURCE_PATTERN.IsMatch(source))
            {
                throw new InvalidOperationException("Invalid CKB source '" + source + "' in appsettings (expected [server].[database].[schema])");
            }
            return source;
        }

        // Query scalar integer, kolom hasil harus bernama "Value" (konvensi EF SqlQueryRaw)
        protected int ScalarInt(DBContext db, string sqlSelectValue, params SqlParameter[] param)
        {
            return db.Database.SqlQueryRaw<int>(sqlSelectValue, param).AsEnumerable().First();
        }

        protected bool Exists(DBContext db, string sqlSelect, params SqlParameter[] param)
        {
            return ScalarInt(db, "SELECT CASE WHEN EXISTS (" + sqlSelect + ") THEN 1 ELSE 0 END AS Value", param) == 1;
        }

        // Query bebas -> DataTable lewat koneksi EF (ikut transaksi EF yang sedang aktif).
        // Dipakai untuk hasil yang bentuknya tidak tetap (report, data SOC) atau baris yang
        // diproses ulang di kode.
        protected DataTable QueryTable(DBContext db, string sql, params SqlParameter[] param)
        {
            var conn = (SqlConnection)db.Database.GetDbConnection();
            bool opened = false;
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
                opened = true;
            }

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = db.Database.GetCommandTimeout() ?? 30;
                cmd.Transaction = (SqlTransaction)db.Database.CurrentTransaction?.GetDbTransaction();
                cmd.Parameters.AddRange(param);

                DataTable dt = new DataTable();
                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
                cmd.Parameters.Clear();
                return dt;
            }
            finally
            {
                if (opened) conn.Close();
            }
        }

        // DataTable -> list dictionary supaya bisa dikirim sebagai JSON (System.Text.Json tidak mendukung DataTable)
        public static List<Dictionary<string, object>> ToRows(DataTable dt)
        {
            return dt.AsEnumerable()
                .Select(r => dt.Columns.Cast<DataColumn>()
                    .ToDictionary(c => c.ColumnName, c => r.IsNull(c) ? null : r[c]))
                .ToList();
        }

        protected SqlParameter CreateSqlParameterOutputInt(string parameterName)
        {
            var output = new SqlParameter(parameterName, SqlDbType.Int);
            output.Direction = System.Data.ParameterDirection.Output;
            output.Value = -1;

            return output;
        }

        protected SqlParameter CreateSqlParameterOutputString(string parameterName)
        {
            var output = new SqlParameter(parameterName, SqlDbType.VarChar, 2000);
            output.Direction = System.Data.ParameterDirection.Output;
            output.Value = string.Empty;

            return output;
        }

        // Pengganti pola SP GRMS (sp_StartLog -> proses -> sp_WriteLog dengan status 2/3/4):
        //   body mengembalikan DBResult; status=false dicatat sebagai "finish with error" (3),
        //   exception dicatat sebagai "abnormal" (4) dan dikembalikan sebagai DBResult gagal.
        protected DBResult RunWithLog(DBContext db, string module, string function, string location, string loginUser, Func<DBResult> body)
        {
            LogMonitoringRepo logRepo = LogMonitoringRepo.Instance;
            long processId = logRepo.StartLog(new LogHeader { MODULE = module, FUNCTION = function }, location, loginUser, db);

            try
            {
                DBResult result = body();
                if (result.status)
                {
                    logRepo.WriteLog(processId, "2", "INF", result.message, location, loginUser, db);
                }
                else
                {
                    logRepo.WriteLog(processId, "3", "ERR", result.message, location, loginUser, db);
                }
                return result;
            }
            catch (Exception ex)
            {
                string message = "ERROR: " + location + ": " + ex.Message;
                logRepo.WriteLog(processId, "4", "ERR", message, location, loginUser, db);
                return new DBResult(false, message);
            }
        }

        protected static DBResult Fail(string message)
        {
            return new DBResult(false, "ERROR: " + message);
        }

        protected static DBResult Ok(string message)
        {
            return new DBResult(true, message);
        }
    }
}
