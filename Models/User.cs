using System.ComponentModel.DataAnnotations;

namespace MessManagementSystem.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public required string Username { get; set; }

        [Required]
        public required string PasswordHash { get; set; }

        [Required]
        [StringLength(50)]
        public required string Role { get; set; } // Admin, Teacher, AttendanceTaker

        public bool MustChangePassword { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        // Navigation property
        public Teacher? Teacher { get; set; }
    }
}
