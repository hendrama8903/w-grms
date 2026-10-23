using GRMS.Common.Controllers;
using GRMS.Common.Repo;
using GRMS.Models;
using GRMS.Models.DB;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Data;

namespace GRMS.Controllers
{
    // Konversi frmGoodsReceivingCKB (desktop "Goods Receiving (CKB)"): scan/ketik DN, pilih kanban & qty,
    // simpan GRN, buat DN sisa otomatis, buka GRN lama, hapus GRN, cetak GRN / DN.
    public class GoodsReceivingController : BaseController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public GoodsReceivingController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        private TransactionRepo transactionRepo = TransactionRepo.Instance;

        private const string MENU_URL = "/GoodsReceiving/Index";

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

            ViewData["Save"] = HasFunction("GR-SAVE");
            ViewData["Delete"] = HasFunction("GR-DELETE");

            ViewData["Title"] = "Goods Receiving (CKB)";

            return View();
        }

        public JsonResult GetDn(string dn)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(transactionRepo.GetDnForReceive(dn?.Trim(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetReceive(string noseq)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                DataTable dt = transactionRepo.GetReceive(noseq, db);
                if (dt.Rows.Count == 0)
                {
                    return Json(new { status = false, message = "Data not found" });
                }

                string remainingDn = transactionRepo.RemainingDnOf(dt.Rows[0]["DELIVERYNOTE"].ToString(), db);
                return Json(new { status = true, data = BaseRepo.ToRows(dt), closed = remainingDn != null, remainingDn });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Lookup(DateTime? from, DateTime? to)
        {
            if (!HasMenuAccess(MENU_URL))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(new { status = true, data = BaseRepo.ToRows(transactionRepo.LookupReceive(from ?? DateTime.Today.AddMonths(-3), to ?? DateTime.Today, ckbSources.Select(x => x.PlantCode), db)) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Save([FromBody] GoodsReceiveSave data)
        {
            if (!HasFunction("GR-SAVE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                data.DELIVERYNOTE = data.DELIVERYNOTE?.Trim();
                return Json(transactionRepo.SaveReceive(data, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Delete(string noseq)
        {
            if (!HasFunction("GR-DELETE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(transactionRepo.DeleteReceive(noseq, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
