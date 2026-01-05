using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessManagementSystem.Data;
using MessManagementSystem.Models;
using MessManagementSystem.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MessManagementSystem.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeachersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TeachersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .Include(t => t.Bills)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound("Teacher profile not found.");
            }

            // Get current month attendance
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            var startDate = new DateTime(currentYear, currentMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            var monthlyAttendance = await _context.Attendances
                .Where(a => a.TeacherId == teacher.TeacherId && 
                           a.Date >= startDate && a.Date <= endDate)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            ViewBag.MonthlyAttendance = monthlyAttendance;
            ViewBag.TotalMeals = monthlyAttendance.Sum(a => 
                (a.LunchTaken ? 1 : 0) + (a.DinnerTaken ? 1 : 0));

            // Get unpaid bills
            ViewBag.UnpaidBills = await _context.Bills
                .Where(b => b.TeacherId == teacher.TeacherId && !b.IsPaid)
                .OrderBy(b => b.Year).ThenBy(b => b.Month)
                .ToListAsync();

            return View(teacher);
        }

        // GET: Teachers/ViewAttendance
        public async Task<IActionResult> ViewAttendance(int? month, int? year)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            var attendanceRecords = await _context.Attendances
                .Where(a => a.TeacherId == teacher.TeacherId && 
                           a.Date >= startDate && a.Date <= endDate)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.TeacherName = teacher.FullName;

            return View(attendanceRecords);
        }

        // GET: Teachers/VerifyAttendance
        public async Task<IActionResult> VerifyAttendance()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            // Get last 30 days attendance
            var startDate = DateTime.Now.AddDays(-30);
            var attendanceRecords = await _context.Attendances
                .Where(a => a.TeacherId == teacher.TeacherId && a.Date >= startDate)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            ViewBag.TeacherName = teacher.FullName;

            return View(attendanceRecords);
        }

        // GET: Teachers/ReportWrongAttendance
        public async Task<IActionResult> ReportWrongAttendance(int? attendanceId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            // Get last 30 days attendance with disputes
            var startDate = DateTime.Now.AddDays(-30);
            var attendances = await _context.Attendances
                .Where(a => a.TeacherId == teacher.TeacherId && a.Date >= startDate)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.SelectedAttendanceId = attendanceId;

            return View(attendances);
        }

        // POST: Teachers/ReportWrongAttendance
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportWrongAttendance(int attendanceId, string reason)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.TeacherId == teacher.TeacherId);

            if (attendance == null)
            {
                TempData["Error"] = "Attendance record not found.";
                return RedirectToAction(nameof(ReportWrongAttendance));
            }

            // Check if already reported
            var existingDispute = await _context.AttendanceDisputes
                .FirstOrDefaultAsync(d => d.AttendanceId == attendanceId && d.Status == "Pending");

            if (existingDispute != null)
            {
                TempData["Error"] = "You have already reported this attendance. Please wait for admin review.";
                return RedirectToAction(nameof(ReportWrongAttendance));
            }

            // Create new dispute
            var dispute = new AttendanceDispute
            {
                AttendanceId = attendanceId,
                TeacherId = teacher.TeacherId,
                Reason = reason,
                Status = "Pending",
                ReportedDate = DateTime.Now
            };

            _context.AttendanceDisputes.Add(dispute);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Your attendance issue has been reported successfully! Admin will review and respond soon.";
            return RedirectToAction(nameof(ReportWrongAttendance));
        }

        // GET: Teachers/ViewDisputeHistory
        public async Task<IActionResult> ViewDisputeHistory(string? status, bool showAll = false)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var disputesQuery = _context.AttendanceDisputes
                .Include(d => d.Attendance)
                .Include(d => d.ResolvedByUser)
                .Where(d => d.TeacherId == teacher.TeacherId);

            // By default, hide acknowledged disputes unless showAll is true
            if (!showAll)
            {
                disputesQuery = disputesQuery.Where(d => !d.IsAcknowledgedByTeacher);
            }

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                disputesQuery = disputesQuery.Where(d => d.Status == status);
            }

            var disputes = await disputesQuery
                .OrderByDescending(d => d.ReportedDate)
                .ToListAsync();

            ViewBag.SelectedStatus = status ?? "All";
            ViewBag.TeacherName = teacher.FullName;
            ViewBag.ShowAll = showAll;

            return View(disputes);
        }

        // POST: Teachers/AcknowledgeDispute/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeDispute(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var dispute = await _context.AttendanceDisputes
                .FirstOrDefaultAsync(d => d.DisputeId == id && d.TeacherId == teacher.TeacherId);

            if (dispute == null)
            {
                TempData["Error"] = "Dispute not found.";
                return RedirectToAction(nameof(ViewDisputeHistory));
            }

            // Only allow acknowledging resolved disputes
            if (dispute.Status == "Pending")
            {
                TempData["Error"] = "Cannot acknowledge a pending dispute. Please wait for admin review.";
                return RedirectToAction(nameof(ViewDisputeHistory));
            }

            dispute.IsAcknowledgedByTeacher = true;
            dispute.AcknowledgedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Dispute acknowledged and dismissed from your view.";
            return RedirectToAction(nameof(ViewDisputeHistory));
        }

        // GET: Teachers/ViewBills
        public async Task<IActionResult> ViewBills()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var bills = await _context.Bills
                .Where(b => b.TeacherId == teacher.TeacherId)
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.Month)
                .ToListAsync();

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.TotalUnpaid = bills.Where(b => !b.IsPaid).Sum(b => b.TotalBill);

            return View(bills);
        }

        // GET: Teachers/MakePayment/5
        public async Task<IActionResult> MakePayment(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == id && b.TeacherId == teacher.TeacherId);

            if (bill == null || bill.IsPaid)
            {
                return NotFound();
            }

            // Generate payment token
            bill.PaymentToken = PaymentGatewayService.GeneratePaymentToken();
            await _context.SaveChangesAsync();

            return View(bill);
        }

        // POST: Teachers/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int billId, string cardNumber, string cardHolderName, string expiryDate, string cvv)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == billId && b.TeacherId == teacher.TeacherId);

            if (bill == null || bill.IsPaid)
            {
                TempData["Error"] = "Bill not found or already paid.";
                return RedirectToAction(nameof(ViewBills));
            }

            // Process payment through dummy gateway
            var paymentResult = PaymentGatewayService.ProcessPayment(
                cardNumber, 
                cardHolderName, 
                expiryDate, 
                cvv, 
                bill.TotalBill
            );

            if (!paymentResult.IsSuccess)
            {
                TempData["Error"] = paymentResult.ErrorMessage;
                return RedirectToAction(nameof(MakePayment), new { id = billId });
            }

            // Update bill as paid
            bill.IsPaid = true;
            bill.PaidDate = DateTime.Now;
            bill.PaymentMethod = "Credit/Debit Card";
            bill.TransactionId = paymentResult.TransactionId;
            bill.UnpaidBalance = 0; // Clear unpaid balance after successful payment

            await _context.SaveChangesAsync();

            // Clear attendance records for this bill's month after payment (only if no disputes)
            var startDate = new DateTime(bill.Year, bill.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            
            var attendancesToClear = await _context.Attendances
                .Where(a => a.TeacherId == bill.TeacherId && 
                           a.Date >= startDate && 
                           a.Date <= endDate)
                .ToListAsync();
            
            if (attendancesToClear.Any())
            {
                // Get all disputes for these attendances
                var attendanceIds = attendancesToClear.Select(a => a.AttendanceId).ToList();
                var allDisputes = await _context.AttendanceDisputes
                    .Where(d => attendanceIds.Contains(d.AttendanceId))
                    .ToListAsync();

                // Auto-reject any pending disputes since bill is now paid
                var pendingDisputes = allDisputes.Where(d => d.Status == "Pending").ToList();
                if (pendingDisputes.Any())
                {
                    foreach (var dispute in pendingDisputes)
                    {
                        dispute.Status = "Rejected";
                        dispute.ResolvedDate = DateTime.Now;
                        dispute.AdminNotes = "[System] Auto-rejected: Bill has been paid by teacher.";
                    }
                }

                // Delete all disputes (pending ones are now rejected, others can be removed)
                _context.AttendanceDisputes.RemoveRange(allDisputes);
                
                // Delete all attendance records
                _context.Attendances.RemoveRange(attendancesToClear);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = $"✅ Payment successful! Transaction ID: {bill.TransactionId}. Amount paid: Rs. {bill.TotalBill:N2}. All attendance records have been cleared.";
            }
            else
            {
                TempData["Success"] = $"✅ Payment successful! Transaction ID: {bill.TransactionId}. Amount paid: Rs. {bill.TotalBill:N2}.";
            }

            return RedirectToAction(nameof(ViewBills));
        }

        // GET: Teachers/PayAllBills
        public async Task<IActionResult> PayAllBills()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var unpaidBills = await _context.Bills
                .Where(b => b.TeacherId == teacher.TeacherId && !b.IsPaid)
                .OrderBy(b => b.Year).ThenBy(b => b.Month)
                .ToListAsync();

            if (!unpaidBills.Any())
            {
                TempData["Info"] = "You don't have any unpaid bills.";
                return RedirectToAction(nameof(ViewBills));
            }

            ViewBag.TotalAmount = unpaidBills.Sum(b => b.TotalBill);
            ViewBag.BillCount = unpaidBills.Count;
            return View(unpaidBills);
        }

        // POST: Teachers/ProcessAllPayments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessAllPayments(string cardNumber, string cardHolderName, string expiryDate, string cvv)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var unpaidBills = await _context.Bills
                .Where(b => b.TeacherId == teacher.TeacherId && !b.IsPaid)
                .OrderBy(b => b.Year).ThenBy(b => b.Month)
                .ToListAsync();

            if (!unpaidBills.Any())
            {
                TempData["Error"] = "No unpaid bills found.";
                return RedirectToAction(nameof(ViewBills));
            }

            var totalAmount = unpaidBills.Sum(b => b.TotalBill);

            // Process payment through dummy gateway
            var paymentResult = PaymentGatewayService.ProcessPayment(
                cardNumber,
                cardHolderName,
                expiryDate,
                cvv,
                totalAmount
            );

            if (!paymentResult.IsSuccess)
            {
                TempData["Error"] = paymentResult.ErrorMessage;
                return RedirectToAction(nameof(PayAllBills));
            }

            // Update all bills as paid
            int clearedMonths = 0;
            foreach (var bill in unpaidBills)
            {
                bill.IsPaid = true;
                bill.PaidDate = DateTime.Now;
                bill.PaymentMethod = "Credit/Debit Card";
                bill.TransactionId = paymentResult.TransactionId;
                bill.UnpaidBalance = 0;

                // Clear attendance records for this bill's month
                var startDate = new DateTime(bill.Year, bill.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var attendancesToClear = await _context.Attendances
                    .Where(a => a.TeacherId == bill.TeacherId &&
                               a.Date >= startDate &&
                               a.Date <= endDate)
                    .ToListAsync();

                if (attendancesToClear.Any())
                {
                    // Get all disputes for these attendances
                    var attendanceIds = attendancesToClear.Select(a => a.AttendanceId).ToList();
                    var allDisputes = await _context.AttendanceDisputes
                        .Where(d => attendanceIds.Contains(d.AttendanceId))
                        .ToListAsync();

                    // Auto-reject any pending disputes since bill is now paid
                    var pendingDisputes = allDisputes.Where(d => d.Status == "Pending").ToList();
                    if (pendingDisputes.Any())
                    {
                        foreach (var dispute in pendingDisputes)
                        {
                            dispute.Status = "Rejected";
                            dispute.ResolvedDate = DateTime.Now;
                            dispute.AdminNotes = "[System] Auto-rejected: Bill has been paid by teacher.";
                        }
                    }

                    // Delete all disputes
                    _context.AttendanceDisputes.RemoveRange(allDisputes);
                    
                    // Delete all attendance records
                    _context.Attendances.RemoveRange(attendancesToClear);
                    clearedMonths++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Payment successful! Transaction ID: {paymentResult.TransactionId}. Total amount paid: Rs. {totalAmount:N2} for {unpaidBills.Count} bill(s). Attendance records cleared for {clearedMonths} month(s).";
            return RedirectToAction(nameof(ViewBills));
        }

        // GET: Teachers/BillDetails/5
        public async Task<IActionResult> BillDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var bill = await _context.Bills
                .Include(b => b.Teacher)
                .FirstOrDefaultAsync(b => b.BillId == id && b.TeacherId == teacher.TeacherId);

            if (bill == null)
            {
                return NotFound();
            }

            // Get attendance records for the bill's month
            var attendances = await _context.Attendances
                .Where(a => a.TeacherId == teacher.TeacherId 
                    && a.Date.Month == bill.Month 
                    && a.Date.Year == bill.Year)
                .OrderBy(a => a.Date)
                .ToListAsync();

            // Get billing configuration
            var config = await _context.BillingConfigurations.FirstOrDefaultAsync();

            ViewBag.BillingConfig = config;
            ViewBag.Attendances = attendances;

            return View(bill);
        }

        // POST: Teachers/DeleteAttendance/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttendance(int id, string? returnAction = "ViewAttendance")
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                TempData["Error"] = "Teacher profile not found.";
                return RedirectToAction(returnAction);
            }

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.AttendanceId == id && a.TeacherId == teacher.TeacherId);

            if (attendance == null)
            {
                TempData["Error"] = "Attendance record not found or you don't have permission to delete it.";
                return RedirectToAction(returnAction);
            }

            var attendanceDate = attendance.Date;

            // Check if there's a bill for this attendance
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.TeacherId == teacher.TeacherId 
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

                    // Update bill
                    bill.FoodBill -= deduction;
                    bill.TotalBill -= deduction;
                    bill.TotalMealsConsumed -= mealsReduced;

                    if (bill.IsPaid)
                    {
                        // If bill is paid, create a credit for next bill
                        bill.UnpaidBalance = -deduction; // Negative balance = credit
                        TempData["Success"] = $"Attendance deleted for {attendanceDate:MMM dd, yyyy}. Credit of Rs. {deduction:N2} will be applied to your next bill.";
                    }
                    else
                    {
                        // If unpaid, just reduce the current bill
                        bill.UnpaidBalance -= deduction;
                        TempData["Success"] = $"Attendance deleted for {attendanceDate:MMM dd, yyyy}. Your bill has been reduced by Rs. {deduction:N2}.";
                    }
                }
            }

            // Delete the attendance record
            _context.Attendances.Remove(attendance);
            await _context.SaveChangesAsync();

            if (bill == null)
            {
                TempData["Success"] = $"Attendance deleted for {attendanceDate:MMM dd, yyyy}.";
            }

            return RedirectToAction(returnAction);
        }


    }
}
