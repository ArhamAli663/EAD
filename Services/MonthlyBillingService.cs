using Microsoft.EntityFrameworkCore;
using MessManagementSystem.Data;
using MessManagementSystem.Models;

namespace MessManagementSystem.Services
{
    public class MonthlyBillingService : IHostedService, IDisposable
    {
        private Timer? _timer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MonthlyBillingService> _logger;

        public MonthlyBillingService(IServiceProvider serviceProvider, ILogger<MonthlyBillingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Monthly Billing Service started.");
            
            // Calculate time until 1st of next month at 12 AM
            var now = DateTime.Now;
            var nextFirstOfMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            var timeUntilFirstOfMonth = nextFirstOfMonth - now;
            
            _logger.LogInformation($"Next monthly billing scheduled at: {nextFirstOfMonth}");

            // Set up timer to trigger on 1st of every month
            _timer = new Timer(
                GenerateMonthlyBills,
                null,
                timeUntilFirstOfMonth,
                TimeSpan.FromDays(30)); // Check daily, will only generate on 1st

            return Task.CompletedTask;
        }

        private async void GenerateMonthlyBills(object? state)
        {
            var now = DateTime.Now;
            
            // Only generate bills on the 1st of the month
            if (now.Day != 1)
            {
                return;
            }

            _logger.LogInformation("Starting monthly bill generation at {time}", now);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Get previous month details
                var previousMonth = now.AddMonths(-1);
                var startDate = new DateTime(previousMonth.Year, previousMonth.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Get billing configuration
                var config = await context.BillingConfigurations.FirstOrDefaultAsync();
                if (config == null)
                {
                    _logger.LogError("Billing configuration not found.");
                    return;
                }

                // Get all active teachers
                var activeTeachers = await context.Teachers
                    .Where(t => t.IsActive)
                    .ToListAsync();

                if (!activeTeachers.Any())
                {
                    _logger.LogWarning("No active teachers found for billing.");
                    return;
                }

                // Get admin user for GeneratedBy field
                var adminUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Role == "Admin");

                if (adminUser == null)
                {
                    _logger.LogError("No admin user found for generating bills.");
                    return;
                }

                var activeTeachersCount = activeTeachers.Count;
                var waterBillPerTeacher = config.MonthlyWaterBillTotal / activeTeachersCount;

                int billsGenerated = 0;

                foreach (var teacher in activeTeachers)
                {
                    // Check if bill already exists for this period
                    var existingBill = await context.Bills
                        .FirstOrDefaultAsync(b => b.TeacherId == teacher.TeacherId 
                            && b.Month == previousMonth.Month 
                            && b.Year == previousMonth.Year);

                    if (existingBill != null)
                    {
                        _logger.LogInformation($"Bill already exists for teacher {teacher.FullName} for {previousMonth:MMMM yyyy}");
                        continue;
                    }

                    // Get attendance for previous month
                    var attendances = await context.Attendances
                        .Where(a => a.TeacherId == teacher.TeacherId 
                            && a.Date >= startDate 
                            && a.Date <= endDate)
                        .ToListAsync();

                    // Calculate meal costs
                    var totalBreakfast = attendances.Count(a => a.BreakfastTaken);
                    var totalLunch = attendances.Count(a => a.LunchTaken);
                    var totalDinner = attendances.Count(a => a.DinnerTaken);
                    var totalMeals = totalBreakfast + totalLunch + totalDinner;

                    var breakfastCost = totalBreakfast * config.DefaultBreakfastRate;
                    var lunchCost = totalLunch * config.DefaultLunchRate;
                    var dinnerCost = totalDinner * config.DefaultDinnerRate;
                    var foodBill = breakfastCost + lunchCost + dinnerCost;

                    // Get previous unpaid balance
                    var previousUnpaidBill = await context.Bills
                        .Where(b => b.TeacherId == teacher.TeacherId && !b.IsPaid)
                        .OrderByDescending(b => b.Year)
                        .ThenByDescending(b => b.Month)
                        .FirstOrDefaultAsync();

                    var carryoverAmount = previousUnpaidBill?.UnpaidBalance ?? 0;

                    // Create new bill
                    var newBill = new Bill
                    {
                        TeacherId = teacher.TeacherId,
                        Teacher = teacher,
                        Month = previousMonth.Month,
                        Year = previousMonth.Year,
                        FoodBill = foodBill,
                        WaterBill = waterBillPerTeacher,
                        TotalMealsConsumed = totalMeals,
                        UnpaidBalance = carryoverAmount,
                        TotalBill = foodBill + waterBillPerTeacher + carryoverAmount,
                        IsPaid = false,
                        GeneratedBy = adminUser.UserId,
                        GeneratedDate = DateTime.Now
                    };

                    context.Bills.Add(newBill);
                    billsGenerated++;
                }

                await context.SaveChangesAsync();
                _logger.LogInformation($"Monthly bills generated successfully: {billsGenerated} bills for {previousMonth:MMMM yyyy}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating monthly bills");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Monthly Billing Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
