using GRMS.Common.Controllers;
using GRMS.Common.Repo;
using GRMS.Models;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace GRMS.Controllers
{
    // Konversi frmDeliveryNotePrinting (desktop "Delivery Note Printing (CKB)"): cari DN sisa (DN#n) lalu cetak.
    public class DeliveryNotePrintingController : BaseController
    {
        DBContext db;
        public DeliveryNotePrintingController(DBContext db)
        {
            this.db = db;
        }

        private TransactionRepo transactionRepo = TransactionRepo.Instance;

        private const string MENU_URL = "/DeliveryNotePrinting/Index";

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

            ViewData["Title"] = "Delivery Note Printing (CKB)";

            return View();
        }

        public JsonResult Lookup(DateTime? from, DateTime? to)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(new { status = true, data = BaseRepo.ToRows(transactionRepo.LookupSplitDn(from ?? DateTime.Today.AddMonths(-3), to ?? DateTime.Today, db)) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Detail(string dn)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                var rows = BaseRepo.ToRows(transactionRepo.GetDnDetail(dn?.Trim(), db));
                if (rows.Count == 0)
                {
                    return Json(new { status = false, message = "Delivery Note not found" });
                }
                return Json(new { status = true, data = rows });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
