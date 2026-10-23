using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    // TB_M_AUTH_MENU - menu yang diberikan ke role
    public class AuthMenuRepo : BaseRepo
    {
        private AuthMenuRepo() { }

        #region Singleton
        private static AuthMenuRepo instance = null;
        public static AuthMenuRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AuthMenuRepo();
                }
                return instance;
            }
        }
        #endregion

        private const string COLUMNS = "ROLE_ID, MENU_ID, CREATED_DT, CREATED_BY, CHANGED_DT, CHANGED_BY";

        public IList<AuthMenu> Search(AuthMenu data, DBContext db)
        {
            string query = "SELECT " + COLUMNS + " FROM [dbo].[TB_M_AUTH_MENU] WHERE (@ROLE_ID IS NULL OR ROLE_ID LIKE @ROLE_ID)";
            return db.AuthMenu.FromSqlRaw<AuthMenu>(query, new SqlParameter("@ROLE_ID", LikeValue(data.ROLE_ID)))
                .AsNoTracking().ToList();
        }

        public IList<AuthMenu> GetAll(DBContext db)
        {
            string query = "SELECT " + COLUMNS + " FROM [dbo].[TB_M_AUTH_MENU]";
            return db.AuthMenu.FromSqlRaw<AuthMenu>(query).AsNoTracking().ToList();
        }

        // Dipanggil di dalam transaksi RoleController.AddEditMenu (log ditulis oleh controller)
        public DBResult Insert(AuthMenu data, string loginUser, DBContext db)
        {
            if (string.IsNullOrEmpty(data.MENU_ID)) return Fail("Menu should not be null");

            db.Database.ExecuteSqlRaw(@"
                INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_DT, CREATED_BY)
                VALUES (@ROLE_ID, @MENU_ID, GETDATE(), @LOGIN_USER)",
                new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)),
                new SqlParameter("@MENU_ID", data.MENU_ID),
                new SqlParameter("@LOGIN_USER", loginUser));

            return Ok("Successfully saved menu data");
        }

        public DBResult DeleteByRole(AuthMenu data, DBContext db)
        {
            db.Database.ExecuteSqlRaw("DELETE FROM [dbo].[TB_M_AUTH_MENU] WHERE ROLE_ID = @ROLE_ID",
                new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)));

            return Ok("Successfully deleted data");
        }
    }
}
