using GRMS.Common.Models;
using GRMS.Common.Repo;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GRMS.Models.Repo
{
    // Master Receiving Area (TB_M_RECEIVING_AREA): kode area receive CKB (RACRAC) yang dipilih
    // sebagai akses user di User Management (disimpan ke TB_M_USER_PLANT.PLANTCODE).
    public class ReceivingAreaRepo : BaseRepo
    {
        private ReceivingAreaRepo() { }

        #region Singleton
        private static ReceivingAreaRepo instance = null;
        public static ReceivingAreaRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ReceivingAreaRepo();
                }
                return instance;
            }
        }
        #endregion

        public DataTable Search(DBContext db)
        {
            return QueryTable(db, @"
                SELECT A.AREA_CODE, A.AREA_NAME, A.PLANT_CODE,
                       (SELECT COUNT(*) FROM [dbo].[TB_M_USER_PLANT] U WHERE U.PLANTCODE = A.AREA_CODE) AS USER_COUNT,
                       A.CREATED_BY, A.CREATED_DT, A.CHANGED_BY, A.CHANGED_DT
                FROM [dbo].[TB_M_RECEIVING_AREA] A
                ORDER BY A.AREA_CODE");
        }

        public bool Exists(string areaCode, DBContext db)
        {
            return Exists(db, "SELECT 1 FROM [dbo].[TB_M_RECEIVING_AREA] WHERE AREA_CODE = @AREA_CODE",
                VarChar("@AREA_CODE", CheckNullValue(areaCode)));
        }

        public DBResult Save(string screenMode, string areaCode, string areaName, string plantCode, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Receiving Area", screenMode == "ADD" ? "Insert" : "Update", "ReceivingAreaRepo.Save", loginUser, () =>
            {
                areaCode = (areaCode ?? "").Trim().ToUpper();
                if (areaCode == "") return Fail("Area Code should not be empty");
                if (areaCode.Length > 4) return Fail("Area Code maximum is 4 characters");
                if (areaCode.Contains(',')) return Fail("Area Code cannot contain a comma");

                var param = new[]
                {
                    VarChar("@AREA_CODE", areaCode),
                    VarChar("@AREA_NAME", CheckNullValue(areaName?.Trim())),
                    VarChar("@PLANT_CODE", CheckNullValue(plantCode)),
                    VarChar("@LOGIN_USER", loginUser)
                };

                if (screenMode == "ADD")
                {
                    if (Exists(areaCode, db)) return Fail("Area Code " + areaCode + " already exists");

                    db.Database.ExecuteSqlRaw(@"
                        INSERT INTO [dbo].[TB_M_RECEIVING_AREA] (AREA_CODE, AREA_NAME, PLANT_CODE, CREATED_BY, CREATED_DT)
                        VALUES (@AREA_CODE, @AREA_NAME, @PLANT_CODE, @LOGIN_USER, GETDATE())", param);
                    return Ok("Successfully saved receiving area " + areaCode);
                }

                int rows = db.Database.ExecuteSqlRaw(@"
                    UPDATE [dbo].[TB_M_RECEIVING_AREA]
                    SET AREA_NAME = @AREA_NAME, PLANT_CODE = @PLANT_CODE, CHANGED_BY = @LOGIN_USER, CHANGED_DT = GETDATE()
                    WHERE AREA_CODE = @AREA_CODE", param);
                if (rows == 0) return Fail("Data not found");

                return Ok("Successfully updated receiving area " + areaCode);
            });
        }

        // Area yang masih dipakai user tidak boleh dihapus (akses user di desktop ikut hilang)
        public DBResult Delete(string areaCode, string loginUser, DBContext db)
        {
            return RunWithLog(db, "Receiving Area", "Delete", "ReceivingAreaRepo.Delete", loginUser, () =>
            {
                int users = ScalarInt(db, "SELECT COUNT(*) AS Value FROM [dbo].[TB_M_USER_PLANT] WHERE PLANTCODE = @AREA_CODE",
                    VarChar("@AREA_CODE", CheckNullValue(areaCode)));
                if (users > 0)
                {
                    return Fail("Area Code " + areaCode + " is still assigned to " + users + " user(s) - remove it from those users first");
                }

                int rows = db.Database.ExecuteSqlRaw("DELETE FROM [dbo].[TB_M_RECEIVING_AREA] WHERE AREA_CODE = @AREA_CODE",
                    VarChar("@AREA_CODE", CheckNullValue(areaCode)));
                if (rows == 0) return Fail("Data not found");

                return Ok("Successfully deleted receiving area " + areaCode);
            });
        }
    }
}
