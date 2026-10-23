namespace GRMS.Models.DB
{
    // OLKPAM - part master CKB (key: SUPSCD, SUPPCD, SUPSHD, PLCPCD, RACRAC, PAMPNO, PAMPCK).
    // Keyless & unmapped - hanya dibaca lewat MasterDataRepo.SearchParts, lihat DBContext.OnModelCreating.
    public class PartsMaster
    {
        public string? SUPSCD { get; set; }
        public string? SUPPCD { get; set; }
        public string? SUPSHD { get; set; }
        public string? PLCPCD { get; set; }
        public string? RACRAC { get; set; }
        public string? PAMPNO { get; set; }
        public int? PAMPCK { get; set; }
        public string? PAMPNM { get; set; }
        public string? PAMBSC { get; set; }
        public string? PAMUNO { get; set; }
    }
}
