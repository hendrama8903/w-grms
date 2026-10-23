using GRMS.Models.DB;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GRMS.Common.Controllers
{
    public class BaseController : Controller
    {
        public string GenerateMenu(List<Menu> menus)
        {
            DataSet ds = new DataSet();
            ds = ToDataSet(menus);
            DataTable table = ds.Tables[0];
            DataRow[] parentMenus = table.Select("PARENT_ID IS NULL");
            var sb = new StringBuilder();
            string menuString = GenerateUL(parentMenus, table, sb);

            return menuString;
        }

        public string GenerateUL(DataRow[] menu, DataTable table, StringBuilder sb)
        {
            if (menu.Length > 0)
            {
                foreach (DataRow dr in menu)
                {
                    string menuText = dr["MENU_NAME"].ToString();
                    string url = dr["MENU_URL"].ToString();
                    string icon = dr["MENU_ICON"].ToString();
                    string parentId = dr["PARENT_ID"].ToString();
                    string pid = dr["MENU_ID"].ToString();
                    DataRow[] subMenu = table.Select(String.Format("PARENT_ID = '{0}'", pid));

                    // Menu yang punya child selalu dirender sebagai grup expand saja,
                    // supaya tidak dobel muncul kalau MENU_URL-nya kelupaan diisi "#".
                    if (url != "#" && subMenu.Length == 0)
                    {
                        string line = String.Format(@"<li id=""sidebarMenu{0}"" data-parentId=""{3}""><a href=""{0}""><i class=""fa {2}""></i> <span>{1}</span></a></li>", url, menuText, icon, parentId);
                        sb.Append(line);
                    }
                    if (subMenu.Length > 0 && !pid.Equals(parentId))
                    {
                        string line = String.Format(@"<li id=""parentId{3}"" class=""treeview""><a href=""#""><i class=""fa {0}""></i> <span>{1}</span><span class=""pull-right-container""><i class=""fa fa-angle-left pull-right""></i></span></a><ul class=""treeview-menu"">", icon, menuText, parentId, pid);
                        var subMenuBuilder = new StringBuilder();
                        sb.AppendLine(line);
                        sb.Append(GenerateUL(subMenu, table, subMenuBuilder));
                        sb.Append("</ul></li>");
                    }
                }
            }
            return sb.ToString();
        }

        public DataSet ToDataSet<T>(List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);
            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name);
            }
            foreach (T item in items)
            {
                var values = new object[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                {
                    values[i] = Props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }
            DataSet ds = new DataSet();
            ds.Tables.Add(dataTable);
            return ds;
        }

        public void CreateSession(User user, string menuString, string menuURL, string functionList)
        {
            HttpContext.Session.SetString("USERNAME", user.USER_ID ?? "");
            HttpContext.Session.SetString("FULL_NAME", user.USER_NAME ?? "");
            HttpContext.Session.SetString("DEPT", user.DEPT ?? "");
            HttpContext.Session.SetString("EMAIL", user.EMAIL ?? "");
            HttpContext.Session.SetString("ROLE_ID", user.ROLE_ID ?? "");
            HttpContext.Session.SetString("ROLE_NAME", user.ROLE_NAME ?? "");
            HttpContext.Session.SetString("menuString", menuString);
            HttpContext.Session.SetString("menuURL", menuURL);
            HttpContext.Session.SetString("functionList", functionList);
        }

        public void ClearSession()
        {
            HttpContext.Session.Clear();
        }

        public string GetLoginUsername()
        {
            return HttpContext.Session.GetString("USERNAME");
        }

        //Cek apakah user yang login berhak akses menu tertentu (mis. "/Role/Index"),
        //berdasarkan CSV menuURL yang disimpan saat login (lihat CreateSession).
        //Dipakai untuk guard action Index (redirect/403) maupun endpoint AJAX (json status=false),
        //supaya endpoint AJAX tidak bisa dipanggil langsung oleh user yang tidak punya menunya.
        public bool HasMenuAccess(string menuUrl)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return false;
            }

            string menuURLList = HttpContext.Session.GetString("menuURL");
            return menuURLList != null && menuURLList.Contains(menuUrl);
        }

        //Cek function (tombol Add/Edit/Delete dst) berdasarkan CSV functionList di session.
        public bool HasFunction(string functionId)
        {
            string functionList = HttpContext.Session.GetString("functionList");
            return functionList != null && functionList.Split(", ").Contains(functionId);
        }

        //Hash password identik dengan PasswordEncryptor.md512 di GRMS desktop
        //(SHA512, tiap byte ToString("X") tanpa padding) supaya password lama tetap valid
        //dan user yang ganti password di web tetap bisa login di desktop, begitu juga sebaliknya.
        public static string HashPassword(string password)
        {
            byte[] hash = SHA512.HashData(Encoding.Default.GetBytes(password ?? ""));
            var sb = new StringBuilder();
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("X"));
            }
            return sb.ToString();
        }

        protected ICellStyle CellStyleLeft(XSSFWorkbook oHSSFWorkbook)
        {
            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Left;
            return oICellStyle;
        }

        protected ICellStyle CellStyleRight(XSSFWorkbook oHSSFWorkbook)
        {
            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Right;
            return oICellStyle;
        }

        protected ICellStyle CellStyleCenter(XSSFWorkbook oHSSFWorkbook)
        {
            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Top;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Center;
            return oICellStyle;
        }
    }
}
