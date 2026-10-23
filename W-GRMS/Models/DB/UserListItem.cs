using GRMS.Common.Models;

namespace GRMS.Models.DB
{
    // Read model untuk grid User Management (UserRepo.Search / UserRepo.GetByKey).
    // Keyless & unmapped - lihat DBContext.OnModelCreating.
    public class UserListItem : BaseModel
    {
        public string? USER_ID { get; set; }
        public string? USER_NAME { get; set; }
        public string? DEPT { get; set; }
        public string? NIK { get; set; }
        public DateTime? EXP_PWD { get; set; }
        public string? EMAIL { get; set; }
        public string? PHONE { get; set; }
        public string? ROLE_ID { get; set; }
        public string? ROLE_NAME { get; set; }
        public string? DELETE_FLAG { get; set; }
        public string? PLANTS { get; set; }
    }
}
