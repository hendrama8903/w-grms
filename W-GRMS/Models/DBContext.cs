using GRMS.Models.DB;
using Microsoft.EntityFrameworkCore;

namespace GRMS.Models
{
    public partial class DBContext : DbContext
    {
        public DBContext(DbContextOptions<DBContext> options) : base(options) { }

        public virtual DbSet<User>? User { get; set; }
        public virtual DbSet<UserListItem>? UserListItem { get; set; }
        public virtual DbSet<RoleListItem>? RoleListItem { get; set; }
        public virtual DbSet<MenuListItem>? MenuListItem { get; set; }
        public virtual DbSet<FunctionListItem>? FunctionListItem { get; set; }
        public virtual DbSet<MenuFunctionStats>? MenuFunctionStats { get; set; }
        public virtual DbSet<Role>? Role { get; set; }
        public virtual DbSet<Menu>? Menu { get; set; }
        public virtual DbSet<Function>? Function { get; set; }
        public virtual DbSet<AuthMenu>? AuthMenu { get; set; }
        public virtual DbSet<AuthFunction>? AuthFunction { get; set; }
        public virtual DbSet<LogHeader>? LogHeader { get; set; }
        public virtual DbSet<LogDetail>? LogDetail { get; set; }
        public virtual DbSet<PartsMaster>? PartsMaster { get; set; }
        public virtual DbSet<SupplierMaster>? SupplierMaster { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<PartsMaster>().HasNoKey().ToView(null);
            builder.Entity<SupplierMaster>().HasNoKey().ToView(null);
            builder.Entity<UserListItem>().HasNoKey().ToView(null);
            builder.Entity<RoleListItem>().HasNoKey().ToView(null);
            builder.Entity<MenuListItem>().HasNoKey().ToView(null);
            builder.Entity<FunctionListItem>().HasNoKey().ToView(null);
            builder.Entity<MenuFunctionStats>().HasNoKey().ToView(null);

            builder.Entity<AuthMenu>().HasKey(table => new
            {
                table.ROLE_ID,
                table.MENU_ID
            });

            builder.Entity<AuthFunction>().HasKey(table => new
            {
                table.ROLE_ID,
                table.FUNCTION_ID
            });

            builder.Entity<LogDetail>().HasKey(table => new
            {
                table.PROCESS_ID,
                table.SEQ_NO
            });
        }
    }
}
