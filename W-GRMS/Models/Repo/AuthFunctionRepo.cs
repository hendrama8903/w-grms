using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    // TB_M_AUTH_FUNCTION - function (tombol) yang diberikan ke role
    public class AuthFunctionRepo : BaseRepo
    {
        private AuthFunctionRepo() { }

        #region Singleton
        private static AuthFunctionRepo instance = null;
        public static AuthFunctionRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AuthFunctionRepo();
                }
                return instance;
            }
        }
        #endregion

        private const string COLUMNS = "ROLE_ID, FUNCTION_ID, CREATED_DT, CREATED_BY, CHANGED_DT, CHANGED_BY";

        public IList<AuthFunction> Search(AuthFunction data, DBContext db)
        {
            string query = "SELECT " + COLUMNS + " FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE (@ROLE_ID IS NULL OR ROLE_ID LIKE @ROLE_ID)";
            return db.AuthFunction.FromSqlRaw<AuthFunction>(query, new SqlParameter("@ROLE_ID", LikeValue(data.ROLE_ID)))
                .AsNoTracking().ToList();
        }

        public IList<AuthFunction> GetAll(DBContext db)
        {
            string query = "SELECT " + COLUMNS + " FROM [dbo].[TB_M_AUTH_FUNCTION]";
            return db.AuthFunction.FromSqlRaw<AuthFunction>(query).AsNoTracking().ToList();
        }

        // Dipanggil di dalam transaksi RoleController.AddEditFunction (log ditulis oleh controller)
        public DBResult Insert(AuthFunction data, string loginUser, DBContext db)
        {
            if (string.IsNullOrEmpty(data.FUNCTION_ID)) return Fail("Function should not be null");

            db.Database.ExecuteSqlRaw(@"
                INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_DT, CREATED_BY)
                VALUES (@ROLE_ID, @FUNCTION_ID, GETDATE(), @LOGIN_USER)",
                new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)),
                new SqlParameter("@FUNCTION_ID", data.FUNCTION_ID),
                new SqlParameter("@LOGIN_USER", loginUser));

            return Ok("Successfully saved function data");
        }

        public DBResult DeleteByRole(AuthFunction data, DBContext db)
        {
            db.Database.ExecuteSqlRaw("DELETE FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE ROLE_ID = @ROLE_ID",
                new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)));

            return Ok("Successfully deleted data");
        }
    }
}
