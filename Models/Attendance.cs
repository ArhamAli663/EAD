using System.ComponentModel.DataAnnotations;

namespace MessManagementSystem.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int TeacherId { get; set; }
        public Teacher? Teacher { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public bool BreakfastTaken { get; set; } = false;
        public bool LunchTaken { get; set; } = false;
        public bool DinnerTaken { get; set; } = false;

        public string? Remarks { get; set; }

        public DateTime RecordedDate { get; set; } = DateTime.Now;

        public int? RecordedBy { get; set; } // UserId of attendance taker
    }
}
