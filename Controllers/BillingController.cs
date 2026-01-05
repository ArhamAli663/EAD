using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessManagementSystem.Data;
using MessManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MessManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var bills = await _context.Bills
                .Include(b => b.Teacher)
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.Month)
                .Take(50)
                .ToListAsync();

            return View(bills);
        }

        // Helper method to calculate food bill from menu prices
        private async Task<decimal> CalculateFoodBillFromMenu(List<Attendance> attendances)
        {
            decimal totalFoodBill = 0;

            foreach (var attendance in attendances)
            {
                var dayOfWeek = attendance.Date.DayOfWeek.ToString();

                if (attendance.LunchTaken)
                {
                    var lunchItem = await _context.MenuItems
                        .Where(m => m.DayOfWeek == dayOfWeek && m.MealType == "Lunch" && m.IsActive)
                        .FirstOrDefaultAsync();
                    totalFoodBill += lunchItem?.RatePerServing ?? 0;
                }

                if (attendance.DinnerTaken)
                {
                    var dinnerItem = await _context.MenuItems
                        .Where(m => m.DayOfWeek == dayOfWeek && m.MealType == "Dinner" && m.IsActive)
                        .FirstOrDefaultAsync();
                    totalFoodBill += dinnerItem?.RatePerServing ?? 0;
                }
            }

            return totalFoodBill;
        }

        public async Task<IActionResult> Generate(int? month, int? year, int? teacherId)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            // Get all active teachers for dropdown
            var teachers = await _context.Teachers
                .Where(t => t.IsActive)
                .OrderBy(t => t.FullName)
                .ToListAsync();
            ViewBag.Teachers = teachers;
            ViewBag.SelectedTeacherId = teacherId;

            // If no POST request (just loading the page), return the view
            if (!Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return View();
            }

            // Validate teacher selection
            if (teacherId == null || teacherId == 0)
            {
                TempData["Error"] = "Please select a teacher to generate the bill.";
                return View();
            }

            var config = await _context.BillingConfigurations.FirstOrDefaultAsync();

            if (config == null)
            {
                TempData["Error"] = "Billing configuration not found. Please configure billing first.";
                return RedirectToAction(nameof(Configuration));
            }

            // Get selected teacher
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.TeacherId == teacherId && t.IsActive);

            if (teacher == null)
            {
                TempData["Error"] = "Teacher not found or inactive.";
                return View();
            }

            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get attendance for selected teacher only
            var attendances = await _context.Attendances
                .Where(a => a.TeacherId == teacherId && a.Date >= startDate && a.Date <= endDate)
                .ToListAsync();

            var lunchCount = attendances.Count(a => a.LunchTaken);
            var dinnerCount = attendances.Count(a => a.DinnerTaken);

            // Calculate food bill using menu prices
            var foodBill = await CalculateFoodBillFromMenu(attendances);

            var totalMeals = lunchCount + dinnerCount;

            // Calculate water bill per teacher (shared equally)
            var activeTeacherCount = await _context.Teachers.CountAsync(t => t.IsActive);
            var waterBillPerTeacher = activeTeacherCount > 0 ? config.MonthlyWaterBillTotal / activeTeacherCount : 0;

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            // Check if bill already exists
            var existingBill = await _context.Bills
                .FirstOrDefaultAsync(b => b.TeacherId == teacherId && 
                                        b.Month == selectedMonth && 
                                        b.Year == selectedYear);

            // Prevent generating bill if teacher has already paid for this month
            if (existingBill != null && existingBill.IsPaid)
            {
                TempData["Error"] = $"❌ Cannot generate bill! Teacher {teacher.FullName} has already paid the bill for {new DateTime(selectedYear, selectedMonth, 1):MMMM yyyy}. Bills cannot be regenerated once paid.";
                return View();
            }

            if (existingBill != null)
            {
                existingBill.FoodBill = foodBill;
                existingBill.WaterBill = waterBillPerTeacher;
                existingBill.TotalBill = foodBill + waterBillPerTeacher + existingBill.UnpaidBalance;
                existingBill.TotalMealsConsumed = totalMeals;
                existingBill.GeneratedDate = DateTime.Now;
                existingBill.GeneratedBy = userId;
                
                await _context.SaveChangesAsync();

                // Clear attendance records after bill generation (only if no pending disputes)
                if (attendances.Any())
                {
                    // Check if any attendance has pending disputes
                    var attendanceIds = attendances.Select(a => a.AttendanceId).ToList();
                    var hasDisputes = await _context.AttendanceDisputes
                        .AnyAsync(d => attendanceIds.Contains(d.AttendanceId) && d.Status == "Pending");

                    if (!hasDisputes)
                    {
                        // Delete resolved disputes before clearing attendance
                        var resolvedDisputes = await _context.AttendanceDisputes
                            .Where(d => attendanceIds.Contains(d.AttendanceId) && d.Status != "Pending")
                            .ToListAsync();
                        if (resolvedDisputes.Any())
                        {
                            _context.AttendanceDisputes.RemoveRange(resolvedDisputes);
                            await _context.SaveChangesAsync();
                        }

                        _context.Attendances.RemoveRange(attendances);
                        await _context.SaveChangesAsync();
                        TempData["Success"] = $"✅ Bill updated successfully for {teacher.FullName} - {new DateTime(selectedYear, selectedMonth, 1):MMMM yyyy}! Attendance records cleared.";
                    }
                    else
                    {
                        TempData["Success"] = $"✅ Bill updated successfully for {teacher.FullName} - {new DateTime(selectedYear, selectedMonth, 1):MMMM yyyy}! Note: Attendance records with pending disputes were not cleared.";
                    }
                }
                else
                {
                    TempData["Success"] = $"✅ Bill updated successfully for {teacher.FullName} - {new DateTime(selectedYear, selectedMonth, 1):MMMM yyyy}!";
                }
            }
            else
            {
                // Check for previous unpaid balance
                var previousBill = await _context.Bills
                    .Where(b => b.TeacherId == teacherId && !b.IsPaid)
                    .OrderByDescending(b => b.Year)
                    .ThenByDescending(b => b.Month)
                    .FirstOrDefaultAsync();

                var unpaidBalance = previousBill?.UnpaidBalance ?? 0;

                var bill = new Bill
                {
                    TeacherId = teacherId.Value,
                    Teacher = teacher,
                    Month = selectedMonth,
                    Year = selectedYear,
                    FoodBill = foodBill,
                    WaterBill = waterBillPerTeacher,
                    TotalBill = foodBill + waterBillPerTeacher + unpaidBalance,
                    UnpaidBalance = unpaidBalance,
                    TotalMealsConsumed = totalMeals,
                    GeneratedBy = userId,
                    IsPaid = false
                };

                _context.Bills.Add(bill);
                await _context.SaveChangesAsync();

                // Clear attendance records after bill generation (only if no pending disputes)
                if (attendances.Any())
                {
                    // Check if any attendance has pending disputes
                    var attendanceIds = attendances.Select(a => a.AttendanceId).ToList();
                    var hasDisputes = await _context.AttendanceDisputes
                        .AnyAsync(d => attendanceIds.Contains(d.AttendanceId) && d.Status == "Pending");

                    if (!hasDisputes)
                    {
                        // Delete resolved disputes before clearing attendance
                        var resolvedDisputes = await _context.AttendanceDisputes
                            .Where(d => attendanceIds.Contains(d.AttendanceId) && d.Status != "Pending")
                            .ToListAsync();
                        if (resolvedDisputes.Any())
                        {
                            _context.AttendanceDisputes.RemoveRange(resolvedDisputes);
                            await _context.SaveChangesAsync();
                        }

                        _context.Attendances.RemoveRange(attendances);
                        await _context.SaveChangesAsync();
                        TempData["Success"] = $"✅ Bill generated successfully for {teacher.FullName} - {new DateTime(selectedYear, selectedMonth, 1):MMMM yyyy}! Attendance records cleared.";
                    }
                    else
                    {
                        TempData["Success"] = $"✅ Bill generated successfully for {teacher.FullName} - {new DateTime(selectedYear, selectedMonth, 1):MMMM yyyy}! Note: Attendance records with pending disputes were not cleared.";
                    }
                }
                else
                {
                    TempData["Success"] = $"✅ Bill generated successfully for {teacher.FullName} - {new DateTime(selectedYear, selectedMonth, 1):MMMM yyyy}!";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Configuration()
        {
            var config = await _context.BillingConfigurations.FirstOrDefaultAsync();
            var menuItems = await _context.MenuItems
                .Where(m => m.IsActive)
                .OrderBy(m => m.DayOfWeek)
                .ThenBy(m => m.MealType)
                .ToListAsync();

            if (config == null)
            {
                config = new BillingConfiguration();
            }

            return View((config, menuItems));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateConfiguration(BillingConfiguration config)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            config.UpdatedBy = userId;
            config.LastUpdated = DateTime.Now;

            var existingConfig = await _context.BillingConfigurations.FirstOrDefaultAsync();

            if (existingConfig != null)
            {
                existingConfig.MonthlyWaterBillTotal = config.MonthlyWaterBillTotal;
                existingConfig.DefaultBreakfastRate = config.DefaultBreakfastRate;
                existingConfig.DefaultLunchRate = config.DefaultLunchRate;
                existingConfig.DefaultDinnerRate = config.DefaultDinnerRate;
                existingConfig.LastUpdated = config.LastUpdated;
                existingConfig.UpdatedBy = config.UpdatedBy;
            }
            else
            {
                _context.BillingConfigurations.Add(config);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Billing configuration updated successfully!";
            return RedirectToAction(nameof(Configuration));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMenuItem(MenuItem menuItem)
        {
            menuItem.IsActive = true;
            menuItem.CreatedDate = DateTime.Now;
            
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Menu item '{menuItem.ItemName}' added successfully!";
            return RedirectToAction(nameof(Configuration));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMenuItem(MenuItem menuItem)
        {
            var existingItem = await _context.MenuItems.FindAsync(menuItem.MenuItemId);
            
            if (existingItem != null)
            {
                existingItem.ItemName = menuItem.ItemName;
                existingItem.Description = menuItem.Description;
                existingItem.MealType = menuItem.MealType;
                existingItem.DayOfWeek = menuItem.DayOfWeek;
                existingItem.RatePerServing = menuItem.RatePerServing;
                
                await _context.SaveChangesAsync();
                TempData["Success"] = $"✅ Menu item '{menuItem.ItemName}' updated successfully!";
            }
            else
            {
                TempData["Error"] = "Menu item not found.";
            }

            return RedirectToAction(nameof(Configuration));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMenuItem(int menuItemId)
        {
            var menuItem = await _context.MenuItems.FindAsync(menuItemId);
            
            if (menuItem != null)
            {
                _context.MenuItems.Remove(menuItem);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"✅ Menu item '{menuItem.ItemName}' deleted successfully!";
            }
            else
            {
                TempData["Error"] = "Menu item not found.";
            }

            return RedirectToAction(nameof(Configuration));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Teacher)
                .FirstOrDefaultAsync(b => b.BillId == id);
                
            if (bill != null)
            {
                // First, delete all attendance records for this bill's month
                var startDate = new DateTime(bill.Year, bill.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);
                
                var attendancesToDelete = await _context.Attendances
                    .Where(a => a.TeacherId == bill.TeacherId && 
                               a.Date >= startDate && 
                               a.Date <= endDate)
                    .ToListAsync();
                
                if (attendancesToDelete.Any())
                {
                    _context.Attendances.RemoveRange(attendancesToDelete);
                }
                
                // Then mark bill as paid
                bill.IsPaid = true;
                bill.PaidDate = DateTime.Now;
                
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ Bill marked as paid for {bill.Teacher?.FullName}! All attendance records for {System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(bill.Month)} {bill.Year} have been removed.";
            }
            else
            {
                TempData["Error"] = "Bill not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAllBills(int? month, int? year)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var config = await _context.BillingConfigurations.FirstOrDefaultAsync();

            if (config == null)
            {
                TempData["Error"] = "Billing configuration not found. Please configure billing first.";
                return RedirectToAction(nameof(Configuration));
            }

            // Get all active teachers
            var teachers = await _context.Teachers
                .Where(t => t.IsActive)
                .ToListAsync();

            if (!teachers.Any())
            {
                TempData["Error"] = "No active teachers found.";
                return RedirectToAction(nameof(Generate));
            }

            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var activeTeacherCount = teachers.Count;
            var waterBillPerTeacher = activeTeacherCount > 0 ? config.MonthlyWaterBillTotal / activeTeacherCount : 0;

            int generatedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;
            List<string> skippedTeachers = new List<string>();

            foreach (var teacher in teachers)
            {
                // Check if bill already exists and is paid
                var existingBill = await _context.Bills
                    .FirstOrDefaultAsync(b => b.TeacherId == teacher.TeacherId &&
                                            b.Month == selectedMonth &&
                                            b.Year == selectedYear);

                if (existingBill != null && existingBill.IsPaid)
                {
                    skippedCount++;
                    skippedTeachers.Add(teacher.FullName);
                    continue;
                }

                // Get attendance for this teacher
                var attendances = await _context.Attendances
                    .Where(a => a.TeacherId == teacher.TeacherId && a.Date >= startDate && a.Date <= endDate)
                    .ToListAsync();

                var lunchCount = attendances.Count(a => a.LunchTaken);
                var dinnerCount = attendances.Count(a => a.DinnerTaken);

                // Calculate food bill using menu prices
                var foodBill = await CalculateFoodBillFromMenu(attendances);

                var totalMeals = lunchCount + dinnerCount;

                if (existingBill != null)
                {
                    // Update existing bill
                    existingBill.FoodBill = foodBill;
                    existingBill.WaterBill = waterBillPerTeacher;
                    existingBill.TotalBill = foodBill + waterBillPerTeacher + existingBill.UnpaidBalance;
                    existingBill.TotalMealsConsumed = totalMeals;
                    existingBill.GeneratedDate = DateTime.Now;
                    existingBill.GeneratedBy = userId;
                    updatedCount++;
                }
                else
                {
                    // Check for previous unpaid balance
                    var previousBill = await _context.Bills
                        .Where(b => b.TeacherId == teacher.TeacherId && !b.IsPaid)
                        .OrderByDescending(b => b.Year)
                        .ThenByDescending(b => b.Month)
                        .FirstOrDefaultAsync();

                    var unpaidBalance = previousBill?.UnpaidBalance ?? 0;

                    // Create new bill
                    var bill = new Bill
                    {
                        TeacherId = teacher.TeacherId,
                        Teacher = teacher,
                        Month = selectedMonth,
                        Year = selectedYear,
                        FoodBill = foodBill,
                        WaterBill = waterBillPerTeacher,
                        TotalBill = foodBill + waterBillPerTeacher + unpaidBalance,
                        UnpaidBalance = unpaidBalance,
                        TotalMealsConsumed = totalMeals,
                        GeneratedBy = userId,
                        IsPaid = false
                    };

                    _context.Bills.Add(bill);
                    generatedCount++;
                }

                // Clear attendance records after bill generation (only if no pending disputes)
                if (attendances.Any())
                {
                    // Check if any attendance has pending disputes
                    var attendanceIds = attendances.Select(a => a.AttendanceId).ToList();
                    var hasDisputes = await _context.AttendanceDisputes
                        .AnyAsync(d => attendanceIds.Contains(d.AttendanceId) && d.Status == "Pending");

                    if (!hasDisputes)
                    {
                        // Delete resolved disputes before clearing attendance
                        var resolvedDisputes = await _context.AttendanceDisputes
                            .Where(d => attendanceIds.Contains(d.AttendanceId) && d.Status != "Pending")
                            .ToListAsync();
                        if (resolvedDisputes.Any())
                        {
                            _context.AttendanceDisputes.RemoveRange(resolvedDisputes);
                        }

                        _context.Attendances.RemoveRange(attendances);
                    }
                }
            }

            await _context.SaveChangesAsync();

            var periodText = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");
            var successMessage = $"✅ Bulk bill generation completed for {periodText}! ";
            
            if (generatedCount > 0)
                successMessage += $"{generatedCount} new bill(s) generated. ";
            
            if (updatedCount > 0)
                successMessage += $"{updatedCount} existing bill(s) updated. ";
            
            if (skippedCount > 0)
                successMessage += $"{skippedCount} teacher(s) already paid ({string.Join(", ", skippedTeachers)}). ";
            
            successMessage += "Attendance records (without disputes) cleared.";

            TempData["Success"] = successMessage;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Teacher)
                .FirstOrDefaultAsync(b => b.BillId == id);

            if (bill == null)
            {
                TempData["Error"] = "Bill not found.";
                return RedirectToAction(nameof(Index));
            }

            // Only allow deletion of paid bills
            if (!bill.IsPaid)
            {
                TempData["Error"] = "Only paid bills can be deleted. Please mark the bill as paid first.";
                return RedirectToAction(nameof(Index));
            }

            var billInfo = $"{bill.Teacher.FullName} - {new DateTime(bill.Year, bill.Month, 1):MMMM yyyy}";

            _context.Bills.Remove(bill);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Bill deleted successfully for {billInfo}!";

            return RedirectToAction(nameof(Index));
        }
    }
}
