using GRMS.Common.Controllers;
using GRMS.Common.Models;
using GRMS.Common.Repo;
using GRMS.Models;
using GRMS.Models.DB;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;

namespace GRMS.Controllers
{
    // Konversi frmGetGoodsReceiveCKB (desktop "Order Data (CKB)"): lihat order per plant & tanggal,
    // download order baru dari database CKB plant, hapus order yang belum diterima.
    public class OrderDataController : BaseController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        IServiceScopeFactory scopeFactory;
        public OrderDataController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources, IServiceScopeFactory scopeFactory)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
            this.scopeFactory = scopeFactory;
        }

        private TransactionRepo transactionRepo = TransactionRepo.Instance;

        private const string MENU_URL = "/OrderData/Index";

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

            ViewData["Download"] = HasFunction("ORDER-DOWNLOAD");
            ViewData["Delete"] = HasFunction("ORDER-DELETE");
            ViewData["Plants"] = ckbSources;

            ViewData["Title"] = "Order Data (CKB)";

            return View();
        }

        public JsonResult Search(string plantCode, DateTime? from, DateTime? to)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }
            if (from == null || to == null)
            {
                return Json(new { status = false, message = "Please fill the period (from - to)" });
            }

            try
            {
                DataTable dt = transactionRepo.SearchOrders(plantCode, from.Value, to.Value, db);
                return Json(new { status = true, data = BaseRepo.ToRows(dt) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Download berjalan di background (bisa lama: baca CKB lewat linked server + simpan per baris),
        // layar memantau lewat DownloadProgress. Satu download per plant sekaligus supaya tidak dobel.
        private static readonly Dictionary<string, OrderDownloadProgress> downloads = new Dictionary<string, OrderDownloadProgress>();
        private static readonly object downloadsLock = new object();

        [HttpPost]
        public JsonResult Download(string plantCode)
        {
            if (!HasFunction("ORDER-DOWNLOAD"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            CkbSourceOptions plant = ckbSources.FirstOrDefault(x => x.PlantCode == plantCode);
            if (plant == null)
            {
                return Json(new { status = false, message = "Unknown plant code " + plantCode });
            }

            OrderDownloadProgress job;
            lock (downloadsLock)
            {
                job = downloads.Values.FirstOrDefault(x => x.PlantCode == plant.PlantCode && x.Stage != "done");
                if (job != null)
                {
                    return Json(new { status = true, jobId = job.JobId, running = true, user = job.UserName });
                }

                // buang status download lama yang sudah selesai
                foreach (var old in downloads.Values.Where(x => x.EndTime < DateTime.Now.AddHours(-1)).ToList())
                {
                    downloads.Remove(old.JobId);
                }

                job = new OrderDownloadProgress { PlantCode = plant.PlantCode, PlantName = plant.PlantName, UserName = GetLoginUsername() };
                downloads[job.JobId] = job;
            }

            // DBContext request ini sudah dibuang saat request selesai, jadi background memakai scope sendiri
            Task.Run(() =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var jobDb = scope.ServiceProvider.GetRequiredService<DBContext>();
                    DBResult result = transactionRepo.DownloadOrders(plant, job.UserName, jobDb, job);
                    job.Status = result.status;
                    job.Message = result.message;
                }
                catch (Exception ex)
                {
                    job.Status = false;
                    job.Message = ex.Message;
                }
                finally
                {
                    job.EndTime = DateTime.Now;
                    job.Stage = "done";
                }
            });

            return Json(new { status = true, jobId = job.JobId, running = false });
        }

        public JsonResult DownloadProgress(string jobId)
        {
            if (!HasFunction("ORDER-DOWNLOAD"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            OrderDownloadProgress job;
            lock (downloadsLock)
            {
                downloads.TryGetValue(jobId ?? "", out job);
            }
            if (job == null)
            {
                return Json(new { status = false, message = "Download process not found" });
            }

            return Json(new
            {
                status = true,
                stage = job.Stage,
                plant = job.PlantCode + " - " + job.PlantName,
                total = job.Total,
                inserted = job.Inserted,
                failed = job.Failed,
                elapsed = (int)((job.EndTime ?? DateTime.Now) - job.StartTime).TotalSeconds,
                result = job.Status,
                message = job.Message
            });
        }

        [HttpPost]
        public JsonResult Delete(string SOCDNO, string SOCPOD, string SOCPOT)
        {
            if (!HasFunction("ORDER-DELETE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(transactionRepo.DeleteOrder(SOCDNO, SOCPOD, SOCPOT, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel(string plantCode, DateTime? from, DateTime? to)
        {
            if (!HasMenuAccess(MENU_URL) || from == null || to == null)
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            DataTable dt = transactionRepo.SearchOrders(plantCode, from.Value, to.Value, db);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Order Data CKB");

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
                "ORDER-DATA-CKB-PLANT" + plantCode + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
