using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceAPIV2.Models
{
    public class AttendanceContext : IdentityDbContext<User>
    {
        public AttendanceContext(DbContextOptions<AttendanceContext> options)
            : base(options)
        {
        }

       // public DbSet<User> Users { get; set; }
       
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
		public DbSet<SessionQrCode> SessionQRCodes { get; set; }

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = "Data Source=DESKTOP-LU175Q4\\SQLEXPRESS01;Initial Catalog=AttendanceV2;Integrated Security=True; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=true";

            optionsBuilder.UseSqlServer(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(a => a.User)
                .WithMany(u => u.AttendanceRecords)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(a => a.Session)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

            modelBuilder.Entity<Session>()
                .HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.User_Id)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete
                                                    

            // Unique constraint for Email in Users table
            modelBuilder.Entity<User>()
                .HasIndex(s => s.Email)
                .IsUnique();


            base.OnModelCreating(modelBuilder);
        }
    }
}
