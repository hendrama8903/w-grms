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
    // Konversi frmPartsMasterCKB (desktop): lihat part per plant CKB, update dari database CKB, hapus part.
    public class PartsMasterController : BaseController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public PartsMasterController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        private MasterDataRepo masterDataRepo = MasterDataRepo.Instance;

        private const string MENU_URL = "/PartsMaster/Index";

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

            ViewData["Sync"] = HasFunction("PARTS-SYNC");
            ViewData["Delete"] = HasFunction("PARTS-DELETE");
            ViewData["Plants"] = ckbSources;

            ViewData["Title"] = "Parts Master (CKB)";

            return View();
        }

        public JsonResult Search(string plantCode)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                var data = masterDataRepo.SearchParts(plantCode, db);
                return Json(new { status = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Sync(string plantCode)
        {
            if (!HasFunction("PARTS-SYNC"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                CkbSourceOptions plant = ckbSources.FirstOrDefault(x => x.PlantCode == plantCode);
                if (plant == null)
                {
                    return Json(new { status = false, message = "Unknown plant code " + plantCode });
                }

                return Json(masterDataRepo.SyncParts(plant, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Delete(PartsMaster data)
        {
            if (!HasFunction("PARTS-DELETE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(masterDataRepo.DeletePart(data, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel(string plantCode)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            IList<PartsMaster> listData = masterDataRepo.SearchParts(plantCode, db);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Parts Master CKB");

            string[] headers = { "Supplier Code", "Supplier Plant Code", "Supplier Shipping Dock", "Plant Code", "Receiving Area Code",
                                 "Part Number", "Pcs/Kbn", "Part Name", "Unique No", "Box Spec" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            foreach (PartsMaster item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                row.CreateCell(0).SetCellValue(item.SUPSCD);
                row.CreateCell(1).SetCellValue(item.SUPPCD);
                row.CreateCell(2).SetCellValue(item.SUPSHD);
                row.CreateCell(3).SetCellValue(item.PLCPCD);
                row.CreateCell(4).SetCellValue(item.RACRAC);
                row.CreateCell(5).SetCellValue(item.PAMPNO);
                row.CreateCell(6).SetCellValue(item.PAMPCK ?? 0);
                row.CreateCell(7).SetCellValue(item.PAMPNM);
                row.CreateCell(8).SetCellValue(item.PAMUNO);
                row.CreateCell(9).SetCellValue(item.PAMBSC);
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
                "PARTS-MASTER-CKB-PLANT" + plantCode + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
