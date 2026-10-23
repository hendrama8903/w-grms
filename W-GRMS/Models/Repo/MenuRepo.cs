using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    public class MenuRepo : BaseRepo
    {
        private MenuRepo() { }

        #region Singleton
        private static MenuRepo instance = null;
        public static MenuRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MenuRepo();
                }
                return instance;
            }
        }
        #endregion

        private const string COLUMNS = @"
            M.MENU_ID, M.PARENT_ID, B.MENU_NAME AS PARENT_NAME, M.MENU_NAME, M.MENU_ICON, M.MENU_URL, M.MENU_SEQ,
            M.CREATED_DT, M.CREATED_BY, M.CHANGED_DT, M.CHANGED_BY";

        private const string FROM = @"
            FROM [dbo].[TB_M_MENU] M
            LEFT JOIN [dbo].[TB_M_MENU] B ON M.PARENT_ID = B.MENU_ID";

        // Angka kartu statistik halaman Menu & Function
        public MenuFunctionStats GetStats(DBContext db)
        {
            string query = @"
                SELECT
                    (SELECT COUNT(*) FROM [dbo].[TB_M_MENU]) AS TOTAL_MENUS,
                    (SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION]) AS TOTAL_FUNCTIONS,
                    (SELECT COUNT(DISTINCT ROLE_ID) FROM (
                        SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_MENU]
                        UNION
                        SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_FUNCTION]) R) AS USED_BY_ROLES,
                    (SELECT COUNT(*) FROM [dbo].[TB_M_MENU] WHERE ISNULL(DELETE_FLAG, 0) = 1) +
                    (SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION] WHERE ISNULL(DELETE_FLAG, 0) = 1) AS INACTIVE_COUNT";

            return db.MenuFunctionStats.FromSqlRaw<MenuFunctionStats>(query).AsEnumerable().FirstOrDefault();
        }

        // Seluruh menu (tanpa paging) untuk tree panel: + jumlah function, jumlah role pemakai, status aktif
        public IList<MenuListItem> GetTree(DBContext db)
        {
            string query = "SELECT " + COLUMNS + @",
                    ISNULL(M.DELETE_FLAG, 0) AS DELETE_FLAG,
                    CASE
                        WHEN M.PARENT_ID IS NULL THEN ISNULL((
                            SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION] F
                            JOIN [dbo].[TB_M_MENU] C ON C.MENU_ID = F.MENU_ID
                            WHERE C.PARENT_ID = M.MENU_ID), 0)
                        ELSE ISNULL((SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION] F WHERE F.MENU_ID = M.MENU_ID), 0)
                    END AS FUNCTION_COUNT,
                    ISNULL((SELECT COUNT(DISTINCT A.ROLE_ID) FROM [dbo].[TB_M_AUTH_MENU] A WHERE A.MENU_ID = M.MENU_ID), 0) AS USED_BY_ROLES
                " + FROM + " ORDER BY M.PARENT_ID ASC, M.MENU_SEQ ASC";

            return db.MenuListItem.FromSqlRaw<MenuListItem>(query).ToList();
        }

        public DBResult ToggleStatus(string menuId, int deleteFlag, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Menu", "ToggleStatus", "MenuRepo.ToggleStatus", loginUser, () =>
            {
                db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_MENU]
                    SET DELETE_FLAG = @DELETE_FLAG, CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER
                    WHERE MENU_ID = @MENU_ID",
                    new SqlParameter("@MENU_ID", CheckNullValue(menuId)),
                    new SqlParameter("@DELETE_FLAG", deleteFlag),
                    new SqlParameter("@LOGIN_USER", loginUser));

                return Ok("Successfully updated data");
            });
        }

        public IList<Menu> GetAll(DBContext db)
        {
            string query = "SELECT " + COLUMNS + FROM + " ORDER BY M.MENU_SEQ ASC";
            return db.Menu.FromSqlRaw<Menu>(query).AsNoTracking().ToList();
        }

        public IList<Menu> Search(Menu data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_NAME", LikeValue(data.MENU_NAME) ),
                new SqlParameter ( "@PARENT_ID", LikeValue(data.PARENT_ID) )
            };

            // ORDERING: parent dulu, lalu child-nya berurutan di bawah parent
            string select = "SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS RowNumber, " + COLUMNS + @",
                    CASE WHEN M.PARENT_ID IS NULL THEN M.MENU_SEQ * 100 ELSE M.MENU_SEQ + B.MENU_SEQ * 100 END AS ORDERING
                " + FROM + @"
                WHERE (@MENU_NAME IS NULL OR M.MENU_NAME LIKE @MENU_NAME)
                  AND (@PARENT_ID IS NULL OR M.PARENT_ID LIKE @PARENT_ID)";

            string query = Paged(select, param, PageNumber, PageSize, "ORDERING ASC");
            return db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).AsNoTracking().ToList();
        }

        public IList<Menu> SearchParent(Menu data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_NAME", LikeValue(data.MENU_NAME) )
            };

            string select = "SELECT ROW_NUMBER() OVER (ORDER BY M.MENU_ID ASC) AS RowNumber, " + COLUMNS + FROM + @"
                WHERE (M.PARENT_ID IS NULL OR LEN(M.PARENT_ID) < 1)
                  AND (@MENU_NAME IS NULL OR M.MENU_NAME LIKE @MENU_NAME)";

            string query = Paged(select, param, PageNumber, PageSize);
            return db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).AsNoTracking().ToList();
        }

        public IList<Menu> SearchChild(Menu data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_NAME", LikeValue(data.MENU_NAME) )
            };

            string select = "SELECT ROW_NUMBER() OVER (ORDER BY M.MENU_ID ASC) AS RowNumber, " + COLUMNS + FROM + @"
                WHERE M.PARENT_ID IS NOT NULL
                  AND (@MENU_NAME IS NULL OR M.MENU_NAME LIKE @MENU_NAME)";

            string query = Paged(select, param, PageNumber, PageSize);
            return db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).AsNoTracking().ToList();
        }

        public Menu GetByKey(Menu data, DBContext db)
        {
            string query = "SELECT " + COLUMNS + FROM + " WHERE M.MENU_ID = @MENU_ID";
            return db.Menu.FromSqlRaw<Menu>(query, new SqlParameter("@MENU_ID", CheckNullValue(data.MENU_ID)))
                .AsNoTracking().AsEnumerable().FirstOrDefault();
        }

        // ID menu berikutnya: parent "M00001", child "<PARENT_ID>-01"
        public String GetMenuLastID(Menu data, DBContext db)
        {
            if (string.IsNullOrEmpty(data.PARENT_ID))
            {
                int seq = ScalarInt(db, @"
                    SELECT ISNULL(MAX(CAST(SUBSTRING(MENU_ID, 2, 5) AS INT)), 0) AS Value
                    FROM [dbo].[TB_M_MENU] WHERE PARENT_ID IS NULL");
                return "M" + (seq + 1).ToString("00000");
            }
            else
            {
                int seq = ScalarInt(db, @"
                    SELECT ISNULL(MAX(CAST(SUBSTRING(MENU_ID, 8, 2) AS INT)), 0) AS Value
                    FROM [dbo].[TB_M_MENU] WHERE PARENT_ID = @PARENT_ID",
                    new SqlParameter("@PARENT_ID", data.PARENT_ID));
                return data.PARENT_ID + "-" + (seq + 1).ToString("00");
            }
        }

        public String GetMenuLastSeq(Menu data, DBContext db)
        {
            int seq = ScalarInt(db, @"
                SELECT ISNULL(MAX(MENU_SEQ), 0) AS Value
                FROM [dbo].[TB_M_MENU]
                WHERE (@PARENT_ID IS NULL AND PARENT_ID IS NULL) OR PARENT_ID = @PARENT_ID",
                new SqlParameter("@PARENT_ID", CheckNullValue(string.IsNullOrEmpty(data.PARENT_ID) ? null : data.PARENT_ID)));
            return (seq + 1).ToString();
        }

        private DBResult Validate(Menu data, DBContext db)
        {
            if (string.IsNullOrEmpty(data.MENU_ID)) return Fail("Menu ID should not be null");
            if (string.IsNullOrEmpty(data.MENU_NAME)) return Fail("Menu Name should not be null");
            if (string.IsNullOrEmpty(data.MENU_ICON)) return Fail("Menu Icon should not be null");
            if (string.IsNullOrEmpty(data.MENU_URL)) return Fail("Menu URL should not be null");
            if (data.MENU_SEQ == null) return Fail("Sequence should not be null");

            // Nomor urut harus unik di dalam parent yang sama
            bool seqExists = Exists(db, @"
                SELECT 1 FROM [dbo].[TB_M_MENU]
                WHERE ((@PARENT_ID IS NULL AND PARENT_ID IS NULL) OR PARENT_ID = @PARENT_ID)
                  AND MENU_SEQ = @MENU_SEQ AND MENU_ID <> @MENU_ID",
                new SqlParameter("@PARENT_ID", CheckNullValue(string.IsNullOrEmpty(data.PARENT_ID) ? null : data.PARENT_ID)),
                new SqlParameter("@MENU_SEQ", data.MENU_SEQ),
                new SqlParameter("@MENU_ID", data.MENU_ID));
            if (seqExists)
            {
                return Fail("Sequence No " + data.MENU_SEQ + " already exists" + (string.IsNullOrEmpty(data.PARENT_ID) ? "" : " in parent " + data.PARENT_ID));
            }

            return null;
        }

        private SqlParameter[] SaveParams(Menu data, string loginUser)
        {
            return new[]
            {
                new SqlParameter("@MENU_ID", data.MENU_ID),
                new SqlParameter("@PARENT_ID", CheckNullValue(string.IsNullOrEmpty(data.PARENT_ID) ? null : data.PARENT_ID)),
                new SqlParameter("@MENU_NAME", data.MENU_NAME),
                new SqlParameter("@MENU_ICON", data.MENU_ICON),
                new SqlParameter("@MENU_URL", data.MENU_URL),
                new SqlParameter("@MENU_SEQ", data.MENU_SEQ),
                new SqlParameter("@LOGIN_USER", loginUser)
            };
        }

        public DBResult Insert(Menu data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Menu", "Insert", "MenuRepo.Insert", loginUser, () =>
            {
                DBResult invalid = Validate(data, db);
                if (invalid != null) return invalid;

                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = @MENU_ID", new SqlParameter("@MENU_ID", data.MENU_ID)))
                {
                    return Fail("Data already exists, Menu ID = " + data.MENU_ID);
                }

                db.Database.ExecuteSqlRaw(@"
                    INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_DT, CREATED_BY)
                    VALUES (@MENU_ID, @PARENT_ID, @MENU_NAME, @MENU_ICON, @MENU_URL, @MENU_SEQ, 0, GETDATE(), @LOGIN_USER)",
                    SaveParams(data, loginUser));

                return Ok("Successfully saved data");
            });
        }

        public DBResult Update(Menu data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Menu", "Update", "MenuRepo.Update", loginUser, () =>
            {
                DBResult invalid = Validate(data, db);
                if (invalid != null) return invalid;

                db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_MENU]
                    SET PARENT_ID = @PARENT_ID, MENU_NAME = @MENU_NAME, MENU_ICON = @MENU_ICON, MENU_URL = @MENU_URL,
                        MENU_SEQ = @MENU_SEQ, CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER
                    WHERE MENU_ID = @MENU_ID",
                    SaveParams(data, loginUser));

                return Ok("Successfully saved data");
            });
        }

        public DBResult Delete(Menu data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Menu & Function - Menu", "Delete", "MenuRepo.Delete", loginUser, () =>
            {
                SqlParameter Id() => new SqlParameter("@MENU_ID", CheckNullValue(data.MENU_ID));

                // Cegah function/sub-menu/otorisasi yatim
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE MENU_ID = @MENU_ID", Id()))
                {
                    return Fail("Menu still has registered functions - delete or move those functions before deleting this menu");
                }
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_MENU] WHERE PARENT_ID = @MENU_ID", Id()))
                {
                    return Fail("Menu still has sub-menus - delete or move those sub-menus before deleting this menu");
                }
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] WHERE MENU_ID = @MENU_ID", Id()))
                {
                    return Fail("Menu is still assigned to one or more roles - deactivate this menu or remove its access in Role Authorization before deleting it");
                }

                db.Database.ExecuteSqlRaw("DELETE FROM [dbo].[TB_M_MENU] WHERE MENU_ID = @MENU_ID", Id());

                return Ok("Successfully deleted data");
            });
        }
    }
}
