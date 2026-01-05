using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessManagementSystem.Data;
using MessManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MessManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string filter = "")
        {
            // Since we permanently delete teachers, all teachers in DB are active
            var totalTeachers = await _context.Teachers.CountAsync();
            ViewBag.TotalTeachers = totalTeachers;
            ViewBag.ActiveTeachers = totalTeachers;
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TodayAttendance = await _context.Attendances
                .CountAsync(a => a.Date.Date == DateTime.Today);
            // Removed PendingDisputes from dashboard

            var recentAttendance = await _context.Attendances
                .Include(a => a.Teacher)
                .OrderByDescending(a => a.RecordedDate)
                .Take(10)
                .ToListAsync();

            // Get filtered data based on clicked stat
            ViewBag.Filter = filter;
            ViewBag.FilteredTeachers = new List<Teacher>();

            if (!string.IsNullOrEmpty(filter))
            {
                switch (filter.ToLower())
                {
                    case "total":
                    case "active":
                        ViewBag.FilteredTeachers = await _context.Teachers
                            .Include(t => t.User)
                            .OrderBy(t => t.FullName)
                            .ToListAsync();
                        ViewBag.FilterTitle = "All Teachers";
                        break;
                    case "users":
                        ViewBag.FilteredUsers = await _context.Users
                            .OrderBy(u => u.Username)
                            .ToListAsync();
                        ViewBag.FilterTitle = "All Users";
                        break;
                    case "attendance":
                        ViewBag.FilteredAttendance = await _context.Attendances
                            .Include(a => a.Teacher)
                            .Where(a => a.Date.Date == DateTime.Today)
                            .OrderBy(a => a.Teacher!.FullName)
                            .ToListAsync();
                        ViewBag.FilterTitle = "Today's Attendance";
                        break;
                    // Removed disputes case
                }
            }

            return View(recentAttendance);
        }

        // GET: Admin/ManageTeachers
        public async Task<IActionResult> ManageTeachers()
        {
            var teachers = await _context.Teachers
                .Include(t => t.User)
                .OrderBy(t => t.FullName)
                .ToListAsync();
            return View(teachers);
        }

        // GET: Admin/CreateTeacher
        public IActionResult CreateTeacher()
        {
            return View();
        }

        // POST: Admin/CreateTeacher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeacher(Teacher teacher, string username, string password)
        {
            // Remove validation errors for navigation properties
            ModelState.Remove("User");
            ModelState.Remove("Attendances");
            ModelState.Remove("Bills");

            // Validate required fields manually
            if (string.IsNullOrWhiteSpace(username))
            {
                ViewBag.Error = "Username is required.";
                return View(teacher);
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                return View(teacher);
            }

            if (ModelState.IsValid)
            {
                // Check if username already exists
                if (await _context.Users.AnyAsync(u => u.Username == username))
                {
                    ViewBag.Error = "Username already exists. Please choose a different username.";
                    return View(teacher);
                }

                // Check if email already exists
                if (await _context.Teachers.AnyAsync(t => t.Email == teacher.Email))
                {
                    ViewBag.Error = "Email already exists. Please use a different email.";
                    return View(teacher);
                }

                // Create user account for teacher
                var user = new User
                {
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = "Teacher",
                    MustChangePassword = true,
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Link teacher to user
                teacher.UserId = user.UserId;
                teacher.JoiningDate = DateTime.Now;
                teacher.IsActive = true;

                _context.Teachers.Add(teacher);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Teacher '{teacher.FullName}' has been successfully added!";
                return RedirectToAction(nameof(ManageTeachers));
            }

            // Show validation errors
            ViewBag.Error = "Please fix the validation errors and try again.";
            return View(teacher);
        }

        // GET: Admin/EditTeacher/5
        public async Task<IActionResult> EditTeacher(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == id);

            if (teacher == null)
            {
                return NotFound();
            }

            return View(teacher);
        }

        // POST: Admin/EditTeacher/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTeacher(int id, Teacher teacher)
        {
            if (id != teacher.TeacherId)
            {
                return NotFound();
            }

            // Remove validation errors for navigation properties
            ModelState.Remove("User");
            ModelState.Remove("Attendances");
            ModelState.Remove("Bills");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(teacher);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Teacher '{teacher.FullName}' has been successfully updated!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await TeacherExists(teacher.TeacherId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(ManageTeachers));
            }
            return View(teacher);
        }

        // POST: Admin/DeleteTeacher/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeacher(int id)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .Include(t => t.Attendances)
                .Include(t => t.Bills)
                .FirstOrDefaultAsync(t => t.TeacherId == id);

            if (teacher != null)
            {
                string teacherName = teacher.FullName;

                // Delete all related attendance records
                if (teacher.Attendances.Any())
                {
                    _context.Attendances.RemoveRange(teacher.Attendances);
                }

                // Delete all related bills
                if (teacher.Bills.Any())
                {
                    _context.Bills.RemoveRange(teacher.Bills);
                }

                // Delete the associated user account
                if (teacher.UserId != null && teacher.User != null)
                {
                    _context.Users.Remove(teacher.User);
                }

                // Delete the teacher record
                _context.Teachers.Remove(teacher);

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Teacher '{teacherName}' and all associated data have been permanently deleted!";
            }

            return RedirectToAction(nameof(ManageTeachers));
        }

        // GET: Admin/ViewTeacherDetails/5
        public async Task<IActionResult> ViewTeacherDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .Include(t => t.Attendances.OrderByDescending(a => a.Date).Take(30))
                .Include(t => t.Bills.OrderByDescending(b => b.Year).ThenByDescending(b => b.Month).Take(6))
                .FirstOrDefaultAsync(t => t.TeacherId == id);

            if (teacher == null)
            {
                return NotFound();
            }

            return View(teacher);
        }

        private async Task<bool> TeacherExists(int id)
        {
            return await _context.Teachers.AnyAsync(e => e.TeacherId == id);
        }

        // GET: Admin/ManageDisputes
        public async Task<IActionResult> ManageDisputes(string status = "Pending")
        {
            ViewBag.CurrentStatus = status;
            
            var disputes = await _context.AttendanceDisputes
                .Include(d => d.Attendance)
                .Include(d => d.Teacher)
                .Include(d => d.ResolvedByUser)
                .Where(d => d.Status == status)
                .OrderByDescending(d => d.ReportedDate)
                .ToListAsync();

            return View(disputes);
        }

        // GET: Admin/ViewDispute/5
        public async Task<IActionResult> ViewDispute(int id)
        {
            var dispute = await _context.AttendanceDisputes
                .Include(d => d.Attendance)
                .Include(d => d.Teacher)
                .Include(d => d.ResolvedByUser)
                .FirstOrDefaultAsync(d => d.DisputeId == id);

            if (dispute == null)
            {
                return NotFound();
            }

            // Get billing configuration for calculation
            var config = await _context.BillingConfigurations.FirstOrDefaultAsync();
            ViewBag.BillingConfig = config;

            return View(dispute);
        }

        // POST: Admin/ResolveDispute
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveDispute(int disputeId, string action, string? adminNotes, bool? removeBreakfast, bool? removeLunch, bool? removeDinner)
        {
            var dispute = await _context.AttendanceDisputes
                .Include(d => d.Attendance)
                .Include(d => d.Teacher)
                .FirstOrDefaultAsync(d => d.DisputeId == disputeId);

            if (dispute == null)
            {
                TempData["Error"] = "Dispute not found.";
                return RedirectToAction(nameof(ManageDisputes));
            }

            if (dispute.Status != "Pending")
            {
                TempData["Error"] = "This dispute has already been resolved.";
                return RedirectToAction(nameof(ManageDisputes));
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            if (action == "Approve")
            {
                // Check which meals to remove (CHECKED checkbox = true, UNCHECKED = null)
                bool shouldRemoveBreakfast = removeBreakfast == true;
                bool shouldRemoveLunch = removeLunch == true;
                bool shouldRemoveDinner = removeDinner == true;

                // Debug logging
                System.Diagnostics.Debug.WriteLine($"Meal Selection - Breakfast: {removeBreakfast}, Lunch: {removeLunch}, Dinner: {removeDinner}");
                System.Diagnostics.Debug.WriteLine($"Will Remove - Breakfast: {shouldRemoveBreakfast}, Lunch: {shouldRemoveLunch}, Dinner: {shouldRemoveDinner}");

                if (!shouldRemoveBreakfast && !shouldRemoveLunch && !shouldRemoveDinner)
                {
                    TempData["Error"] = "⚠️ Please select at least one meal to remove before approving.";
                    return RedirectToAction(nameof(ManageDisputes));
                }

                var attendance = dispute.Attendance;
                if (attendance != null)
                {
                    // Log original state
                    System.Diagnostics.Debug.WriteLine($"Original Attendance - Breakfast: {attendance.BreakfastTaken}, Lunch: {attendance.LunchTaken}, Dinner: {attendance.DinnerTaken}");
                    
                    // Recalculate bill for the teacher
                    var teacherId = attendance.TeacherId;
                    var month = attendance.Date.Month;
                    var year = attendance.Date.Year;

                    // Get the bill for that month
                    var bill = await _context.Bills
                        .FirstOrDefaultAsync(b => b.TeacherId == teacherId && b.Month == month && b.Year == year);

                    var config = await _context.BillingConfigurations.FirstOrDefaultAsync();
                    
                    decimal deduction = 0;
                    int mealsReduced = 0;
                    List<string> removedMeals = new List<string>();

                    // Calculate deductions only for selected meals
                    if (shouldRemoveBreakfast && attendance.BreakfastTaken && config != null)
                    {
                        deduction += config.DefaultBreakfastRate;
                        mealsReduced++;
                        removedMeals.Add("Breakfast");
                        attendance.BreakfastTaken = false;
                        System.Diagnostics.Debug.WriteLine("Removing Breakfast");
                    }
                    if (shouldRemoveLunch && attendance.LunchTaken && config != null)
                    {
                        deduction += config.DefaultLunchRate;
                        mealsReduced++;
                        removedMeals.Add("Lunch");
                        attendance.LunchTaken = false;
                        System.Diagnostics.Debug.WriteLine("Removing Lunch");
                    }
                    if (shouldRemoveDinner && attendance.DinnerTaken && config != null)
                    {
                        deduction += config.DefaultDinnerRate;
                        mealsReduced++;
                        removedMeals.Add("Dinner");
                        attendance.DinnerTaken = false;
                        System.Diagnostics.Debug.WriteLine("Removing Dinner");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Modified Attendance - Breakfast: {attendance.BreakfastTaken}, Lunch: {attendance.LunchTaken}, Dinner: {attendance.DinnerTaken}");
                    System.Diagnostics.Debug.WriteLine($"Removed Meals: {string.Join(", ", removedMeals)}");

                    // Update bill if exists and not paid
                    if (bill != null && !bill.IsPaid)
                    {
                        bill.FoodBill -= deduction;
                        bill.TotalBill -= deduction;
                        bill.UnpaidBalance -= deduction;
                        bill.TotalMealsConsumed -= mealsReduced;
                    }

                    // If all meals are removed, delete the entire attendance record
                    if (!attendance.BreakfastTaken && !attendance.LunchTaken && !attendance.DinnerTaken)
                    {
                        dispute.Attendance = null;
                        _context.Attendances.Remove(attendance);
                    }

                    var removedMealsText = string.Join(", ", removedMeals);
                    dispute.AdminNotes = $"{adminNotes}\n\n[System] Removed meals: {removedMealsText}. Bill adjusted by Rs. {deduction:N2}.";
                }

                dispute.Status = "Approved";
                dispute.ResolvedBy = userId;
                dispute.ResolvedDate = DateTime.Now;

                TempData["Success"] = $"✅ Dispute approved! Selected meals removed from {attendance?.Date.ToString("MMM dd, yyyy")} for {dispute.Teacher?.FullName}. Teacher has been notified.";
            }
            else if (action == "Reject")
            {
                dispute.Status = "Rejected";
                dispute.ResolvedBy = userId;
                dispute.ResolvedDate = DateTime.Now;
                dispute.AdminNotes = adminNotes;

                TempData["Success"] = $"❌ Dispute rejected. Attendance record for {dispute.Teacher?.FullName} on {dispute.Attendance?.Date.ToString("MMM dd, yyyy")} remains unchanged.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageDisputes));
        }
    }
}
