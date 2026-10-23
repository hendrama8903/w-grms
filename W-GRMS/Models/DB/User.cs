using GRMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GRMS.Models.DB
{
    // TB_M_USER milik GRMS (dipakai bersama aplikasi desktop).
    // Kolom ROLE_ID/EMAIL/PHONE/DELETE_FLAG ditambahkan untuk web - lihat SQL/01_CreateAuthTables.sql.
    [Table("TB_M_USER")]
    public partial class User : BaseModel
    {
        [Key]
        public string? USER_ID { get; set; }
        public string? USER_NAME { get; set; }
        public string? USER_PWD { get; set; }
        public string? DEPT { get; set; }
        public string? NIK { get; set; }
        public DateTime? EXP_PWD { get; set; }
        public string? EMAIL { get; set; }
        public string? PHONE { get; set; }
        public string? ROLE_ID { get; set; }
        public string? ROLE_NAME { get; set; }
        public string? DELETE_FLAG { get; set; }

        // Field form saja (tidak ada di tabel)
        [NotMapped]
        public string? OLD_PASSWORD { get; set; }
        [NotMapped]
        public string? PASSWORD { get; set; }
        [NotMapped]
        public string? CONFIRM_PASSWORD { get; set; }
        [NotMapped]
        public string? PLANTS { get; set; }
    }
}
