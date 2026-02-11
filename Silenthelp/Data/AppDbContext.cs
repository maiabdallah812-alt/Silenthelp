using Microsoft.EntityFrameworkCore;
using SilentHelp.Models;

namespace SilentHelp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<FamilyLink> FamilyLinks => Set<FamilyLink>();
        public DbSet<Alert> Alerts => Set<Alert>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User email is unique
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Role validation
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            // FamilyLink relationships
            modelBuilder.Entity<FamilyLink>()
                .HasOne(fl => fl.Parent)
                .WithMany(u => u.ParentLinks)
                .HasForeignKey(fl => fl.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FamilyLink>()
                .HasOne(fl => fl.Child)
                .WithMany(u => u.ChildLinks)
                .HasForeignKey(fl => fl.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FamilyLink>()
                .HasIndex(fl => fl.InviteCode)
                .IsUnique();

            // Alert relationships
            modelBuilder.Entity<Alert>()
                .HasOne(a => a.Child)
                .WithMany(u => u.Alerts)
                .HasForeignKey(a => a.ChildId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
