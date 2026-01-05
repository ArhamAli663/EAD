using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessManagementSystem.Data;
using MessManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace MessManagementSystem.Controllers
{
    [Authorize]
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Teachers and Admins can view the menu
        public async Task<IActionResult> Index()
        {
            var menuItems = await _context.MenuItems
                .Where(m => m.IsActive && m.MealType != "Breakfast")
                .OrderBy(m => m.DayOfWeek)
                .ThenBy(m => m.MealType)
                .ToListAsync();

            return View(menuItems);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(MenuItem menuItem)
        {
            if (ModelState.IsValid)
            {
                // Check if a menu item with the same DayOfWeek and MealType already exists
                var existingItem = await _context.MenuItems
                    .FirstOrDefaultAsync(m => m.DayOfWeek == menuItem.DayOfWeek 
                                            && m.MealType == menuItem.MealType 
                                            && m.IsActive);

                if (existingItem != null)
                {
                    ModelState.AddModelError("", $"A {menuItem.MealType} menu item already exists for {menuItem.DayOfWeek}. Please edit the existing item or delete it first.");
                    return View(menuItem);
                }

                _context.MenuItems.Add(menuItem);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Menu item added successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(menuItem);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }

            return View(menuItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, MenuItem menuItem)
        {
            if (id != menuItem.MenuItemId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Check if another menu item with the same DayOfWeek and MealType already exists
                var existingItem = await _context.MenuItems
                    .FirstOrDefaultAsync(m => m.DayOfWeek == menuItem.DayOfWeek 
                                            && m.MealType == menuItem.MealType 
                                            && m.IsActive 
                                            && m.MenuItemId != id);

                if (existingItem != null)
                {
                    ModelState.AddModelError("", $"A {menuItem.MealType} menu item already exists for {menuItem.DayOfWeek}. Please delete the other item first.");
                    return View(menuItem);
                }

                try
                {
                    _context.Update(menuItem);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Menu item updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuItemExists(menuItem.MenuItemId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(menuItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem != null)
            {
                menuItem.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Menu item deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.MenuItemId == id);
        }
    }
}
