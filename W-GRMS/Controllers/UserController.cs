using GRMS.Common.Controllers;
using GRMS.Common.Models;
using GRMS.Models;
using GRMS.Models.DB;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;

namespace GRMS.Controllers
{
    public class UserController : BaseController
    {
        DBContext db;
        public UserController(DBContext db)
        {
            this.db = db;
        }

        private UserRepo userRepo = UserRepo.Instance;
        private RoleRepo roleRepo = RoleRepo.Instance;

        private const string MENU_URL = "/User/Index";

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else if (!HasMenuAccess(MENU_URL))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            ViewData["Add"] = HasFunction("USER-ADD");
            ViewData["Edit"] = HasFunction("USER-EDIT");
            ViewData["Delete"] = HasFunction("USER-DELETE");
            ViewData["Restore"] = HasFunction("USER-RESTORE");

            ViewData["Title"] = "User Management";

            return View();
        }

        public ActionResult Search(User data, bool showDeleted = false)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                var userData = userRepo.Search(data, db, null, null, showDeleted);
                return Json(new { status = true, data = userData });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetByKey(User data)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                UserListItem result = userRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Pilihan dropdown Role & Plant di form user
        public JsonResult GetFormOptions()
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                var roles = roleRepo.Search(new Role(), db, null, null)
                    .Select(x => new { id = x.ROLE_ID, text = x.ROLE_NAME });
                // pilihan Receiving Area dari master, mis. "1E - Engine Line (Plant 1)"
                var areas = ReceivingAreaRepo.Instance.Search(db).AsEnumerable().Select(r =>
                {
                    string code = r["AREA_CODE"].ToString();
                    string name = r["AREA_NAME"] as string;
                    string plant = r["PLANT_CODE"] as string;
                    string text = code + (string.IsNullOrEmpty(name) ? "" : " - " + name) + (string.IsNullOrEmpty(plant) ? "" : " (Plant " + plant + ")");
                    return new { id = code, text };
                });
                return Json(new { status = true, roles, areas });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult AddEdit(string screenMode, User data)
        {
            try
            {
                if (screenMode == "ADD")
                {
                    if (!HasFunction("USER-ADD"))
                    {
                        return Json(new { status = false, message = "Access denied" });
                    }

                    // Sama dengan desktop: kalau password dikosongkan, password awal = User ID
                    string password = string.IsNullOrEmpty(data.PASSWORD) ? data.USER_ID : data.PASSWORD;
                    if (!string.IsNullOrEmpty(data.PASSWORD) && data.PASSWORD != data.CONFIRM_PASSWORD)
                    {
                        return Json(new { status = false, message = "Confirm password does not match" });
                    }

                    return Json(userRepo.Insert(data, HashPassword(password), GetLoginUsername(), db));
                }
                else
                {
                    if (!HasFunction("USER-EDIT"))
                    {
                        return Json(new { status = false, message = "Access denied" });
                    }

                    // Password hanya di-reset kalau diisi
                    string passwordHash = null;
                    if (!string.IsNullOrEmpty(data.PASSWORD))
                    {
                        if (data.PASSWORD != data.CONFIRM_PASSWORD)
                        {
                            return Json(new { status = false, message = "Confirm password does not match" });
                        }
                        passwordHash = HashPassword(data.PASSWORD);
                    }

                    return Json(userRepo.Update(data, passwordHash, GetLoginUsername(), db));
                }
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Delete(User data)
        {
            if (!HasFunction("USER-DELETE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                DBResult result = userRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Restore(User data)
        {
            if (!HasFunction("USER-RESTORE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                DBResult result = userRepo.Restore(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel()
        {
            if (!HasMenuAccess(MENU_URL))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            IList<UserListItem> listData = userRepo.Search(new User(), db, null, null, true);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("User Management");

            string[] headers = { "No", "User ID", "Name", "NIK", "Department", "Email", "Role", "Receiving Areas", "Password Expired", "Status" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            foreach (UserListItem item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(rowIndex);
                row.CreateCell(1).SetCellValue(item.USER_ID);
                row.CreateCell(2).SetCellValue(item.USER_NAME);
                row.CreateCell(3).SetCellValue(item.NIK);
                row.CreateCell(4).SetCellValue(item.DEPT);
                row.CreateCell(5).SetCellValue(item.EMAIL);
                row.CreateCell(6).SetCellValue(item.ROLE_NAME);
                row.CreateCell(7).SetCellValue(item.PLANTS);
                row.CreateCell(8).SetCellValue(item.EXP_PWD?.ToString("yyyy-MM-dd"));
                row.CreateCell(9).SetCellValue(item.DELETE_FLAG == "1" ? "Inactive" : "Active");
                rowIndex++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                workbook.Write(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "USER-MANAGEMENT-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
