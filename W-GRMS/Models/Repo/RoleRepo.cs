using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    public class RoleRepo : BaseRepo
    {
        private RoleRepo() { }

        #region Singleton
        private static RoleRepo instance = null;
        public static RoleRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RoleRepo();
                }
                return instance;
            }
        }
        #endregion

        // Daftar role + jumlah user aktif & jumlah izin (menu + function)
        private string SearchQuery(Role data, List<SqlParameter> param, int? PageNumber, int? PageSize)
        {
            param.Add(new SqlParameter("@ROLE_ID", LikeValue(data.ROLE_ID)));
            param.Add(new SqlParameter("@ROLE_NAME", LikeValue(data.ROLE_NAME)));

            string select = @"
                SELECT ROW_NUMBER() OVER (ORDER BY G.ROLE_ID ASC) AS RowNumber,
                    G.ROLE_ID, G.ROLE_NAME, G.ROLE_DESC, G.CREATED_DT, G.CREATED_BY, G.CHANGED_DT, G.CHANGED_BY,
                    ISNULL(UC.USERS_COUNT, 0) AS USERS_COUNT,
                    ISNULL(AM.MENU_COUNT, 0) + ISNULL(AF.FUNCTION_COUNT, 0) AS PERMISSIONS_COUNT
                FROM [dbo].[TB_M_ROLE] G
                LEFT JOIN (SELECT ROLE_ID, COUNT(*) AS USERS_COUNT FROM [dbo].[TB_M_USER]
                           WHERE ISNULL(DELETE_FLAG, '0') <> '1' GROUP BY ROLE_ID) UC ON UC.ROLE_ID = G.ROLE_ID
                LEFT JOIN (SELECT ROLE_ID, COUNT(*) AS MENU_COUNT FROM [dbo].[TB_M_AUTH_MENU] GROUP BY ROLE_ID) AM ON AM.ROLE_ID = G.ROLE_ID
                LEFT JOIN (SELECT ROLE_ID, COUNT(*) AS FUNCTION_COUNT FROM [dbo].[TB_M_AUTH_FUNCTION] GROUP BY ROLE_ID) AF ON AF.ROLE_ID = G.ROLE_ID
                WHERE (@ROLE_ID IS NULL OR G.ROLE_ID LIKE @ROLE_ID)
                  AND (@ROLE_NAME IS NULL OR G.ROLE_NAME LIKE @ROLE_NAME)";

            return Paged(select, param, PageNumber, PageSize);
        }

        public IList<Role> Search(Role data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>();
            string query = SearchQuery(data, param, PageNumber, PageSize);
            return db.Role.FromSqlRaw<Role>(query, param.ToArray()).AsNoTracking().ToList();
        }

        public IList<RoleListItem> SearchGrid(Role data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>();
            string query = SearchQuery(data, param, PageNumber, PageSize);
            return db.RoleListItem.FromSqlRaw<RoleListItem>(query, param.ToArray()).ToList();
        }

        public Role GetByKey(Role data, DBContext db)
        {
            string query = @"
                SELECT ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_DT, CREATED_BY, CHANGED_BY, CHANGED_DT
                FROM [dbo].[TB_M_ROLE]
                WHERE ROLE_ID = @ROLE_ID";

            return db.Role.FromSqlRaw<Role>(query, new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)))
                .AsNoTracking().AsEnumerable().FirstOrDefault();
        }

        public DBResult Insert(Role data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Role Authorization - Role", "Insert", "RoleRepo.Insert", loginUser, () =>
            {
                if (string.IsNullOrEmpty(data.ROLE_ID)) return Fail("Role ID should not be null");
                if (string.IsNullOrEmpty(data.ROLE_NAME)) return Fail("Role Name should not be null");
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_ROLE] WHERE ROLE_ID = @ROLE_ID", new SqlParameter("@ROLE_ID", data.ROLE_ID)))
                {
                    return Fail("Data already exists, Role ID = " + data.ROLE_ID);
                }

                db.Database.ExecuteSqlRaw(@"
                    INSERT INTO [dbo].[TB_M_ROLE] (ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_DT, CREATED_BY)
                    VALUES (@ROLE_ID, @ROLE_NAME, @ROLE_DESC, GETDATE(), @LOGIN_USER)",
                    new SqlParameter("@ROLE_ID", data.ROLE_ID),
                    new SqlParameter("@ROLE_NAME", data.ROLE_NAME),
                    new SqlParameter("@ROLE_DESC", CheckNullValue(data.ROLE_DESC)),
                    new SqlParameter("@LOGIN_USER", loginUser));

                return Ok("Successfully saved data");
            });
        }

        public DBResult Update(Role data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Role Authorization - Role", "Update", "RoleRepo.Update", loginUser, () =>
            {
                if (string.IsNullOrEmpty(data.ROLE_NAME)) return Fail("Role Name should not be null");

                db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_ROLE]
                    SET ROLE_NAME = @ROLE_NAME, ROLE_DESC = @ROLE_DESC, CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER
                    WHERE ROLE_ID = @ROLE_ID",
                    new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)),
                    new SqlParameter("@ROLE_NAME", data.ROLE_NAME),
                    new SqlParameter("@ROLE_DESC", CheckNullValue(data.ROLE_DESC)),
                    new SqlParameter("@LOGIN_USER", loginUser));

                return Ok("Successfully saved data");
            });
        }

        public DBResult Delete(Role data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Role Authorization - Role", "Delete", "RoleRepo.Delete", loginUser, () =>
            {
                // Role yang masih dipakai user tidak boleh dihapus (user tsb tidak akan bisa login)
                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_USER] WHERE ROLE_ID = @ROLE_ID", new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID))))
                {
                    return Fail("Role is still assigned to one or more users");
                }

                db.Database.ExecuteSqlRaw(@"
                    DELETE FROM [dbo].[TB_M_AUTH_MENU] WHERE ROLE_ID = @ROLE_ID;
                    DELETE FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE ROLE_ID = @ROLE_ID;
                    DELETE FROM [dbo].[TB_M_ROLE] WHERE ROLE_ID = @ROLE_ID;",
                    new SqlParameter("@ROLE_ID", CheckNullValue(data.ROLE_ID)));

                return Ok("Successfully deleted data");
            });
        }
    }
}
