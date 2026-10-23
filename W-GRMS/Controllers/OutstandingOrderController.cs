using GRMS.Common.Controllers;
using GRMS.Models;
using GRMS.Models.Repo;
using Microsoft.Extensions.Options;
using System.Data;

namespace GRMS.Controllers
{
    // Konversi frmOutstandingOrderCKB (desktop "Outstanding Order (CKB)")
    public class OutstandingOrderController : GridReportController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public OutstandingOrderController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        protected override string MenuUrl => "/OutstandingOrder/Index";
        protected override string Title => "Outstanding Order (CKB)";
        protected override string FilePrefix => "OUTSTANDING-ORDER-CKB";

        protected override GridReportPage BuildPage()
        {
            return new GridReportPage
            {
                Plants = ckbSources,
                PeriodLabel = "Receive plan",
                Columns = new List<ReportColumn>
                {
                    new("SOCDNO", "Delivery Note"),
                    new("SUPSCD", "Supplier Code"),
                    new("SUPPCD", "Supplier Plant"),
                    new("SUPSHD", "Shipping Dock"),
                    new("SUPNAM", "Supplier Name"),
                    new("RACRAC", "Receiving Area"),
                    new("PAMPNO", "Part Number"),
                    new("PAMPNM", "Part Name"),
                    new("PAMUNO", "Unique No"),
                    new("KBNORDTPC", "Order Qty", "number"),
                    new("SOCPRD", "Receive Plan Date", "date"),
                    new("SOCPRT", "Receive Plan Time"),
                    new("SOCPOCS", "Order Cycle")
                },
            };
        }

        protected override DataTable LoadData(ReportFilter filter)
        {
            return ReportRepo.Instance.OutstandingOrder(filter.PlantCode, filter.From.Value, filter.To.Value, db);
        }
    }
}
