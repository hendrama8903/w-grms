using GRMS.Common.Controllers;
using GRMS.Common.Repo;
using GRMS.Models;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Text.RegularExpressions;

namespace GRMS.Controllers
{
    // Konversi olksoc_y230 (desktop "Data SOC (CKB)"): tampilkan OLKSOC dari database CKB plant per periode.
    public class DataSocController : BaseController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public DataSocController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        private TransactionRepo transactionRepo = TransactionRepo.Instance;

        private const string MENU_URL = "/DataSoc/Index";

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login?param=" + MENU_URL);
            }
            else if (!HasMenuAccess(MENU_URL))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            ViewData["Plants"] = ckbSources.Where(x => !string.IsNullOrEmpty(x.PartSource)).ToList();

            ViewData["Title"] = "Data SOC (CKB)";

            return View();
        }

        private DataTable Load(string plantCode, string period, out string error)
        {
            error = null;
            CkbSourceOptions plant = ckbSources.FirstOrDefault(x => x.PlantCode == plantCode);
            if (plant == null) { error = "Please select plant code"; return null; }
            if (period == null || !Regex.IsMatch(period, @"^\d{6}$")) { error = "Period format must be YYYYMM"; return null; }

            return transactionRepo.GetSoc(plant, period, db);
        }

        public JsonResult Search(string plantCode, string period)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                DataTable dt = Load(plantCode, period, out string error);
                if (error != null) return Json(new { status = false, message = error });

                return Json(new { status = true, data = BaseRepo.ToRows(dt) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel(string plantCode, string period)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            DataTable dt = Load(plantCode, period, out string error);
            if (error != null) return Content(error);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Data SOC");

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < dt.Columns.Count; col++)
            {
                headerRow.CreateCell(col).SetCellValue(dt.Columns[col].ColumnName);
            }

            int rowIndex = 1;
            foreach (DataRow dr in dt.Rows)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                for (int col = 0; col < dt.Columns.Count; col++)
                {
                    row.CreateCell(col).SetCellValue(dr[col]?.ToString());
                }
            }

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                workbook.Write(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DATA-SOC-PLANT" + plantCode + "-" + period + ".xlsx");
        }
    }
}
