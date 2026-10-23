namespace GRMS.Models
{
    // Plant CKB & sumber linked server, dibaca dari appsettings.json bagian "CkbSources"
    // (pengganti nilai hardcode di form CKB desktop). Source = prefix nama 3 bagian
    // "[server].[database].[schema]"; kosong = plant tidak punya database CKB.
    // Semua akses CKB lewat linked server karena server CKB masih SQL Server 2000.
    public class CkbSourceOptions
    {
        public const string SECTION = "CkbSources";

        public string PlantCode { get; set; } = "";
        public string PlantName { get; set; } = "";

        // Database CKB plant: Parts Master (OLKPAM), Order Data (OLKKBNORD + OLKSOC, tandai GRMS = 1) dan Data SOC
        public string? PartSource { get; set; }

        // Master supplier (OLKSUP); biasanya sama dengan PartSource, kecuali plant 4 (Machining)
        public string? SupplierSource { get; set; }
    }
}
