using System.ComponentModel.DataAnnotations;

namespace MessManagementSystem.Models
{
    public class MenuItem
    {
        [Key]
        public int MenuItemId { get; set; }

        [Required]
        [StringLength(100)]
        public required string ItemName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public required string MealType { get; set; } // Breakfast, Lunch, Dinner

        [Required]
        public required string DayOfWeek { get; set; } // Monday, Tuesday, etc.

        [Required]
        [Range(0, 10000)]
        public decimal RatePerServing { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
