using DevExpress.XtraReports.UI;

namespace GRMS.Reports
{
    // Delivery Note (desain dari GRMS desktop Report/rptDN).
    // Data diisi lewat ReportHelper.ToPdf dengan DataTable bernama "printDN".
    public partial class rptDN : DevExpress.XtraReports.UI.XtraReport
    {
        public rptDN()
        {
            InitializeComponent();
        }

        // Event handler kosong dari desain desktop (masih direferensikan Designer)
        private void ReportHeader_AfterPrint(object sender, EventArgs e)
        {
        }

        private void rptDN_AfterPrint(object sender, EventArgs e)
        {
        }
    }
}
