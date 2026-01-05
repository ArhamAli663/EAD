using Microsoft.AspNetCore.Mvc;
using MessManagementSystem.Data;
using MessManagementSystem.Models;
using MessManagementSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace MessManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtTokenService _jwtTokenService;

        public AccountController(ApplicationDbContext context, JwtTokenService jwtTokenService)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                // Redirect based on user role
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                else if (User.IsInRole("Teacher"))
                {
                    return RedirectToAction("Index", "Teachers");
                }
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string role)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ViewBag.Error = "Invalid username or password.";
                return View();
            }

            // Verify role matches
            if (!string.IsNullOrEmpty(role) && user.Role != role)
            {
                ViewBag.Error = "Invalid credentials for selected role.";
                return View();
            }

            // Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Generate JWT token
            var jwtToken = _jwtTokenService.GenerateToken(user);
            
            // Store JWT token in cookie for API access
            Response.Cookies.Append("jwt_token", jwtToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });

            // Store token in ViewBag for client-side access if needed
            TempData["JwtToken"] = jwtToken;

            // Check if user must change password
            if (user.MustChangePassword)
            {
                return RedirectToAction("ChangePassword");
            }

            // Redirect to appropriate dashboard based on role
            if (user.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                return RedirectToAction("Index", "Teachers");
            }
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword != confirmPassword)
            {
                ViewBag.Error = "New password and confirm password do not match.";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters long.";
                return View();
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var user = _context.Users.Find(userId);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ViewBag.Error = "Current password is incorrect.";
                return View();
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            await _context.SaveChangesAsync();

            ViewBag.Success = "Password changed successfully!";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Remove JWT token from cookie
            Response.Cookies.Delete("jwt_token");
            
            return RedirectToAction("Login");
        }

        // API Endpoint for JWT Authentication
        [HttpPost]
        [Route("api/account/login")]
        public IActionResult ApiLogin([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required." });
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == request.Username && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { success = false, message = "Invalid username or password." });
            }

            // Verify role matches if provided
            if (!string.IsNullOrEmpty(request.Role) && user.Role != request.Role)
            {
                return Unauthorized(new { success = false, message = "Invalid credentials for selected role." });
            }

            // Generate JWT token
            var token = _jwtTokenService.GenerateToken(user);

            return Ok(new
            {
                success = true,
                token = token,
                user = new
                {
                    userId = user.UserId,
                    username = user.Username,
                    role = user.Role,
                    mustChangePassword = user.MustChangePassword
                },
                expiresIn = 8 * 60 * 60 // 8 hours in seconds
            });
        }

        // API Endpoint to validate JWT token
        [HttpGet]
        [Route("api/account/validate")]
        public IActionResult ValidateToken()
        {
            var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            
            if (string.IsNullOrEmpty(token))
            {
                token = Request.Cookies["jwt_token"];
            }

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, message = "No token provided." });
            }

            var principal = _jwtTokenService.ValidateToken(token);

            if (principal == null)
            {
                return Unauthorized(new { success = false, message = "Invalid or expired token." });
            }

            return Ok(new
            {
                success = true,
                user = new
                {
                    userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    username = principal.FindFirst(ClaimTypes.Name)?.Value,
                    role = principal.FindFirst(ClaimTypes.Role)?.Value
                }
            });
        }
    }

    // Request model for API login
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Role { get; set; }
    }
}
