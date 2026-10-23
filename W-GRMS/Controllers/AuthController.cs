using GRMS.Common.Controllers;
using GRMS.Common.Models;
using GRMS.Models;
using GRMS.Models.DB;
using GRMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace GRMS.Controllers
{
    public class AuthController : BaseController
    {
        DBContext db;
        public AuthController(DBContext db)
        {
            this.db = db;
        }

        private LoginRepo loginRepo = LoginRepo.Instance;
        private UserRepo userRepo = UserRepo.Instance;

        public IActionResult Login(string param)
        {
            if (HttpContext.Session.GetString("USERNAME") != null)
            {
                return Redirect("/Home/Index");
            }

            ViewData["Title"] = "Login";
            ViewData["ParamURL"] = param;
            return View();
        }

        public ActionResult Validate(User data, string param)
        {
            User user = loginRepo.GetUserByUsername(data, db);

            string module = "Login";
            string function = "Validate";
            string location = "Login";

            LogHeader logH = new LogHeader();
            logH.MODULE = module;
            logH.FUNCTION = function;

            long processid = LogMonitoringRepo.Instance.StartLog(logH, location, data.USER_ID ?? "anonymous", db);

            try
            {
                if (user == null)
                {
                    LogMonitoringRepo.Instance.WriteLog(processid, "3", "ERR", "invalid username", location, data.USER_ID ?? "anonymous", db);
                    return Json(new { status = false, message = "Invalid Username!" });
                }

                if (user.USER_PWD != HashPassword(data.PASSWORD))
                {
                    LogMonitoringRepo.Instance.WriteLog(processid, "3", "ERR", "Invalid Password!", location, user.USER_ID, db);
                    return Json(new { status = false, message = "Invalid Password!" });
                }

                // Sama seperti desktop: password kadaluarsa wajib diganti dulu sebelum masuk.
                // Session login penuh belum dibuat, hanya penanda user yang sedang ganti password.
                if (user.EXP_PWD == null || user.EXP_PWD.Value.Date < DateTime.Today)
                {
                    HttpContext.Session.SetString("EXPIRED_USER", user.USER_ID);
                    HttpContext.Session.SetString("EXPIRED_PARAM", param ?? "");
                    LogMonitoringRepo.Instance.WriteLog(processid, "2", "WRN", "Password expired", location, user.USER_ID, db);
                    return Json(new { status = true, expired = true, message = "Your password has expired, please change your password" });
                }

                StartSession(user);
                LogMonitoringRepo.Instance.WriteLog(processid, "2", "INF", "Login successful", location, user.USER_ID, db);

                return Json(new { status = true, message = "Login successful!", data = SafeRedirect(param) });
            }
            catch (Exception ex)
            {
                LogMonitoringRepo.Instance.WriteLog(processid, "4", "ERR", ex.Message, location, data.USER_ID ?? "anonymous", db);
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Redirect setelah login hanya boleh ke URL lokal aplikasi (cegah open redirect)
        private string SafeRedirect(string param)
        {
            if (string.IsNullOrEmpty(param)) { return null; }
            param = param.Replace("&amp;", "&");
            return Url.IsLocalUrl(param) ? param : null;
        }

        private void StartSession(User user)
        {
            string menuURL = "";
            string functionList = "";

            List<Menu> menus = loginRepo.GetMenuByRole(user, db);
            foreach (Menu oMenu in menus)
            {
                menuURL = menuURL + ", " + oMenu.MENU_URL;
            }
            List<Function> functions = loginRepo.GetFunctionByRole(user, db);
            foreach (Function oFunc in functions)
            {
                functionList = functionList + ", " + oFunc.FUNCTION_ID;
            }

            CreateSession(user, GenerateMenu(menus), menuURL, functionList);
        }

        // Halaman ganti password: dipakai untuk password expired (sebelum login penuh)
        // maupun ganti password biasa dari menu user di topbar.
        public IActionResult ChangePassword()
        {
            string userId = HttpContext.Session.GetString("USERNAME") ?? HttpContext.Session.GetString("EXPIRED_USER");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            ViewData["Title"] = "Change Password";
            ViewData["UserId"] = userId;
            ViewData["Expired"] = HttpContext.Session.GetString("USERNAME") == null;
            return View();
        }

        [HttpPost]
        public JsonResult SavePassword(User data)
        {
            try
            {
                string userId = HttpContext.Session.GetString("USERNAME") ?? HttpContext.Session.GetString("EXPIRED_USER");
                if (userId == null)
                {
                    return Json(new { status = false, message = "Session expired, please login again" });
                }

                if (string.IsNullOrEmpty(data.PASSWORD) || data.PASSWORD.Length < 6)
                {
                    return Json(new { status = false, message = "New password must be at least 6 characters" });
                }

                if (data.PASSWORD != data.CONFIRM_PASSWORD)
                {
                    return Json(new { status = false, message = "Confirm password does not match" });
                }

                DBResult result = userRepo.UpdatePassword(userId, HashPassword(data.OLD_PASSWORD), HashPassword(data.PASSWORD), userId, db);
                if (!result.status)
                {
                    return Json(result);
                }

                string redirect = "/Home/Index";
                if (HttpContext.Session.GetString("USERNAME") == null)
                {
                    // Lanjutkan login yang tertunda karena password expired
                    string param = HttpContext.Session.GetString("EXPIRED_PARAM");
                    User user = loginRepo.GetUserByUsername(new User { USER_ID = userId }, db);
                    HttpContext.Session.Remove("EXPIRED_USER");
                    HttpContext.Session.Remove("EXPIRED_PARAM");
                    StartSession(user);
                    redirect = SafeRedirect(param) ?? redirect;
                }

                return Json(new { status = true, message = result.message, data = redirect });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Logout()
        {
            ClearSession();
            return RedirectToAction("Login", "Auth");
        }
    }
}
