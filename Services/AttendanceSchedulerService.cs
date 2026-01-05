using Microsoft.EntityFrameworkCore;
using MessManagementSystem.Data;
using MessManagementSystem.Models;

namespace MessManagementSystem.Services
{
    public class AttendanceSchedulerService : IHostedService, IDisposable
    {
        private Timer? _timer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AttendanceSchedulerService> _logger;

        public AttendanceSchedulerService(IServiceProvider serviceProvider, ILogger<AttendanceSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attendance Scheduler Service started.");
            
            // Calculate time until next 12 PM
            var now = DateTime.Now;
            var scheduledTime = DateTime.Today.AddHours(12);
            
            if (now > scheduledTime)
            {
                // If it's past 12 PM today, schedule for 12 PM tomorrow
                scheduledTime = scheduledTime.AddDays(1);
            }

            var timeUntilScheduled = scheduledTime - now;
            
            _logger.LogInformation($"Next attendance marking scheduled at: {scheduledTime}");

            // Set up timer to trigger at 12 PM daily
            _timer = new Timer(
                MarkDailyAttendance,
                null,
                timeUntilScheduled,
                TimeSpan.FromDays(1)); // Repeat every 24 hours

            return Task.CompletedTask;
        }

        private async void MarkDailyAttendance(object? state)
        {
            _logger.LogInformation("Starting daily attendance marking at {time}", DateTime.Now);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var today = DateTime.Today;

                // Check if attendance already marked for today
                var existingAttendance = await context.Attendances
                    .AnyAsync(a => a.Date.Date == today);

                if (existingAttendance)
                {
                    _logger.LogInformation("Attendance already marked for today.");
                    return;
                }

                // Get all active teachers
                var activeTeachers = await context.Teachers
                    .Where(t => t.IsActive)
                    .ToListAsync();

                if (!activeTeachers.Any())
                {
                    _logger.LogWarning("No active teachers found for attendance marking.");
                    return;
                }

                // Get admin user for RecordedBy field
                var adminUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Role == "Admin");

                if (adminUser == null)
                {
                    _logger.LogError("No admin user found for recording attendance.");
                    return;
                }

                // Mark attendance for all teachers (default: all meals taken)
                var attendanceRecords = new List<Attendance>();
                foreach (var teacher in activeTeachers)
                {
                    attendanceRecords.Add(new Attendance
                    {
                        TeacherId = teacher.TeacherId,
                        Date = today,
                        BreakfastTaken = true,
                        LunchTaken = true,
                        DinnerTaken = true,
                        RecordedBy = adminUser.UserId,
                        RecordedDate = DateTime.Now,
                        Remarks = "Auto-marked by scheduler at 12 PM"
                    });
                }

                await context.Attendances.AddRangeAsync(attendanceRecords);
                await context.SaveChangesAsync();

                var totalMeals = attendanceRecords.Count * 3; // 3 meals per teacher
                _logger.LogInformation($"Daily attendance marked successfully: {activeTeachers.Count} teachers, {totalMeals} meals");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while marking daily attendance");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attendance Scheduler Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
