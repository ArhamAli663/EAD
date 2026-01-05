using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MessManagementSystem.Models
{
    [Index(nameof(AttendanceId))]
    [Index(nameof(TeacherId))]
    [Index(nameof(Status))]
    public class AttendanceDispute
    {
        [Key]
        public int DisputeId { get; set; }

        [Required]
        public int AttendanceId { get; set; }

        [ForeignKey("AttendanceId")]

        public virtual Attendance? Attendance { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Teacher? Teacher { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        [Required]
        public DateTime ReportedDate { get; set; } = DateTime.Now;

        public int? ResolvedBy { get; set; }

        [ForeignKey("ResolvedBy")]
        public virtual User? ResolvedByUser { get; set; }

        public DateTime? ResolvedDate { get; set; }

        [StringLength(1000)]
        public string? AdminNotes { get; set; }

        public bool IsAcknowledgedByTeacher { get; set; } = false;

        public DateTime? AcknowledgedDate { get; set; }
    }
}
