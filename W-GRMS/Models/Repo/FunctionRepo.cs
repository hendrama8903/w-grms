using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    public class FunctionRepo : BaseRepo
    {
        private FunctionRepo() { }

        #region Singleton
        private static FunctionRepo instance = null;
        public static FunctionRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new FunctionRepo();
                }
                return instance;
            }
        }
        #endregion

        private const string COLUMNS = @"
            F.FUNCTION_ID, F.FUNCTION_NAME, F.FUNCTION_DESC, F.MENU_ID, M.MENU_NAME,
            F.CREATED_DT, F.CREATED_BY, F.CHANGED_DT, F.CHANGED_BY";

        private const string FROM = @"
            FROM [dbo].[TB_M_FUNCTION] F
            LEFT JOIN [dbo].[TB_M_MENU] M ON F.MENU_ID = M.MENU_ID";

        public IList<Function> GetAll(DBContext db)
        {
            string query = "SELECT " + COLUMNS + FROM + " ORDER BY F.FUNCTION_ID ASC";
            return db.Function.FromSqlRaw<Function>(query).AsNoTracking().ToList();
        }

        public IList<Function> Search(Function data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@FUNCTION_ID", LikeValue(data.FUNCTION_ID) ),
                new SqlParameter ( "@FUNCTION_NAME", LikeValue(data.FUNCTION_NAME) )
            };

            string select = "SELECT ROW_NUMBER() OVER (ORDER BY F.FUNCTION_ID ASC) AS RowNumber, " + COLUMNS + FROM + @"
                WHERE (@FUNCTION_ID IS NULL OR F.FUNCTION_ID LIKE @FUNCTION_ID)
                  AND (@FUNCTION_NAME IS NULL OR F.FUNCTION_NAME LIKE @FUNCTION_NAME)";

            string query = Paged(select, param, PageNumber, PageSize);
            return db.Function.FromSqlRaw<Function>(query, param.ToArray()).AsNoTracking().ToList();
        }

        public Function GetByKey(Function data, DBContext db)
        {
            string query = "SELECT " + COLUMNS + FROM + " WHERE F.FUNCTION_ID = @FUNCTION_ID";
            return db.Function.FromSqlRaw<Function>(query, new SqlParameter("@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID)))
                .AsNoTracking().AsEnumerable().FirstOrDefault();
        }

        public IList<Function> GetByMenu(Function data, DBContext db)
        {
            string query = "SELECT " + COLUMNS + FROM + " WHERE (@MENU_ID IS NULL OR F.MENU_ID LIKE @MENU_ID)";
            return db.Function.FromSqlRaw<Function>(query, new SqlParameter("@MENU_ID", LikeValue(data.MENU_ID)))
                .AsNoTracking().ToList();
        }

        // Panel kanan halaman Menu & Function: + status aktif & jumlah role pemakai
        public IList<FunctionListItem> GetByMenuList(Function data, DBContext db)
        {
            string query = "SELECT " + COLUMNS + @",
                    ISNULL(F.DELETE_FLAG, 0) AS DELETE_FLAG,
                    ISNULL((SELECT COUNT(DISTINCT A.ROLE_ID) FROM [dbo].[TB_M_AUTH_FUNCTION] A WHERE A.FUNCTION_ID = F.FUNCTION_ID), 0) AS USED_BY_ROLES
                " + FROM + " WHERE (@MENU_ID IS NULL OR F.MENU_ID LIKE @MENU_ID)";

            return db.FunctionListItem.FromSqlRaw<FunctionListItem>(query, new SqlParameter("@MENU_ID", LikeValue(data.MENU_ID))).ToList();
        }

        public DBResult ToggleStatus(string functionId, int deleteFlag, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Function", "ToggleStatus", "FunctionRepo.ToggleStatus", loginUser, () =>
            {
                db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_FUNCTION]
                    SET DELETE_FLAG = @DELETE_FLAG, CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER
                    WHERE FUNCTION_ID = @FUNCTION_ID",
                    new SqlParameter("@FUNCTION_ID", CheckNullValue(functionId)),
                    new SqlParameter("@DELETE_FLAG", deleteFlag),
                    new SqlParameter("@LOGIN_USER", loginUser));

                return Ok("Successfully updated data");
            });
        }

        public DBResult Insert(Function data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Function", "Insert", "FunctionRepo.Insert", loginUser, () =>
            {
                if (string.IsNullOrEmpty(data.FUNCTION_ID)) return Fail("Function ID should not be null");
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = @FUNCTION_ID", new SqlParameter("@FUNCTION_ID", data.FUNCTION_ID)))
                {
                    return Fail("Data already exists, Function ID = " + data.FUNCTION_ID);
                }
                if (string.IsNullOrEmpty(data.FUNCTION_NAME)) return Fail("Function Name should not be null");
                if (string.IsNullOrEmpty(data.MENU_ID)) return Fail("Menu should not be null");

                db.Database.ExecuteSqlRaw(@"
                    INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, FUNCTION_NAME, FUNCTION_DESC, MENU_ID, CREATED_DT, CREATED_BY)
                    VALUES (@FUNCTION_ID, @FUNCTION_NAME, @FUNCTION_DESC, @MENU_ID, GETDATE(), @LOGIN_USER)",
                    new SqlParameter("@FUNCTION_ID", data.FUNCTION_ID),
                    new SqlParameter("@FUNCTION_NAME", data.FUNCTION_NAME),
                    new SqlParameter("@FUNCTION_DESC", CheckNullValue(data.FUNCTION_DESC)),
                    new SqlParameter("@MENU_ID", data.MENU_ID),
                    new SqlParameter("@LOGIN_USER", loginUser));

                return Ok("Successfully saved data");
            });
        }

        public DBResult Update(Function data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Function", "Update", "FunctionRepo.Update", loginUser, () =>
            {
                if (string.IsNullOrEmpty(data.FUNCTION_NAME)) return Fail("Function Name should not be null");

                db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_FUNCTION]
                    SET FUNCTION_NAME = @FUNCTION_NAME, FUNCTION_DESC = @FUNCTION_DESC, MENU_ID = @MENU_ID,
                        CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER
                    WHERE FUNCTION_ID = @FUNCTION_ID",
                    new SqlParameter("@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID)),
                    new SqlParameter("@FUNCTION_NAME", data.FUNCTION_NAME),
                    new SqlParameter("@FUNCTION_DESC", CheckNullValue(data.FUNCTION_DESC)),
                    new SqlParameter("@MENU_ID", CheckNullValue(data.MENU_ID)),
                    new SqlParameter("@LOGIN_USER", loginUser));

                return Ok("Successfully saved data");
            });
        }

        public DBResult Delete(Function data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Function", "Delete", "FunctionRepo.Delete", loginUser, () =>
            {
                // Function yang masih dipasang ke role tidak boleh dihapus (cegah TB_M_AUTH_FUNCTION yatim)
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE FUNCTION_ID = @FUNCTION_ID", new SqlParameter("@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID))))
                {
                    return Fail("Function is still assigned to one or more roles - deactivate this function or remove its access in Role Authorization before deleting it");
                }

                db.Database.ExecuteSqlRaw("DELETE FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = @FUNCTION_ID",
                    new SqlParameter("@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID)));

                return Ok("Successfully deleted data");
            });
        }
    }
}
