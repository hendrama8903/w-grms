namespace GRMS.Models.DB
{
    // Input simpan Goods Receiving (CKB): header dari layar + baris kanban yang dicentang
    public class GoodsReceiveSave
    {
        public string? DELIVERYNOTE { get; set; }
        public string? SURATJALAN { get; set; }
        public DateTime? TRANSDATE { get; set; }
        public List<GoodsReceiveItem> ITEMS { get; set; } = new List<GoodsReceiveItem>();
    }

    public class GoodsReceiveItem
    {
        public string? PARTNO { get; set; }
        public string? KANBANNO { get; set; }
        public string? KEYCODE { get; set; }
        public int QTY { get; set; }
    }

    // Hasil simpan: nomor GRN baru + DN sisa (DN#n) kalau ada qty yang belum diterima
    public class GoodsReceiveResult
    {
        public string? NOSEQ { get; set; }
        public string? NEW_DN { get; set; }
    }
}
