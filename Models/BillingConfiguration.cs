using System.ComponentModel.DataAnnotations;

namespace MessManagementSystem.Models
{
    public class BillingConfiguration
    {
        [Key]
        public int ConfigId { get; set; }

        [Required]
        [Range(0, 100000)]
        public decimal MonthlyWaterBillTotal { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal DefaultBreakfastRate { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal DefaultLunchRate { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal DefaultDinnerRate { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public int? UpdatedBy { get; set; } // UserId of admin
    }
}
