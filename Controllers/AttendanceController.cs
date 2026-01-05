using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessManagementSystem.Data;
using MessManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MessManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = selectedDate;
            
            Console.WriteLine($"=== Index Action Called ===");
            Console.WriteLine($"Selected Date: {selectedDate:yyyy-MM-dd}");

            // Clear any cached data and force fresh query
            _context.ChangeTracker.Clear();

            var attendances = await _context.Attendances
                .Include(a => a.Teacher)
                .Where(a => a.Date.Date == selectedDate.Date)
                .OrderBy(a => a.Teacher!.FullName)
                .AsNoTracking()
                .ToListAsync();
                
            Console.WriteLine($"Found {attendances.Count} attendance records for {selectedDate:yyyy-MM-dd}");

            var allTeachers = await _context.Teachers
                .Where(t => t.IsActive)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.AllTeachers = allTeachers;

            return View(attendances);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttendance(int teacherId, DateTime date, bool breakfast, bool lunch, bool dinner)
        {
            Console.WriteLine($"=== MarkAttendance Called ===");
            Console.WriteLine($"TeacherId: {teacherId}, Date: {date:yyyy-MM-dd}");
            Console.WriteLine($"Breakfast: {breakfast}, Lunch: {lunch}, Dinner: {dinner}");
            
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers.FindAsync(teacherId);

            // If no meals are selected, show warning message
            if (!breakfast && !lunch && !dinner)
            {
                Console.WriteLine("ERROR: No meals selected!");
                TempData["Error"] = $"⚠️ Please select at least one meal for {teacher?.FullName}. No attendance was saved.";
                return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
            }

            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.TeacherId == teacherId && a.Date.Date == date.Date);

            Attendance savedAttendance;
            
            if (existingAttendance != null)
            {
                existingAttendance.BreakfastTaken = breakfast;
                existingAttendance.LunchTaken = lunch;
                existingAttendance.DinnerTaken = dinner;
                existingAttendance.RecordedDate = DateTime.Now;
                existingAttendance.RecordedBy = userId;
                savedAttendance = existingAttendance;
            }
            else
            {
                savedAttendance = new Attendance
                {
                    TeacherId = teacherId,
                    Date = date.Date,
                    BreakfastTaken = false,
                    LunchTaken = lunch,
                    DinnerTaken = dinner,
                    RecordedBy = userId,
                    RecordedDate = DateTime.Now
                };

                _context.Attendances.Add(savedAttendance);
            }

            await _context.SaveChangesAsync();
            
            Console.WriteLine($"SUCCESS: Attendance saved to database!");
            Console.WriteLine($"AttendanceId: {savedAttendance.AttendanceId}");

            var mealsList = new List<string>();
            if (lunch) mealsList.Add("Lunch");
            if (dinner) mealsList.Add("Dinner");
            
            var mealsText = string.Join(", ", mealsList);
            
            TempData["Success"] = $"✅ Attendance saved for {teacher?.FullName}: {mealsText} on {date.ToString("MMM dd, yyyy")}";
            
            Console.WriteLine($"Redirecting to Index with date: {date:yyyy-MM-dd}");
            
            // Add timestamp to prevent caching
            return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd"), t = DateTime.Now.Ticks });
        }

        public async Task<IActionResult> Report(int? month, int? year)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get all active teachers
            var allTeachers = await _context.Teachers
                .Where(t => t.IsActive)
                .ToListAsync();

            var attendances = await _context.Attendances
                .Include(a => a.Teacher)
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .ToListAsync();

            // Daily summary - which dates have attendance marked
            var dailySummary = attendances
                .GroupBy(a => a.Date.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TeachersCount = g.Select(a => a.TeacherId).Distinct().Count(),
                    TotalMeals = g.Sum(a => (a.BreakfastTaken ? 1 : 0) + (a.LunchTaken ? 1 : 0) + (a.DinnerTaken ? 1 : 0))
                })
                .OrderBy(d => d.Date)
                .ToList();

            ViewBag.DailySummary = dailySummary;

            // Create report for all active teachers (including those with zero attendance)
            var report = allTeachers
                .Select(t => new
                {
                    TeacherId = t.TeacherId,
                    TeacherName = t.FullName,
                    TotalBreakfast = 0,
                    TotalLunch = attendances.Where(a => a.TeacherId == t.TeacherId).Sum(a => a.LunchTaken ? 1 : 0),
                    TotalDinner = attendances.Where(a => a.TeacherId == t.TeacherId).Sum(a => a.DinnerTaken ? 1 : 0),
                    TotalMeals = attendances.Where(a => a.TeacherId == t.TeacherId).Sum(a => (a.LunchTaken ? 1 : 0) + (a.DinnerTaken ? 1 : 0))
                })
                .OrderBy(r => r.TeacherName)
                .ToList();

            return View(report);
        }

        // GET: Attendance/Edit/5
        public async Task<IActionResult> Edit(int id, DateTime? returnDate)
        {
            var attendance = await _context.Attendances
                .Include(a => a.Teacher)
                .FirstOrDefaultAsync(a => a.AttendanceId == id);

            if (attendance == null)
            {
                TempData["Error"] = "Attendance record not found.";
                return RedirectToAction(nameof(Index), new { date = returnDate });
            }

            ViewBag.ReturnDate = returnDate;
            return View(attendance);
        }

        // POST: Attendance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, bool breakfast, bool lunch, bool dinner, DateTime? returnDate)
        {
            var attendance = await _context.Attendances
                .Include(a => a.Teacher)
                .FirstOrDefaultAsync(a => a.AttendanceId == id);

            if (attendance == null)
            {
                TempData["Error"] = "Attendance record not found.";
                return RedirectToAction(nameof(Index), new { date = returnDate });
            }

            // Check if at least one meal is selected
            if (!breakfast && !lunch && !dinner)
            {
                TempData["Error"] = "⚠️ Please select at least one meal. To remove all meals, use the Delete option instead.";
                return RedirectToAction(nameof(Edit), new { id, returnDate });
            }

            var teacherName = attendance.Teacher?.FullName;
            var attendanceDate = attendance.Date;

            // Store original values to calculate adjustments
            var oldBreakfast = attendance.BreakfastTaken;
            var oldLunch = attendance.LunchTaken;
            var oldDinner = attendance.DinnerTaken;

            // Update attendance
            attendance.BreakfastTaken = breakfast;
            attendance.LunchTaken = lunch;
            attendance.DinnerTaken = dinner;
            attendance.RecordedDate = DateTime.Now;
            attendance.RecordedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            // Check if there's a bill for this attendance
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.TeacherId == attendance.TeacherId 
                    && b.Month == attendance.Date.Month 
                    && b.Year == attendance.Date.Year);

            if (bill != null)
            {
                var config = await _context.BillingConfigurations.FirstOrDefaultAsync();
                if (config != null)
                {
                    decimal adjustment = 0;
                    int mealAdjustment = 0;

                    // Calculate what was added
                    if (breakfast && !oldBreakfast)
                    {
                        adjustment += config.DefaultBreakfastRate;
                        mealAdjustment++;
                    }
                    if (lunch && !oldLunch)
                    {
                        adjustment += config.DefaultLunchRate;
                        mealAdjustment++;
                    }
                    if (dinner && !oldDinner)
                    {
                        adjustment += config.DefaultDinnerRate;
                        mealAdjustment++;
                    }

                    // Calculate what was removed
                    if (!breakfast && oldBreakfast)
                    {
                        adjustment -= config.DefaultBreakfastRate;
                        mealAdjustment--;
                    }
                    if (!lunch && oldLunch)
                    {
                        adjustment -= config.DefaultLunchRate;
                        mealAdjustment--;
                    }
                    if (!dinner && oldDinner)
                    {
                        adjustment -= config.DefaultDinnerRate;
                        mealAdjustment--;
                    }

                    // Update bill
                    bill.FoodBill += adjustment;
                    bill.TotalBill += adjustment;
                    bill.TotalMealsConsumed += mealAdjustment;

                    if (bill.IsPaid)
                    {
                        // Adjust credit/balance for paid bills
                        bill.UnpaidBalance += adjustment; // Positive = additional charge, Negative = credit
                    }
                    else
                    {
                        bill.UnpaidBalance += adjustment;
                    }
                }
            }

            await _context.SaveChangesAsync();

            var mealsList = new List<string>();
            if (breakfast) mealsList.Add("Breakfast");
            if (lunch) mealsList.Add("Lunch");
            if (dinner) mealsList.Add("Dinner");
            var mealsText = string.Join(", ", mealsList);

            TempData["Success"] = $"✅ Attendance updated for {teacherName} on {attendanceDate:MMM dd, yyyy}. Meals: {mealsText}";
            return RedirectToAction(nameof(Index), new { date = returnDate });
        }

        // POST: Attendance/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, DateTime? returnDate)
        {
            var attendance = await _context.Attendances
                .Include(a => a.Teacher)
                .FirstOrDefaultAsync(a => a.AttendanceId == id);

            if (attendance == null)
            {
                TempData["Error"] = "Attendance record not found.";
                return RedirectToAction(nameof(Index), new { date = returnDate });
            }

            var teacherName = attendance.Teacher?.FullName;
            var attendanceDate = attendance.Date;

            // Check if there's a bill for this attendance
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.TeacherId == attendance.TeacherId 
                    && b.Month == attendance.Date.Month 
                    && b.Year == attendance.Date.Year);

            if (bill != null)
            {
                // Get billing configuration
                var config = await _context.BillingConfigurations.FirstOrDefaultAsync();
                if (config != null)
                {
                    // Calculate deductions
                    decimal deduction = 0;
                    int mealsReduced = 0;

                    if (attendance.BreakfastTaken)
                    {
                        deduction += config.DefaultBreakfastRate;
                        mealsReduced++;
                    }
                    if (attendance.LunchTaken)
                    {
                        deduction += config.DefaultLunchRate;
                        mealsReduced++;
                    }
                    if (attendance.DinnerTaken)
                    {
                        deduction += config.DefaultDinnerRate;
                        mealsReduced++;
                    }

                    // Update bill regardless of payment status
                    bill.FoodBill -= deduction;
                    bill.TotalBill -= deduction;
                    bill.TotalMealsConsumed -= mealsReduced;

                    if (bill.IsPaid)
                    {
                        // If bill is paid, create a credit for next bill
                        bill.UnpaidBalance = -deduction; // Negative balance = credit
                        TempData["Success"] = $"Attendance deleted for {teacherName} on {attendanceDate:MMM dd, yyyy}. Credit of Rs. {deduction:N2} will be applied to next bill.";
                    }
                    else
                    {
                        // If unpaid, just reduce the current bill
                        bill.UnpaidBalance -= deduction;
                        TempData["Success"] = $"Attendance deleted for {teacherName} on {attendanceDate:MMM dd, yyyy}. Bill reduced by Rs. {deduction:N2}.";
                    }
                }
            }

            // Delete the attendance record
            _context.Attendances.Remove(attendance);
            await _context.SaveChangesAsync();

            if (bill == null)
            {
                TempData["Success"] = $"Attendance deleted for {teacherName} on {attendanceDate:MMM dd, yyyy}.";
            }

            return RedirectToAction(nameof(Index), new { date = returnDate });
        }

        // POST: Attendance/ClearAllAttendance
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAllAttendance()
        {
            try
            {
                // Get all attendance records
                var allAttendances = await _context.Attendances.ToListAsync();
                var attendanceCount = allAttendances.Count;

                if (attendanceCount > 0)
                {
                    // Get all dispute records related to these attendances
                    var attendanceIds = allAttendances.Select(a => a.AttendanceId).ToList();
                    var allDisputes = await _context.AttendanceDisputes
                        .Where(d => attendanceIds.Contains(d.AttendanceId))
                        .ToListAsync();

                    // Remove all disputes first
                    if (allDisputes.Any())
                    {
                        _context.AttendanceDisputes.RemoveRange(allDisputes);
                    }

                    // Remove all attendance records
                    _context.Attendances.RemoveRange(allAttendances);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"✅ Successfully cleared {attendanceCount} attendance record(s) and {allDisputes.Count} related dispute(s) from the system.";
                }
                else
                {
                    TempData["Info"] = "No attendance records found to clear.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Error clearing attendance records: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
