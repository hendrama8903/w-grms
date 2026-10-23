using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    public class LoginRepo : BaseRepo
    {

        private LoginRepo() { }

        #region Singleton
        private static LoginRepo instance = null;
        public static LoginRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LoginRepo();
                }
                return instance;
            }
        }
        #endregion

        // User aktif + nama role (user tanpa role valid tidak bisa login)
        public User GetUserByUsername(User data, DBContext db)
        {
            string query = @"
                SELECT A.USER_ID, A.USER_NAME, A.USER_PWD, A.DEPT, A.NIK, A.EXP_PWD, A.EMAIL, A.PHONE,
                       A.ROLE_ID, B.ROLE_NAME, A.DELETE_FLAG, A.CREATED_DT, A.CREATED_BY, A.CHANGED_DT, A.CHANGED_BY
                FROM [dbo].[TB_M_USER] A
                JOIN [dbo].[TB_M_ROLE] B ON B.ROLE_ID = A.ROLE_ID
                WHERE A.USER_ID = @USERNAME
                  AND ISNULL(A.DELETE_FLAG, '0') <> '1'";

            return db.User.FromSqlRaw<User>(query, new SqlParameter("@USERNAME", CheckNullValue(data.USER_ID)))
                .AsNoTracking().AsEnumerable().FirstOrDefault();
        }

        // Menu aktif milik role (menu/parent yang dinonaktifkan tidak ikut)
        public List<Menu> GetMenuByRole(User data, DBContext db)
        {
            string query = @"
                SELECT M.MENU_ID, M.MENU_NAME, M.MENU_ICON, M.MENU_URL, M.PARENT_ID, B.MENU_NAME AS PARENT_NAME,
                       M.MENU_SEQ, M.CREATED_DT, M.CREATED_BY, M.CHANGED_DT, M.CHANGED_BY
                FROM [dbo].[TB_M_MENU] M
                JOIN [dbo].[TB_M_AUTH_MENU] A ON A.MENU_ID = M.MENU_ID
                LEFT JOIN [dbo].[TB_M_MENU] B ON M.PARENT_ID = B.MENU_ID
                WHERE A.ROLE_ID = @ROLE_ID
                  AND ISNULL(M.DELETE_FLAG, 0) = 0
                  AND ISNULL(B.DELETE_FLAG, 0) = 0
                ORDER BY M.MENU_SEQ ASC";

            return db.Menu.FromSqlRaw<Menu>(query, new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)))
                .AsNoTracking().ToList();
        }

        // Function aktif milik role
        public List<Function> GetFunctionByRole(User data, DBContext db)
        {
            string query = @"
                SELECT F.FUNCTION_ID, F.FUNCTION_NAME, F.FUNCTION_DESC, F.MENU_ID, M.MENU_NAME,
                       F.CREATED_DT, F.CREATED_BY, F.CHANGED_DT, F.CHANGED_BY
                FROM [dbo].[TB_M_FUNCTION] F
                JOIN [dbo].[TB_M_AUTH_FUNCTION] A ON A.FUNCTION_ID = F.FUNCTION_ID
                LEFT JOIN [dbo].[TB_M_MENU] M ON M.MENU_ID = F.MENU_ID
                WHERE A.ROLE_ID = @ROLE_ID
                  AND ISNULL(F.DELETE_FLAG, 0) = 0
                  AND ISNULL(M.DELETE_FLAG, 0) = 0";

            return db.Function.FromSqlRaw<Function>(query, new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)))
                .AsNoTracking().ToList();
        }
    }
}
