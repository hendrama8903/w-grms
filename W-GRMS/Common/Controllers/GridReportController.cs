using GRMS.Common.Repo;
using GRMS.Models;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;

namespace GRMS.Common.Controllers
{
    // Filter report: plant, periode, dan (opsional) jenis tanggal yang difilter
    public class ReportFilter
    {
        public string? PlantCode { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? FilterBy { get; set; }
    }

    public class ReportColumn
    {
        public string Field { get; set; } = "";
        public string Caption { get; set; } = "";
        // string | number | date | datetime
        public string Type { get; set; } = "string";

        public ReportColumn(string field, string caption, string type = "string")
        {
            Field = field;
            Caption = caption;
            Type = type;
        }
    }

    // Model view bersama Views/Shared/_GridReport.cshtml
    public class GridReportPage
    {
        public string Controller { get; set; } = "";
        public List<CkbSourceOptions> Plants { get; set; } = new List<CkbSourceOptions>();
        public bool PlantOptional { get; set; }
        public List<KeyValuePair<string, string>> FilterOptions { get; set; } = new List<KeyValuePair<string, string>>();
        public string PeriodLabel { get; set; } = "Period";
        public List<ReportColumn> Columns { get; set; } = new List<ReportColumn>();
    }

    // Halaman report "filter + grid + export Excel" (pola report desktop GRMS).
    // Turunan cukup mengisi menu, kolom, dan query; tampilan memakai Views/Shared/_GridReport.cshtml.
    public abstract class GridReportController : BaseController
    {
        protected abstract string MenuUrl { get; }
        protected abstract string Title { get; }
        protected abstract string FilePrefix { get; }
        protected abstract GridReportPage BuildPage();
        protected abstract DataTable LoadData(ReportFilter filter);

        // Rentang periode maksimum supaya query ke tabel jutaan baris tetap wajar
        protected virtual int MaxDays => 366;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login?param=" + MenuUrl);
            }
            else if (!HasMenuAccess(MenuUrl))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            ViewData["Title"] = Title;
            GridReportPage page = BuildPage();
            page.Controller = ControllerContext.ActionDescriptor.ControllerName;
            return View("_GridReport", page);
        }

        private string Validate(ReportFilter filter)
        {
            if (filter.From == null || filter.To == null) return "Please fill the period (from - to)";
            if (filter.From > filter.To) return "Period from must be before period to";
            if ((filter.To.Value - filter.From.Value).TotalDays > MaxDays) return "Maximum period is " + MaxDays + " days";
            return null;
        }

        public JsonResult Search(ReportFilter filter)
        {
            if (!HasMenuAccess(MenuUrl))
            {
                return Json(new { status = false, message = "Access denied" });
            }

            string error = Validate(filter);
            if (error != null) return Json(new { status = false, message = error });

            try
            {
                return Json(new { status = true, data = BaseRepo.ToRows(LoadData(filter)) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel(ReportFilter filter)
        {
            if (!HasMenuAccess(MenuUrl))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            string error = Validate(filter);
            if (error != null) return Content(error);

            DataTable dt = LoadData(filter);
            List<ReportColumn> columns = BuildPage().Columns;

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet(Title.Length > 31 ? Title.Substring(0, 31) : Title);

            ICellStyle dateStyle = workbook.CreateCellStyle();
            dateStyle.DataFormat = workbook.CreateDataFormat().GetFormat("yyyy-mm-dd");
            ICellStyle dateTimeStyle = workbook.CreateCellStyle();
            dateTimeStyle.DataFormat = workbook.CreateDataFormat().GetFormat("yyyy-mm-dd hh:mm:ss");

            IRow headerRow = sheet.CreateRow(0);
            headerRow.CreateCell(0).SetCellValue("No");
            for (int col = 0; col < columns.Count; col++)
            {
                headerRow.CreateCell(col + 1).SetCellValue(columns[col].Caption);
            }

            int rowIndex = 1;
            foreach (DataRow dr in dt.Rows)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(rowIndex);
                for (int col = 0; col < columns.Count; col++)
                {
                    ReportColumn c = columns[col];
                    ICell cell = row.CreateCell(col + 1);
                    object value = dt.Columns.Contains(c.Field) ? dr[c.Field] : null;
                    if (value == null || value == DBNull.Value) continue;

                    if (c.Type == "number")
                    {
                        cell.SetCellValue(Convert.ToDouble(value));
                    }
                    else if ((c.Type == "date" || c.Type == "datetime") && value is DateTime d)
                    {
                        cell.SetCellValue(d);
                        cell.CellStyle = c.Type == "date" ? dateStyle : dateTimeStyle;
                    }
                    else
                    {
                        cell.SetCellValue(value.ToString());
                    }
                }
                rowIndex++;
            }

            for (int col = 0; col <= columns.Count; col++)
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
                FilePrefix + "-" + filter.From.Value.ToString("yyyyMMdd") + "-" + filter.To.Value.ToString("yyyyMMdd") + ".xlsx");
        }
    }
}
