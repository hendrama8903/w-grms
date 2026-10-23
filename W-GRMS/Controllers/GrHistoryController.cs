using GRMS.Common.Controllers;
using GRMS.Models;
using GRMS.Models.Repo;
using Microsoft.Extensions.Options;
using System.Data;

namespace GRMS.Controllers
{
    // Konversi frmGoodsReceiveHistoryCKB (desktop "Goods Receiving History (CKB)")
    public class GrHistoryController : GridReportController
    {
        DBContext db;
        List<CkbSourceOptions> ckbSources;
        public GrHistoryController(DBContext db, IOptions<List<CkbSourceOptions>> ckbSources)
        {
            this.db = db;
            this.ckbSources = ckbSources.Value;
        }

        protected override string MenuUrl => "/GrHistory/Index";
        protected override string Title => "Goods Receiving History (CKB)";
        protected override string FilePrefix => "GR-HISTORY-CKB";

        protected override GridReportPage BuildPage()
        {
            return new GridReportPage
            {
                Plants = ckbSources,
                FilterOptions = new List<KeyValuePair<string, string>>
                {
                    new(ReportRepo.FILTER_ACTUAL_RECEIVE, "Actual Receive"),
                    new(ReportRepo.FILTER_ACTUAL_INPUT, "Actual Input"),
                    new(ReportRepo.FILTER_PLAN_RECEIVE, "Plan Receive")
                },
                Columns = new List<ReportColumn>
                {
                    new("NOSEQ", "Receipt Number"),
                    new("DELIVERYNOTE", "Delivery Note"),
                    new("SURATJALAN", "Supplier DN"),
                    new("SUPSCD", "Supplier Code"),
                    new("SUPPCD", "Supplier Plant"),
                    new("SUPSHD", "Shipping Dock"),
                    new("SUPNAM", "Supplier Name"),
                    new("RACRAC", "Receiving Area"),
                    new("PARTNO", "Part Number"),
                    new("PAMPNM", "Part Name"),
                    new("PAMUNO", "Unique No"),
                    new("QTY", "Qty", "number"),
                    new("PLANRECEIVE", "Plan Receive", "date"),
                    new("ACTUALRECEIVE", "Actual Receive", "date"),
                    new("ACTUALTIME", "Actual Time"),
                    new("INPUTDATE", "Input Date", "datetime"),
                    new("INPUTUSER", "Input By"),
                    new("FILE_ID", "SAP File")
                },
            };
        }

        protected override DataTable LoadData(ReportFilter filter)
        {
            return ReportRepo.Instance.GoodsReceiveHistory(filter.PlantCode, filter.FilterBy, filter.From.Value, filter.To.Value, db);
        }
    }
}
