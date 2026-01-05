using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagementSystem.Data;
using System.Security.Claims;

namespace MessManagementSystem.ViewComponents
{
    public class NotificationBadgeViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NotificationBadgeViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = int.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var role = UserClaimsPrincipal.FindFirstValue(ClaimTypes.Role);

            var model = new NotificationBadgeViewModel();

            if (role == "Admin")
            {
                // Count pending disputes for admin
                model.PendingDisputes = await _context.AttendanceDisputes
                    .CountAsync(d => d.Status == "Pending");
            }
            else if (role == "Teacher")
            {
                // Count unacknowledged resolved disputes for teacher
                model.UnacknowledgedDisputes = await _context.AttendanceDisputes
                    .CountAsync(d => d.TeacherId == userId && 
                                    d.Status != "Pending" && 
                                    !d.IsAcknowledgedByTeacher);
            }

            return View(model);
        }
    }

    public class NotificationBadgeViewModel
    {
        public int PendingDisputes { get; set; }
        public int UnacknowledgedDisputes { get; set; }
    }
}
