using Microsoft.EntityFrameworkCore;
using MessManagementSystem.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MessManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Response Caching to reduce server load
builder.Services.AddResponseCaching();

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add DbContext - Use SQLite for Production, SQL Server for Development
var databaseProvider = builder.Configuration["DatabaseProvider"];
if (databaseProvider == "SQLite")
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Add JWT Token Service
builder.Services.AddScoped<JwtTokenService>();

// Add Background Services
builder.Services.AddHostedService<AttendanceSchedulerService>();
builder.Services.AddHostedService<MonthlyBillingService>();

// Add Authentication with both Cookie and JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "")),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    
    // Allow JWT in both header and cookie
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Check for JWT in Authorization header first
            if (string.IsNullOrEmpty(context.Token))
            {
                // If not in header, check cookie
                context.Token = context.Request.Cookies["jwt_token"];
            }
            return Task.CompletedTask;
        }
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Initialize database and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();
        
        // Apply migrations or create database based on provider
        var dbProvider = configuration["DatabaseProvider"];
        if (dbProvider == "SQLite")
        {
            // For SQLite, use EnsureCreated to create schema
            context.Database.EnsureCreated();
        }
        else
        {
            // For SQL Server, use migrations
            context.Database.Migrate();
        }
        
        // Seed admin user if not exists
        if (!context.Users.Any(u => u.Username == "admin"))
        {
            var adminUser = new MessManagementSystem.Models.User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                MustChangePassword = false,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            context.Users.Add(adminUser);
            context.SaveChanges();
        }
        
        // Seed default billing configuration if not exists
        if (!context.BillingConfigurations.Any())
        {
            var config = new MessManagementSystem.Models.BillingConfiguration
            {
                MonthlyWaterBillTotal = 5000,
                DefaultBreakfastRate = 30,
                DefaultLunchRate = 60,
                DefaultDinnerRate = 50,
                LastUpdated = DateTime.Now
            };
            context.BillingConfigurations.Add(config);
            context.SaveChanges();
        }
        
        // Seed Pakistani menu items for all 7 days if not exists
        if (!context.MenuItems.Any())
        {
            var menuItems = new List<MessManagementSystem.Models.MenuItem>
            {
                // Monday
                new() { ItemName = "Halwa Puri", Description = "Traditional halwa with crispy puris", MealType = "Breakfast", DayOfWeek = "Monday", RatePerServing = 80, IsActive = true },
                new() { ItemName = "Chicken Biryani", Description = "Fragrant rice with spiced chicken", MealType = "Lunch", DayOfWeek = "Monday", RatePerServing = 150, IsActive = true },
                new() { ItemName = "Daal Chawal", Description = "Lentils with steamed rice", MealType = "Dinner", DayOfWeek = "Monday", RatePerServing = 100, IsActive = true },
                
                // Tuesday
                new() { ItemName = "Paratha with Omelette", Description = "Flaky paratha with egg omelette", MealType = "Breakfast", DayOfWeek = "Tuesday", RatePerServing = 70, IsActive = true },
                new() { ItemName = "Nihari", Description = "Slow-cooked beef stew with naan", MealType = "Lunch", DayOfWeek = "Tuesday", RatePerServing = 180, IsActive = true },
                new() { ItemName = "Karahi Chicken", Description = "Spicy chicken in wok-style curry", MealType = "Dinner", DayOfWeek = "Tuesday", RatePerServing = 160, IsActive = true },
                
                // Wednesday
                new() { ItemName = "Chana Chaat", Description = "Chickpea salad with spices", MealType = "Breakfast", DayOfWeek = "Wednesday", RatePerServing = 60, IsActive = true },
                new() { ItemName = "Mutton Pulao", Description = "Aromatic rice with tender mutton", MealType = "Lunch", DayOfWeek = "Wednesday", RatePerServing = 170, IsActive = true },
                new() { ItemName = "Aloo Gosht", Description = "Potato and meat curry", MealType = "Dinner", DayOfWeek = "Wednesday", RatePerServing = 140, IsActive = true },
                
                // Thursday
                new() { ItemName = "Aloo Paratha", Description = "Stuffed flatbread with spiced potatoes", MealType = "Breakfast", DayOfWeek = "Thursday", RatePerServing = 75, IsActive = true },
                new() { ItemName = "Fish Curry", Description = "Spicy fish in tomato gravy", MealType = "Lunch", DayOfWeek = "Thursday", RatePerServing = 160, IsActive = true },
                new() { ItemName = "Palak Paneer", Description = "Spinach with cottage cheese", MealType = "Dinner", DayOfWeek = "Thursday", RatePerServing = 120, IsActive = true },
                
                // Friday
                new() { ItemName = "Paya", Description = "Traditional trotters soup", MealType = "Breakfast", DayOfWeek = "Friday", RatePerServing = 90, IsActive = true },
                new() { ItemName = "Beef Pulao", Description = "Fragrant rice with beef", MealType = "Lunch", DayOfWeek = "Friday", RatePerServing = 165, IsActive = true },
                new() { ItemName = "Chicken Korma", Description = "Creamy chicken curry", MealType = "Dinner", DayOfWeek = "Friday", RatePerServing = 150, IsActive = true },
                
                // Saturday
                new() { ItemName = "Nihari", Description = "Spicy slow-cooked beef with naan", MealType = "Breakfast", DayOfWeek = "Saturday", RatePerServing = 120, IsActive = true },
                new() { ItemName = "Kabuli Pulao", Description = "Afghan-style rice with meat and carrots", MealType = "Lunch", DayOfWeek = "Saturday", RatePerServing = 175, IsActive = true },
                new() { ItemName = "Mix Vegetable", Description = "Seasonal vegetables curry", MealType = "Dinner", DayOfWeek = "Saturday", RatePerServing = 110, IsActive = true },
                
                // Sunday
                new() { ItemName = "Haleem", Description = "Rich meat and lentil porridge", MealType = "Breakfast", DayOfWeek = "Sunday", RatePerServing = 100, IsActive = true },
                new() { ItemName = "Chicken Karahi", Description = "Wok-style chicken with tomatoes", MealType = "Lunch", DayOfWeek = "Sunday", RatePerServing = 160, IsActive = true },
                new() { ItemName = "Daal Mash", Description = "Urad lentils with spices", MealType = "Dinner", DayOfWeek = "Sunday", RatePerServing = 95, IsActive = true }
            };
            
            context.MenuItems.AddRange(menuItems);
            context.SaveChanges();
        }
        
        // Seed Pakistani teachers if not exists
        if (!context.Teachers.Any())
        {
            var pakistaniTeachers = new[]
            {
                new { FullName = "Muhammad Ahmed Khan", Email = "ahmed.khan@mess.edu.pk", Phone = "0300-1234567", Department = "Computer Science" },
                new { FullName = "Fatima Zahra Malik", Email = "fatima.malik@mess.edu.pk", Phone = "0321-2345678", Department = "Mathematics" },
                new { FullName = "Ali Hassan Qureshi", Email = "ali.qureshi@mess.edu.pk", Phone = "0333-3456789", Department = "Physics" },
                new { FullName = "Ayesha Siddiqui", Email = "ayesha.siddiqui@mess.edu.pk", Phone = "0345-4567890", Department = "Chemistry" },
                new { FullName = "Usman Tariq", Email = "usman.tariq@mess.edu.pk", Phone = "0312-5678901", Department = "English Literature" },
                new { FullName = "Zainab Bibi", Email = "zainab.bibi@mess.edu.pk", Phone = "0301-6789012", Department = "Urdu" },
                new { FullName = "Imran Hussain Shah", Email = "imran.shah@mess.edu.pk", Phone = "0322-7890123", Department = "History" },
                new { FullName = "Sana Noor", Email = "sana.noor@mess.edu.pk", Phone = "0334-8901234", Department = "Biology" },
                new { FullName = "Bilal Ahmed Rana", Email = "bilal.rana@mess.edu.pk", Phone = "0346-9012345", Department = "Economics" },
                new { FullName = "Maryam Khalid", Email = "maryam.khalid@mess.edu.pk", Phone = "0313-0123456", Department = "Psychology" },
                new { FullName = "Hassan Raza Bukhari", Email = "hassan.bukhari@mess.edu.pk", Phone = "0302-1234568", Department = "Political Science" },
                new { FullName = "Amna Parveen", Email = "amna.parveen@mess.edu.pk", Phone = "0323-2345679", Department = "Sociology" },
                new { FullName = "Farhan Ali Chaudhry", Email = "farhan.chaudhry@mess.edu.pk", Phone = "0335-3456780", Department = "Business Administration" },
                new { FullName = "Hira Batool", Email = "hira.batool@mess.edu.pk", Phone = "0347-4567891", Department = "Fine Arts" },
                new { FullName = "Asad Mehmood Bhatti", Email = "asad.bhatti@mess.edu.pk", Phone = "0314-5678902", Department = "Physical Education" }
            };
            
            foreach (var teacherData in pakistaniTeachers)
            {
                // Create user account for teacher
                var username = teacherData.Email.Split('@')[0].Replace(".", "_");
                var teacherUser = new MessManagementSystem.Models.User
                {
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("teacher123"),
                    Role = "Teacher",
                    MustChangePassword = true,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };
                context.Users.Add(teacherUser);
                context.SaveChanges();
                
                // Create teacher profile linked to user
                var teacher = new MessManagementSystem.Models.Teacher
                {
                    FullName = teacherData.FullName,
                    Email = teacherData.Email,
                    PhoneNumber = teacherData.Phone,
                    Department = teacherData.Department,
                    JoiningDate = DateTime.Now.AddMonths(-new Random().Next(1, 24)),
                    IsActive = true,
                    UserId = teacherUser.UserId
                };
                context.Teachers.Add(teacher);
            }
            context.SaveChanges();
        }
        
        // Seed sample attendance data if not exists
        if (!context.Attendances.Any())
        {
            var teachers = context.Teachers.Where(t => t.IsActive).ToList();
            var adminUser = context.Users.FirstOrDefault(u => u.Role == "Admin");
            
            if (teachers.Any() && adminUser != null)
            {
                var attendanceRecords = new List<MessManagementSystem.Models.Attendance>();
                var today = DateTime.Today;
                
                // Add attendance for the past 10 days for each teacher
                for (int daysAgo = 10; daysAgo >= 0; daysAgo--)
                {
                    var date = today.AddDays(-daysAgo);
                    
                    foreach (var teacher in teachers)
                    {
                        // Randomly assign meals (85% chance of taking each meal)
                        var random = new Random(teacher.TeacherId * daysAgo);
                        
                        attendanceRecords.Add(new MessManagementSystem.Models.Attendance
                        {
                            TeacherId = teacher.TeacherId,
                            Date = date,
                            BreakfastTaken = random.Next(100) < 85,
                            LunchTaken = random.Next(100) < 85,
                            DinnerTaken = random.Next(100) < 85,
                            RecordedBy = adminUser.UserId,
                            RecordedDate = date.AddHours(8), // Assume marked at 8 AM each day
                            Remarks = $"Auto-seeded attendance for {date.ToString("MMMM dd, yyyy")}"
                        });
                    }
                }
                
                context.Attendances.AddRange(attendanceRecords);
                context.SaveChanges();
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Only use HTTPS redirection in development (Docker/reverse proxy handles SSL in production)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

// Add response caching middleware
app.UseResponseCaching();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
