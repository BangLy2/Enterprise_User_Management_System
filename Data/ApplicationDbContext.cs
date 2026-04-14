using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyWeb.Models;

namespace MyWeb.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Department> Departments {  get; set; }

        public DbSet<ActivityInsight> ActivityInsights { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Action);
            });

            builder.Entity<Department>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });
        }
    }
}
