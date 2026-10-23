using GRMS.Common.Controllers;
using GRMS.Common.Repo;
using GRMS.Models;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GRMS.Controllers
{
    // Master Receiving Area: kode area receive CKB yang menjadi pilihan akses user di User Management.
    public class ReceivingAreaController : BaseController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public ReceivingAreaController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        private ReceivingAreaRepo receivingAreaRepo = ReceivingAreaRepo.Instance;

        private const string MENU_URL = "/ReceivingArea/Index";

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

            ViewData["Save"] = HasFunction("RECAREA-SAVE");
            ViewData["Delete"] = HasFunction("RECAREA-DELETE");
            ViewData["Plants"] = ckbSources;

            ViewData["Title"] = "Receiving Area";

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
                return Json(new { status = true, data = BaseRepo.ToRows(receivingAreaRepo.Search(db)) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Save(string screenMode, string AREA_CODE, string AREA_NAME, string PLANT_CODE)
        {
            if (!HasFunction("RECAREA-SAVE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(receivingAreaRepo.Save(screenMode == "ADD" ? "ADD" : "EDIT", AREA_CODE, AREA_NAME, PLANT_CODE, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Delete(string AREA_CODE)
        {
            if (!HasFunction("RECAREA-DELETE"))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            try
            {
                return Json(receivingAreaRepo.Delete(AREA_CODE, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
