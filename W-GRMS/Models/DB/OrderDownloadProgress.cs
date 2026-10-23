namespace GRMS.Models.DB
{
    // Status download Order Data dari CKB yang berjalan di background; dibaca layar lewat polling.
    // Stage: "read" = membaca order baru dari CKB, "save" = menyimpan ke GRMS per baris, "done" = selesai.
    public class OrderDownloadProgress
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString("N");
        public string PlantCode { get; set; } = "";
        public string PlantName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Stage { get; set; } = "read";
        public int Total { get; set; }
        public int Inserted { get; set; }
        public int Failed { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public bool? Status { get; set; }
        public string? Message { get; set; }
    }
}
