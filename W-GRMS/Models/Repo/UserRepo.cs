using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models.Repo
{
    // User Management di atas TB_M_USER & TB_M_USER_PLANT milik GRMS (dipakai bersama desktop).
    // Password diterima sudah dalam bentuk hash (lihat BaseController.HashPassword).
    public class UserRepo : BaseRepo
    {
        private UserRepo() { }

        #region Singleton
        private static UserRepo instance = null;
        public static UserRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new UserRepo();
                }
                return instance;
            }
        }
        #endregion

        private const string COLUMNS = @"
            A.USER_ID, A.USER_NAME, A.DEPT, A.NIK, A.EXP_PWD, A.EMAIL, A.PHONE, A.ROLE_ID, R.ROLE_NAME,
            ISNULL(A.DELETE_FLAG, '0') AS DELETE_FLAG,
            STUFF((SELECT ', ' + P.PLANTCODE FROM [dbo].[TB_M_USER_PLANT] P
                   WHERE P.USER_ID = A.USER_ID ORDER BY P.PLANTCODE
                   FOR XML PATH('')), 1, 2, '') AS PLANTS,
            A.CREATED_DT, A.CREATED_BY, A.CHANGED_DT, A.CHANGED_BY";

        private const string FROM = @"
            FROM [dbo].[TB_M_USER] A
            LEFT JOIN [dbo].[TB_M_ROLE] R ON R.ROLE_ID = A.ROLE_ID";

        public IList<UserListItem> Search(User data, DBContext db, int? PageNumber, int? PageSize, bool showDeleted = false)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USER_ID", LikeValue(data.USER_ID) ),
                new SqlParameter ( "@USER_NAME", LikeValue(data.USER_NAME) ),
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@SHOW_DELETED", showDeleted )
            };

            string select = "SELECT ROW_NUMBER() OVER (ORDER BY A.USER_ID ASC) AS RowNumber, " + COLUMNS + FROM + @"
                WHERE (@USER_ID IS NULL OR A.USER_ID LIKE @USER_ID)
                  AND (@USER_NAME IS NULL OR A.USER_NAME LIKE @USER_NAME)
                  AND (@ROLE_ID IS NULL OR A.ROLE_ID = @ROLE_ID)
                  AND (@SHOW_DELETED = 1 OR ISNULL(A.DELETE_FLAG, '0') <> '1')";

            string query = Paged(select, param, PageNumber, PageSize);
            return db.UserListItem.FromSqlRaw<UserListItem>(query, param.ToArray()).ToList();
        }

        public UserListItem GetByKey(User data, DBContext db)
        {
            string query = "SELECT " + COLUMNS + FROM + " WHERE A.USER_ID = @USER_ID";
            return db.UserListItem.FromSqlRaw<UserListItem>(query, new SqlParameter("@USER_ID", CheckNullValue(data.USER_ID)))
                .AsEnumerable().FirstOrDefault();
        }

        public DBResult Insert(User data, string passwordHash, string loginUser, DBContext db)
        {
            return RunWithLog(db, "User Management", "Insert", "UserRepo.Insert", loginUser, () =>
            {
                DBResult invalid = Validate(data);
                if (invalid != null) return invalid;
                if (string.IsNullOrEmpty(data.USER_ID)) return Fail("User ID should not be null");

                if (Exists(db, "SELECT 1 FROM [dbo].[TB_M_USER] WHERE USER_ID = @USER_ID", new SqlParameter("@USER_ID", data.USER_ID)))
                {
                    return Fail("User ID " + data.USER_ID + " already exists");
                }

                using var trx = db.Database.BeginTransaction();

                db.Database.ExecuteSqlRaw(@"
                    INSERT INTO [dbo].[TB_M_USER] (USER_ID, USER_NAME, USER_PWD, DEPT, NIK, EXP_PWD,
                        EMAIL, PHONE, ROLE_ID, DELETE_FLAG, CREATED_BY, CREATED_DT)
                    VALUES (@USER_ID, @USER_NAME, @USER_PWD, @DEPT, @NIK, DATEADD(MONTH, 3, GETDATE()),
                        @EMAIL, @PHONE, @ROLE_ID, '0', @LOGIN_USER, GETDATE())",
                    SaveParams(data, passwordHash, loginUser));

                SavePlants(data, db);
                trx.Commit();

                return Ok("Successfully saved data");
            });
        }

        // passwordHash null = password tidak diubah
        public DBResult Update(User data, string passwordHash, string loginUser, DBContext db)
        {
            return RunWithLog(db, "User Management", "Update", "UserRepo.Update", loginUser, () =>
            {
                DBResult invalid = Validate(data);
                if (invalid != null) return invalid;

                using var trx = db.Database.BeginTransaction();

                db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_USER]
                    SET USER_NAME = @USER_NAME,
                        USER_PWD = ISNULL(@USER_PWD, USER_PWD),
                        EXP_PWD = CASE WHEN @USER_PWD IS NULL THEN EXP_PWD ELSE DATEADD(MONTH, 3, GETDATE()) END,
                        DEPT = @DEPT, NIK = @NIK, EMAIL = @EMAIL, PHONE = @PHONE, ROLE_ID = @ROLE_ID,
                        CHANGED_BY = @LOGIN_USER, CHANGED_DT = GETDATE()
                    WHERE USER_ID = @USER_ID",
                    SaveParams(data, passwordHash, loginUser));

                SavePlants(data, db);
                trx.Commit();

                return Ok("Successfully saved data");
            });
        }

        private DBResult Validate(User data)
        {
            if (string.IsNullOrEmpty(data.USER_NAME)) return Fail("User Name should not be null");
            if (string.IsNullOrEmpty(data.ROLE_ID)) return Fail("Role should not be null");
            return null;
        }

        private SqlParameter[] SaveParams(User data, string passwordHash, string loginUser)
        {
            return new[]
            {
                new SqlParameter("@USER_ID", CheckNullValue(data.USER_ID)),
                new SqlParameter("@USER_NAME", data.USER_NAME),
                new SqlParameter("@USER_PWD", CheckNullValue(passwordHash)),
                new SqlParameter("@DEPT", CheckNullValue(data.DEPT)),
                new SqlParameter("@NIK", CheckNullValue(data.NIK)),
                new SqlParameter("@EMAIL", CheckNullValue(data.EMAIL)),
                new SqlParameter("@PHONE", CheckNullValue(data.PHONE)),
                new SqlParameter("@ROLE_ID", data.ROLE_ID),
                new SqlParameter("@LOGIN_USER", loginUser)
            };
        }

        // Ganti seluruh plant user dengan daftar di PLANTS (dipisah koma)
        private void SavePlants(User data, DBContext db)
        {
            db.Database.ExecuteSqlRaw("DELETE FROM [dbo].[TB_M_USER_PLANT] WHERE USER_ID = @USER_ID",
                new SqlParameter("@USER_ID", data.USER_ID));

            IEnumerable<string> plants = (data.PLANTS ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string plant in plants)
            {
                db.Database.ExecuteSqlRaw("INSERT INTO [dbo].[TB_M_USER_PLANT] (USER_ID, PLANTCODE) VALUES (@USER_ID, @PLANTCODE)",
                    new SqlParameter("@USER_ID", data.USER_ID),
                    new SqlParameter("@PLANTCODE", plant));
            }
        }

        // Ganti password sendiri (termasuk saat password expired)
        public DBResult UpdatePassword(string userId, string oldPasswordHash, string newPasswordHash, string loginUser, DBContext db)
        {
            return RunWithLog(db, "User Management", "Change Password", "UserRepo.UpdatePassword", loginUser, () =>
            {
                if (!Exists(db, "SELECT 1 FROM [dbo].[TB_M_USER] WHERE USER_ID = @USER_ID AND USER_PWD = @OLD_PASSWORD",
                    new SqlParameter("@USER_ID", CheckNullValue(userId)),
                    new SqlParameter("@OLD_PASSWORD", CheckNullValue(oldPasswordHash))))
                {
                    return Fail("Old password is incorrect");
                }
                if (oldPasswordHash == newPasswordHash) return Fail("New password must be different from the old password");

                db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_USER]
                    SET USER_PWD = @USER_PWD, EXP_PWD = DATEADD(MONTH, 3, GETDATE()),
                        CHANGED_BY = @LOGIN_USER, CHANGED_DT = GETDATE()
                    WHERE USER_ID = @USER_ID",
                    new SqlParameter("@USER_ID", userId),
                    new SqlParameter("@USER_PWD", newPasswordHash),
                    new SqlParameter("@LOGIN_USER", loginUser));

                return Ok("Successfully changed password");
            });
        }

        // Soft delete supaya histori transaksi yang menunjuk user tetap utuh
        public DBResult Delete(User data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "User Management", "Delete", "UserRepo.Delete", loginUser, () =>
            {
                if (data.USER_ID == loginUser) return Fail("You cannot delete your own account");
                SetDeleteFlag(data.USER_ID, "1", loginUser, db);
                return Ok("Successfully deleted data");
            });
        }

        public DBResult Restore(User data, string loginUser, DBContext db)
        {
            return RunWithLog(db, "User Management", "Restore", "UserRepo.Restore", loginUser, () =>
            {
                SetDeleteFlag(data.USER_ID, "0", loginUser, db);
                return Ok("Successfully restored data");
            });
        }

        private void SetDeleteFlag(string userId, string flag, string loginUser, DBContext db)
        {
            db.Database.ExecuteSqlRaw(@"
                UPDATE [dbo].[TB_M_USER]
                SET DELETE_FLAG = @FLAG, CHANGED_BY = @LOGIN_USER, CHANGED_DT = GETDATE()
                WHERE USER_ID = @USER_ID",
                new SqlParameter("@USER_ID", CheckNullValue(userId)),
                new SqlParameter("@FLAG", flag),
                new SqlParameter("@LOGIN_USER", loginUser));
        }
    }
}
