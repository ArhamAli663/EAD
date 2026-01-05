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

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
        
        // Apply migrations
        context.Database.Migrate();
        
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

app.UseHttpsRedirection();
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
