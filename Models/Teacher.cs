using System.ComponentModel.DataAnnotations;

namespace MessManagementSystem.Models
{
    public class Teacher
    {
        [Key]
        public int TeacherId { get; set; }

        [Required]
        [StringLength(100)]
        public required string FullName { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Phone]
        public required string PhoneNumber { get; set; }

        [StringLength(100)]
        public required string Department { get; set; }

        public DateTime JoiningDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        // Foreign Key
        public int? UserId { get; set; }
        public User? User { get; set; }

        // Navigation properties
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<Bill> Bills { get; set; } = new List<Bill>();
    }
}
