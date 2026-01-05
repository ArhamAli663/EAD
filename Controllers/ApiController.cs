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

        // ==========================================
        // TEACHER MANAGEMENT API ENDPOINTS
        // ==========================================

        // Create a new teacher (Admin only)
        [HttpPost("teachers")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || 
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "All required fields must be provided." });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(new { success = false, message = "Password must be at least 6 characters." });
            }

            // Check if username already exists
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest(new { success = false, message = "Username already exists." });
            }

            // Check if email already exists
            if (await _context.Teachers.AnyAsync(t => t.Email == request.Email))
            {
                return BadRequest(new { success = false, message = "Email already exists." });
            }

            // Create user account
            var user = new User
            {
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "Teacher",
                MustChangePassword = true,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create teacher profile
            var teacher = new Teacher
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber ?? "",
                Department = request.Department ?? "",
                UserId = user.UserId,
                JoiningDate = DateTime.Now,
                IsActive = true
            };

            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Teacher '{teacher.FullName}' created successfully.",
                data = new
                {
                    teacherId = teacher.TeacherId,
                    fullName = teacher.FullName,
                    email = teacher.Email,
                    department = teacher.Department,
                    username = user.Username
                }
            });
        }

        // Update teacher (Admin only)
        [HttpPut("teachers/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTeacher(int id, [FromBody] UpdateTeacherRequest request)
        {
            var teacher = await _context.Teachers.FindAsync(id);
            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher not found." });
            }

            // Check if email is being changed to an existing one
            if (!string.IsNullOrEmpty(request.Email) && 
                request.Email != teacher.Email &&
                await _context.Teachers.AnyAsync(t => t.Email == request.Email && t.TeacherId != id))
            {
                return BadRequest(new { success = false, message = "Email already exists." });
            }

            teacher.FullName = request.FullName ?? teacher.FullName;
            teacher.Email = request.Email ?? teacher.Email;
            teacher.PhoneNumber = request.PhoneNumber ?? teacher.PhoneNumber;
            teacher.Department = request.Department ?? teacher.Department;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Teacher '{teacher.FullName}' updated successfully.",
                data = new
                {
                    teacherId = teacher.TeacherId,
                    fullName = teacher.FullName,
                    email = teacher.Email,
                    department = teacher.Department,
                    phoneNumber = teacher.PhoneNumber
                }
            });
        }

        // Delete teacher (Admin only)
        [HttpDelete("teachers/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTeacher(int id)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == id);

            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher not found." });
            }

            var teacherName = teacher.FullName;

            // Delete related records
            var attendances = await _context.Attendances.Where(a => a.TeacherId == id).ToListAsync();
            _context.Attendances.RemoveRange(attendances);

            var bills = await _context.Bills.Where(b => b.TeacherId == id).ToListAsync();
            _context.Bills.RemoveRange(bills);

            var disputes = await _context.AttendanceDisputes.Where(d => d.TeacherId == id).ToListAsync();
            _context.AttendanceDisputes.RemoveRange(disputes);

            // Delete user account if exists
            if (teacher.User != null)
            {
                _context.Users.Remove(teacher.User);
            }

            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Teacher '{teacherName}' and all related data deleted successfully."
            });
        }

        // Get single teacher details (Admin only)
        [HttpGet("teachers/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTeacher(int id)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == id);

            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher not found." });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    teacherId = teacher.TeacherId,
                    fullName = teacher.FullName,
                    email = teacher.Email,
                    phoneNumber = teacher.PhoneNumber,
                    department = teacher.Department,
                    joiningDate = teacher.JoiningDate,
                    isActive = teacher.IsActive,
                    username = teacher.User?.Username
                }
            });
        }

        // ==========================================
        // ATTENDANCE API ENDPOINTS
        // ==========================================

        // Mark attendance (Admin only)
        [HttpPost("attendance")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            var teacher = await _context.Teachers.FindAsync(request.TeacherId);
            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher not found." });
            }

            var date = request.Date?.Date ?? DateTime.Today;

            // Check if attendance already exists for this date
            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.TeacherId == request.TeacherId && a.Date.Date == date);

            if (existingAttendance != null)
            {
                // Update existing attendance
                existingAttendance.BreakfastTaken = request.BreakfastTaken;
                existingAttendance.LunchTaken = request.LunchTaken;
                existingAttendance.DinnerTaken = request.DinnerTaken;
                existingAttendance.Remarks = request.Remarks;
                existingAttendance.RecordedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Attendance updated successfully.",
                    data = new
                    {
                        attendanceId = existingAttendance.AttendanceId,
                        teacherName = teacher.FullName,
                        date = existingAttendance.Date
                    }
                });
            }

            // Create new attendance
            var attendance = new Attendance
            {
                TeacherId = request.TeacherId,
                Date = date,
                BreakfastTaken = request.BreakfastTaken,
                LunchTaken = request.LunchTaken,
                DinnerTaken = request.DinnerTaken,
                Remarks = request.Remarks,
                RecordedDate = DateTime.Now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Attendance marked successfully.",
                data = new
                {
                    attendanceId = attendance.AttendanceId,
                    teacherName = teacher.FullName,
                    date = attendance.Date
                }
            });
        }

        // Delete attendance (Admin only)
        [HttpDelete("attendance/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAttendance(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null)
            {
                return NotFound(new { success = false, message = "Attendance record not found." });
            }

            _context.Attendances.Remove(attendance);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Attendance record deleted successfully." });
        }

        // ==========================================
        // DISPUTE API ENDPOINTS
        // ==========================================

        // Report wrong attendance (Teacher)
        [HttpPost("disputes")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> ReportDispute([FromBody] CreateDisputeRequest request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher profile not found." });
            }

            var attendance = await _context.Attendances.FindAsync(request.AttendanceId);
            if (attendance == null || attendance.TeacherId != teacher.TeacherId)
            {
                return NotFound(new { success = false, message = "Attendance record not found." });
            }

            // Check if dispute already exists
            var existingDispute = await _context.AttendanceDisputes
                .FirstOrDefaultAsync(d => d.AttendanceId == request.AttendanceId && d.Status == "Pending");

            if (existingDispute != null)
            {
                return BadRequest(new { success = false, message = "A pending dispute already exists for this attendance." });
            }

            var dispute = new AttendanceDispute
            {
                AttendanceId = request.AttendanceId,
                TeacherId = teacher.TeacherId,
                Reason = request.Reason ?? "No reason provided",
                Status = "Pending",
                ReportedDate = DateTime.Now
            };

            _context.AttendanceDisputes.Add(dispute);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Dispute submitted successfully. It will be reviewed by admin.",
                data = new
                {
                    disputeId = dispute.DisputeId,
                    status = dispute.Status
                }
            });
        }

        // Get disputes (Admin only)
        [HttpGet("disputes")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDisputes([FromQuery] string? status)
        {
            var query = _context.AttendanceDisputes
                .Include(d => d.Teacher)
                .Include(d => d.Attendance)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(d => d.Status == status);
            }

            var disputes = await query
                .OrderByDescending(d => d.ReportedDate)
                .Select(d => new
                {
                    disputeId = d.DisputeId,
                    teacherName = d.Teacher != null ? d.Teacher.FullName : "Unknown",
                    attendanceDate = d.Attendance != null ? d.Attendance.Date : DateTime.MinValue,
                    reason = d.Reason,
                    status = d.Status,
                    submittedDate = d.ReportedDate,
                    resolvedDate = d.ResolvedDate,
                    adminRemarks = d.AdminNotes
                })
                .ToListAsync();

            return Ok(new { success = true, count = disputes.Count, data = disputes });
        }

        // Resolve dispute (Admin only)
        [HttpPut("disputes/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResolveDispute(int id, [FromBody] ResolveDisputeRequest request)
        {
            var dispute = await _context.AttendanceDisputes
                .Include(d => d.Attendance)
                .FirstOrDefaultAsync(d => d.DisputeId == id);

            if (dispute == null)
            {
                return NotFound(new { success = false, message = "Dispute not found." });
            }

            dispute.Status = request.Approved ? "Approved" : "Rejected";
            dispute.AdminNotes = request.AdminRemarks;
            dispute.ResolvedDate = DateTime.Now;

            // If approved, update the attendance record
            if (request.Approved && dispute.Attendance != null)
            {
                if (request.UpdateBreakfast.HasValue)
                    dispute.Attendance.BreakfastTaken = request.UpdateBreakfast.Value;
                if (request.UpdateLunch.HasValue)
                    dispute.Attendance.LunchTaken = request.UpdateLunch.Value;
                if (request.UpdateDinner.HasValue)
                    dispute.Attendance.DinnerTaken = request.UpdateDinner.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Dispute has been {dispute.Status.ToLower()}."
            });
        }

        // ==========================================
        // BILLING API ENDPOINTS
        // ==========================================

        // Pay bill (Teacher)
        [HttpPost("bills/{id}/pay")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> PayBill(int id, [FromBody] PayBillRequest request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher profile not found." });
            }

            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.BillId == id && b.TeacherId == teacher.TeacherId);
            if (bill == null)
            {
                return NotFound(new { success = false, message = "Bill not found." });
            }

            if (bill.IsPaid)
            {
                return BadRequest(new { success = false, message = "This bill has already been paid." });
            }

            // Process payment (dummy validation)
            if (string.IsNullOrEmpty(request.CardNumber) || request.CardNumber.Replace(" ", "").Length < 16)
            {
                return BadRequest(new { success = false, message = "Invalid card number." });
            }

            // Mark bill as paid
            bill.IsPaid = true;
            bill.PaidDate = DateTime.Now;
            bill.PaymentMethod = request.PaymentMethod ?? "Card";
            bill.TransactionId = $"TXN{DateTime.Now:yyyyMMddHHmmss}{bill.BillId}";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Payment successful! Thank you.",
                data = new
                {
                    billId = bill.BillId,
                    amount = bill.TotalBill,
                    transactionId = bill.TransactionId,
                    paidDate = bill.PaidDate
                }
            });
        }

        // ==========================================
        // MENU API ENDPOINTS
        // ==========================================

        // Create menu item (Admin only)
        [HttpPost("menu")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMenuItem([FromBody] CreateMenuItemRequest request)
        {
            var menuItem = new MenuItem
            {
                ItemName = request.ItemName ?? "",
                Description = request.Description ?? "",
                MealType = request.MealType ?? "Lunch",
                DayOfWeek = request.DayOfWeek ?? "Monday",
                RatePerServing = request.RatePerServing,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Menu item created successfully.",
                data = new { menuItemId = menuItem.MenuItemId }
            });
        }

        // Update menu item (Admin only)
        [HttpPut("menu/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMenuItem(int id, [FromBody] CreateMenuItemRequest request)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound(new { success = false, message = "Menu item not found." });
            }

            menuItem.ItemName = request.ItemName ?? menuItem.ItemName;
            menuItem.Description = request.Description ?? menuItem.Description;
            menuItem.MealType = request.MealType ?? menuItem.MealType;
            menuItem.DayOfWeek = request.DayOfWeek ?? menuItem.DayOfWeek;
            menuItem.RatePerServing = request.RatePerServing > 0 ? request.RatePerServing : menuItem.RatePerServing;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Menu item updated successfully." });
        }

        // Delete menu item (Admin only)
        [HttpDelete("menu/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound(new { success = false, message = "Menu item not found." });
            }

            _context.MenuItems.Remove(menuItem);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Menu item deleted successfully." });
        }

        // Toggle menu item active status (Admin only)
        [HttpPatch("menu/{id}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleMenuItem(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound(new { success = false, message = "Menu item not found." });
            }

            menuItem.IsActive = !menuItem.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Menu item is now {(menuItem.IsActive ? "active" : "inactive")}.",
                isActive = menuItem.IsActive
            });
        }
    }

    // ==========================================
    // REQUEST DTOs
    // ==========================================

    public class CreateTeacherRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class UpdateTeacherRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
    }

    public class MarkAttendanceRequest
    {
        public int TeacherId { get; set; }
        public DateTime? Date { get; set; }
        public bool BreakfastTaken { get; set; }
        public bool LunchTaken { get; set; }
        public bool DinnerTaken { get; set; }
        public string? Remarks { get; set; }
    }

    public class CreateDisputeRequest
    {
        public int AttendanceId { get; set; }
        public string? Reason { get; set; }
    }

    public class ResolveDisputeRequest
    {
        public bool Approved { get; set; }
        public string? AdminRemarks { get; set; }
        public bool? UpdateBreakfast { get; set; }
        public bool? UpdateLunch { get; set; }
        public bool? UpdateDinner { get; set; }
    }

    public class PayBillRequest
    {
        public string? CardNumber { get; set; }
        public string? CardHolderName { get; set; }
        public string? ExpiryDate { get; set; }
        public string? Cvv { get; set; }
        public string? PaymentMethod { get; set; }
    }

    public class CreateMenuItemRequest
    {
        public string? ItemName { get; set; }
        public string? Description { get; set; }
        public string? MealType { get; set; }
        public string? DayOfWeek { get; set; }
        public decimal RatePerServing { get; set; }
    }
}
