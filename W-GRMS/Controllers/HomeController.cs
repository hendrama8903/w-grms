using GRMS.Common.Controllers;
using GRMS.Common.Repo;
using GRMS.Models;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Data;
using System.Diagnostics;

namespace GRMS.Controllers
{
    public class HomeController : BaseController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public HomeController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        private DashboardRepo dashboardRepo = DashboardRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth", new { param = "/Home/Index" });
            }

            ViewData["Title"] = "Dashboard";
            ViewData["Plants"] = ckbSources;
            // tombol cetak di popup detail mengikuti hak akses PrintController
            ViewData["CanPrintGrn"] = HasMenuAccess("/GoodsReceiving/Index");
            ViewData["CanPrintDn"] = HasMenuAccess("/GoodsReceiving/Index") || HasMenuAccess("/DeliveryNotePrinting/Index");

            return View();
        }

        // Ringkasan dashboard untuk satu plant CKB atau semua (plantCode kosong)
        public JsonResult GetDashboard(string plantCode, DateTime? asOf)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Json(new { status = false, message = "Session expired, please login again" });
            }

            try
            {
                List<string> plants = ckbSources.Select(x => x.PlantCode).ToList();
                if (!string.IsNullOrEmpty(plantCode))
                {
                    if (!plants.Contains(plantCode))
                    {
                        return Json(new { status = false, message = "Unknown plant code " + plantCode });
                    }
                    plants = new List<string> { plantCode };
                }

                DateTime day = (asOf ?? DateTime.Today).Date;

                DataTable summary = dashboardRepo.Summary(plants, day, db);

                return Json(new
                {
                    status = true,
                    asOf = day,
                    summary = BaseRepo.ToRows(summary).FirstOrDefault(),
                    trend = BaseRepo.ToRows(dashboardRepo.DailyTrend(plants, day, db)),
                    suppliers = BaseRepo.ToRows(dashboardRepo.OutstandingBySupplier(plants, day, db))
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Detail saat kartu KPI / batang grafik diklik.
        // type: grn_today, grn_month, grn_day (day = tanggal batang), due, overdue, delayed, supplier (supplier = kode)
        public JsonResult GetDashboardDetail(string type, string plantCode, DateTime? asOf, DateTime? day, string supplier)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Json(new { status = false, message = "Session expired, please login again" });
            }

            try
            {
                List<string> plants = ckbSources.Select(x => x.PlantCode).ToList();
                if (!string.IsNullOrEmpty(plantCode))
                {
                    if (!plants.Contains(plantCode))
                    {
                        return Json(new { status = false, message = "Unknown plant code " + plantCode });
                    }
                    plants = new List<string> { plantCode };
                }

                DateTime refDay = (asOf ?? DateTime.Today).Date;
                DataTable dt;
                switch (type)
                {
                    case "grn_today":
                        dt = dashboardRepo.GrnList(plants, refDay, refDay, db);
                        break;
                    case "grn_month":
                        dt = dashboardRepo.GrnList(plants, new DateTime(refDay.Year, refDay.Month, 1), refDay, db);
                        break;
                    case "grn_day":
                        if (day == null) return Json(new { status = false, message = "Please select a day" });
                        dt = dashboardRepo.GrnList(plants, day.Value, day.Value, db);
                        break;
                    case "due":
                    case "overdue":
                    case "delayed":
                    case "supplier":
                        dt = dashboardRepo.DnList(type, plants, refDay, supplier, db);
                        break;
                    default:
                        return Json(new { status = false, message = "Unknown detail type " + type });
                }

                return Json(new { status = true, data = BaseRepo.ToRows(dt) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            ViewData["Title"] = "Error";
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
