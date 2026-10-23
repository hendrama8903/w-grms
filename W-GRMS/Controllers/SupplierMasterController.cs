using GRMS.Common.Controllers;
using GRMS.Models;
using GRMS.Models.DB;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GRMS.Controllers
{
    // Konversi frmSupplierMasterCKB (desktop): lihat supplier, update dari semua database CKB, hapus supplier.
    public class SupplierMasterController : BaseController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public SupplierMasterController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        private MasterDataRepo masterDataRepo = MasterDataRepo.Instance;

        private const string MENU_URL = "/SupplierMaster/Index";

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

            ViewData["Sync"] = HasFunction("SUPPLIER-SYNC");
            ViewData["Delete"] = HasFunction("SUPPLIER-DELETE");

            ViewData["Title"] = "Supplier Master (CKB)";

            return View();
        }

        public JsonResult Search()
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                var data = masterDataRepo.SearchSuppliers(db);
                return Json(new { status = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Sync()
        {
            if (!HasFunction("SUPPLIER-SYNC"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(masterDataRepo.SyncSuppliers(ckbSources, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Delete(SupplierMaster data)
        {
            if (!HasFunction("SUPPLIER-DELETE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(masterDataRepo.DeleteSupplier(data, GetLoginUsername(), db));
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

            IList<SupplierMaster> listData = masterDataRepo.SearchSuppliers(db);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Supplier Master CKB");

            string[] headers = { "Supplier Code", "Supplier Plant Code", "Supplier Shipping Dock", "Supplier Name" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            foreach (SupplierMaster item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                row.CreateCell(0).SetCellValue(item.SUPSCD);
                row.CreateCell(1).SetCellValue(item.SUPPCD);
                row.CreateCell(2).SetCellValue(item.SUPSHD);
                row.CreateCell(3).SetCellValue(item.SUPNAM);
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
                "SUPPLIER-MASTER-CKB-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
