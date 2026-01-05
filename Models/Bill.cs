using System.ComponentModel.DataAnnotations;

namespace MessManagementSystem.Models
{
    public class Bill
    {
        [Key]
        public int BillId { get; set; }

        [Required]
        public int TeacherId { get; set; }
        public required Teacher Teacher { get; set; }

        [Required]
        public int Month { get; set; } // 1-12

        [Required]
        public int Year { get; set; }

        [Required]
        [Range(0, 1000000)]
        public decimal FoodBill { get; set; }

        [Required]
        [Range(0, 1000000)]
        public decimal WaterBill { get; set; }

        [Required]
        [Range(0, 1000000)]
        public decimal TotalBill { get; set; }

        public int TotalMealsConsumed { get; set; }

        public bool IsPaid { get; set; } = false;

        public DateTime? PaidDate { get; set; }

        public DateTime GeneratedDate { get; set; } = DateTime.Now;

        public int? GeneratedBy { get; set; } // UserId of admin

        [Range(0, 1000000)]
        public decimal UnpaidBalance { get; set; } = 0;

        public string? PaymentToken { get; set; }

        public string? PaymentMethod { get; set; } // Card, UPI, etc.

        public string? TransactionId { get; set; }
    }
}
