using GRMS.Common.Controllers;
using GRMS.Models;
using GRMS.Models.Repo;
using Microsoft.Extensions.Options;
using System.Data;

namespace GRMS.Controllers
{
    // Konversi frmSummaryDelayDeliveryCKB (desktop "Summary Delay Delivery (CKB)")
    public class SummaryDelayController : GridReportController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public SummaryDelayController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        protected override string MenuUrl => "/SummaryDelay/Index";
        protected override string Title => "Delivery Delay Summary (CKB)";
        protected override string FilePrefix => "SUMMARY-DELAY-DELIVERY-CKB";

        protected override GridReportPage BuildPage()
        {
            return new GridReportPage
            {
                Plants = ckbSources,
                PlantOptional = true,
                PeriodLabel = "Receive plan",
                Columns = new List<ReportColumn>
                {
                    new("SOCDNO", "Delivery Note"),
                    new("PLCPCD", "Plant"),
                    new("SUPSCD", "Supplier Code"),
                    new("SUPPCD", "Supplier Plant"),
                    new("SUPSHD", "Shipping Dock"),
                    new("SUPNAM", "Supplier Name"),
                    new("RACRAC", "Receiving Area"),
                    new("PAMPNO", "Part Number"),
                    new("PAMPNM", "Part Name"),
                    new("PAMUNO", "Unique No"),
                    new("KBNORDTPC", "Remaining Qty", "number"),
                    new("SOCPRD", "Receive Plan Date", "date"),
                    new("SOCPRT", "Receive Plan Time")
                },
            };
        }

        protected override DataTable LoadData(ReportFilter filter)
        {
            return ReportRepo.Instance.SummaryDelayDelivery(filter.PlantCode, filter.From.Value, filter.To.Value, db);
        }
    }
}
