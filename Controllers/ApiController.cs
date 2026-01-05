using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessManagementSystem.Data;
using MessManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MessManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class ApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Get current user profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    userId = user.UserId,
                    username = user.Username,
                    role = user.Role,
                    isActive = user.IsActive,
                    createdDate = user.CreatedDate
                }
            });
        }

        // Get teachers list (Admin only)
        [HttpGet("teachers")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTeachers()
        {
            var teachers = await _context.Teachers
                .Include(t => t.User)
                .Where(t => t.IsActive)
                .Select(t => new
                {
                    teacherId = t.TeacherId,
                    fullName = t.FullName,
                    email = t.Email,
                    department = t.Department,
                    phoneNumber = t.PhoneNumber,
                    joiningDate = t.JoiningDate,
                    username = t.User != null ? t.User.Username : ""
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                count = teachers.Count,
                data = teachers
            });
        }

        // Get attendance for current teacher
        [HttpGet("attendance")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> GetAttendance([FromQuery] int? month, [FromQuery] int? year)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher profile not found." });
            }

            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var attendances = await _context.Attendances
                .Where(a => a.TeacherId == teacher.TeacherId &&
                           a.Date.Month == selectedMonth &&
                           a.Date.Year == selectedYear)
                .OrderByDescending(a => a.Date)
                .Select(a => new
                {
                    attendanceId = a.AttendanceId,
                    date = a.Date,
                    breakfastTaken = a.BreakfastTaken,
                    lunchTaken = a.LunchTaken,
                    dinnerTaken = a.DinnerTaken,
                    remarks = a.Remarks,
                    recordedDate = a.RecordedDate
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                month = selectedMonth,
                year = selectedYear,
                count = attendances.Count,
                data = attendances
            });
        }

        // Get bills for current teacher
        [HttpGet("bills")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> GetBills()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher profile not found." });
            }

            var bills = await _context.Bills
                .Where(b => b.TeacherId == teacher.TeacherId)
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.Month)
                .Select(b => new
                {
                    billId = b.BillId,
                    month = b.Month,
                    year = b.Year,
                    totalBill = b.TotalBill,
                    foodBill = b.FoodBill,
                    waterBill = b.WaterBill,
                    totalMealsConsumed = b.TotalMealsConsumed,
                    isPaid = b.IsPaid,
                    generatedDate = b.GeneratedDate,
                    paidDate = b.PaidDate,
                    paymentMethod = b.PaymentMethod
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                count = bills.Count,
                data = bills
            });
        }

        // Get menu items
        [HttpGet("menu")]
        public async Task<IActionResult> GetMenu([FromQuery] string? dayOfWeek)
        {
            var query = _context.MenuItems.Where(m => m.IsActive);

            if (!string.IsNullOrEmpty(dayOfWeek))
            {
                query = query.Where(m => m.DayOfWeek == dayOfWeek);
            }

            var menuItems = await query
                .Select(m => new
                {
                    menuItemId = m.MenuItemId,
                    itemName = m.ItemName,
                    description = m.Description,
                    mealType = m.MealType,
                    dayOfWeek = m.DayOfWeek,
                    ratePerServing = m.RatePerServing
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                count = menuItems.Count,
                data = menuItems
            });
        }

        // Get statistics (Admin only)
        [HttpGet("statistics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStatistics()
        {
            var totalTeachers = await _context.Teachers.CountAsync();
            var totalUsers = await _context.Users.CountAsync();
            var todayAttendance = await _context.Attendances
                .CountAsync(a => a.Date.Date == DateTime.Today);
            var pendingDisputes = await _context.AttendanceDisputes
                .CountAsync(d => d.Status == "Pending");
            var unpaidBills = await _context.Bills
                .CountAsync(b => !b.IsPaid);

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalTeachers,
                    totalUsers,
                    todayAttendance,
                    pendingDisputes,
                    unpaidBills
                }
            });
        }
    }
}
