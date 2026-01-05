using Microsoft.EntityFrameworkCore;
using MessManagementSystem.Models;

namespace MessManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<BillingConfiguration> BillingConfigurations { get; set; }
        public DbSet<AttendanceDispute> AttendanceDisputes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Teacher>()
                .HasOne(t => t.User)
                .WithOne(u => u.Teacher)
                .HasForeignKey<Teacher>(t => t.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Teacher)
                .WithMany(t => t.Attendances)
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Bill>()
                .HasOne(b => b.Teacher)
                .WithMany(t => t.Bills)
                .HasForeignKey(b => b.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure decimal precision
            modelBuilder.Entity<MenuItem>()
                .Property(m => m.RatePerServing)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Bill>()
                .Property(b => b.FoodBill)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Bill>()
                .Property(b => b.WaterBill)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Bill>()
                .Property(b => b.TotalBill)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Bill>()
                .Property(b => b.UnpaidBalance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BillingConfiguration>()
                .Property(bc => bc.MonthlyWaterBillTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BillingConfiguration>()
                .Property(bc => bc.DefaultBreakfastRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BillingConfiguration>()
                .Property(bc => bc.DefaultLunchRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BillingConfiguration>()
                .Property(bc => bc.DefaultDinnerRate)
                .HasPrecision(18, 2);
        }
    }
}
