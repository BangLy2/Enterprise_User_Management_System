using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Models;
using MyWeb.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoleManagementController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager; // ← Changed from IdentityUser
        private readonly IAuditService _auditService;

        public RoleManagementController(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager, // ← Changed from IdentityUser
            IAuditService auditService)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        // GET: RoleManagement/Index
        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            // Count users in each role
            var roleStats = new List<RoleViewModel>();
            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                roleStats.Add(new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    UserCount = usersInRole.Count
                });
            }

            return View(roleStats);
        }

        // GET: RoleManagement/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: RoleManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError("", "Role name is required.");
                return View();
            }

            roleName = roleName.Trim();

            if (await _roleManager.RoleExistsAsync(roleName))
            {
                ModelState.AddModelError("", "Role already exists.");
                return View();
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    "Role Created",
                    "Role",
                    roleName,
                    null,
                    null,
                    roleName,
                    $"Role '{roleName}' created by {User.Identity.Name}"
                );

                TempData["SuccessMessage"] = $"Role '{roleName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // POST: RoleManagement/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent deletion of system roles
            if (role.Name == "Admin" || role.Name == "User")
            {
                TempData["ErrorMessage"] = "Cannot delete system roles (Admin, User).";
                return RedirectToAction(nameof(Index));
            }

            // Check if any users have this role
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
            if (usersInRole.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete role '{role.Name}' because {usersInRole.Count} user(s) are assigned to it.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    "Role Deleted",
                    "Role",
                    id,
                    null,
                    role.Name,
                    null,
                    $"Role '{role.Name}' deleted by {User.Identity.Name}"
                );

                TempData["SuccessMessage"] = $"Role '{role.Name}' deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete role.";
            }

            return RedirectToAction(nameof(Index));
        }
    }

    public class RoleViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int UserCount { get; set; }
    }
}