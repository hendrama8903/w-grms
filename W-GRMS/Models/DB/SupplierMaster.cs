namespace GRMS.Models.DB
{
    // OLKSUP - supplier master CKB (key: SUPSCD, SUPPCD, SUPSHD).
    // Keyless & unmapped - hanya dibaca lewat MasterDataRepo.SearchSuppliers, lihat DBContext.OnModelCreating.
    public class SupplierMaster
    {
        public string? SUPSCD { get; set; }
        public string? SUPPCD { get; set; }
        public string? SUPSHD { get; set; }
        public string? SUPNAM { get; set; }
    }
}
