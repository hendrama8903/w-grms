using DevExpress.XtraReports.UI;

namespace GRMS.Reports
{
    // Goods Receive Note (desain dari GRMS desktop Report/rptGRN).
    // Data diisi lewat ReportHelper.ToPdf dengan DataTable bernama "printGRN".
    public partial class rptGRN : DevExpress.XtraReports.UI.XtraReport
    {
        public rptGRN()
        {
            InitializeComponent();
        }
    }
}
