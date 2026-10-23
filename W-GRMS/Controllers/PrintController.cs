using GRMS.Common.Controllers;
using GRMS.Models;
using GRMS.Models.Repo;
using GRMS.Reports;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace GRMS.Controllers
{
    // Cetak DN & GRN sebagai PDF (DevExpress XtraReport, desain dari desktop), dibuka di tab browser.
    public class PrintController : BaseController
    {
        DBContext db;
        public PrintController(DBContext db)
        {
            this.db = db;
        }

        private TransactionRepo transactionRepo = TransactionRepo.Instance;

        public IActionResult Dn(string dn)
        {
            if (!HasMenuAccess("/GoodsReceiving/Index") && !HasMenuAccess("/DeliveryNotePrinting/Index"))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            DataTable data = transactionRepo.PrintDnData(dn, db);
            if (data.Rows.Count == 0)
            {
                return Content("No data to print for Delivery Note " + dn);
            }

            byte[] pdf = ReportHelper.ToPdf(new rptDN(), data, "printDN", "DN" + dn);
            return InlinePdf(pdf, "DN" + dn);
        }

        public IActionResult Grn(string noseq)
        {
            if (!HasMenuAccess("/GoodsReceiving/Index"))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            DataTable data = transactionRepo.PrintGrnData(noseq, db);
            if (data.Rows.Count == 0)
            {
                return Content("No data to print for " + noseq);
            }

            // Nama file = nomor GRN + nomor DN, mis. GRN2609005965_DN1902003801.pdf
            string dn = data.Rows[0]["DELIVERYNOTE"].ToString();
            byte[] pdf = ReportHelper.ToPdf(new rptGRN(), data, "printGRN", noseq + "_DN" + dn);
            return InlinePdf(pdf, noseq + "_DN" + dn);
        }

        // PDF tetap dibuka di tab browser (inline), tapi saat disimpan / di-download memakai nama file ini.
        // '#' pada DN sisa (DN#1) tidak valid di nama file, diganti '-'.
        private IActionResult InlinePdf(byte[] pdf, string name)
        {
            string fileName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '#' ? '-' : c)) + ".pdf";
            var disposition = new System.Net.Mime.ContentDisposition { Inline = true, FileName = fileName };
            Response.Headers["Content-Disposition"] = disposition.ToString();
            return File(pdf, "application/pdf");
        }
    }
}
