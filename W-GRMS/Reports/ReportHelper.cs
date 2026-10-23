using DevExpress.XtraReports.UI;
using System.Data;

namespace GRMS.Reports
{
    public static class ReportHelper
    {
        // Render XtraReport ke PDF. Binding desain desktop memakai "<dataMember>.<kolom>",
        // jadi data dibungkus DataSet dengan nama tabel = dataMember.
        // title = judul dokumen PDF (tampil di tab browser), opsional
        public static byte[] ToPdf(XtraReport report, DataTable data, string dataMember, string title = null)
        {
            data.TableName = dataMember;
            DataSet ds = new DataSet();
            ds.Tables.Add(data);

            report.DataSource = ds;
            report.DataMember = dataMember;
            if (!string.IsNullOrEmpty(title))
            {
                report.DisplayName = title;
                report.ExportOptions.Pdf.DocumentOptions.Title = title;
            }

            using (var ms = new MemoryStream())
            {
                report.ExportToPdf(ms);
                return ms.ToArray();
            }
        }
    }
}
