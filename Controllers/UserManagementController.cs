using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Data;
using MyWeb.Models;
using MyWeb.Services;
using MyWeb.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;

        public UserManagementController(UserManager<ApplicationUser> userManager, IAuditService auditService, ApplicationDbContext context)
        {
            _userManager = userManager;
            _auditService = auditService;
            _context = context;
        }

        // GET: UserManagement/Index
        public async Task<IActionResult> Index(
            string searchTerm,
            List<string> statusFilter,
            List<string> roleFilter,
            List<string> departmentFilter,
            string sortBy = "UserName",
            string sortOrder = "asc",
            int page = 1,
            int pageSize = 10)
        {
            var query = _userManager.Users.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                query = query.Where(u =>
                    u.UserName.ToLower().Contains(searchTerm) ||
                    u.Email.ToLower().Contains(searchTerm) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(searchTerm)) ||
                    (u.Department != null && u.Department.ToLower().Contains(searchTerm))
                );
            }

            // Apply status filter
            if (statusFilter != null && statusFilter.Any())
            {
                if (statusFilter.Contains("active") && !statusFilter.Contains("inactive"))
                {
                    query = query.Where(u => u.IsActive);
                }
                else if (statusFilter.Contains("inactive") && !statusFilter.Contains("active"))
                {
                    query = query.Where(u => !u.IsActive);
                }
                // If both or neither are selected, show all
            }

            // Apply department filter
            if (departmentFilter != null && departmentFilter.Any())
            {
                query = query.Where(u => departmentFilter.Contains(u.Department));
            }

            // Get total count before pagination (for departments only, role filter comes later)
            var totalUsers = await query.CountAsync();

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "username" => sortOrder == "asc" ? query.OrderBy(u => u.UserName) : query.OrderByDescending(u => u.UserName),
                "email" => sortOrder == "asc" ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                "fullname" => sortOrder == "asc" ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName),
                "department" => sortOrder == "asc" ? query.OrderBy(u => u.Department) : query.OrderByDescending(u => u.Department),
                "createddate" => sortOrder == "asc" ? query.OrderBy(u => u.CreatedDate) : query.OrderByDescending(u => u.CreatedDate),
                "isactive" => sortOrder == "asc" ? query.OrderBy(u => u.IsActive) : query.OrderByDescending(u => u.IsActive),
                _ => query.OrderBy(u => u.UserName)
            };

            // Apply pagination
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Build UserViewModels with roles
            var userViewModels = new List<UserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FullName = user.FullName,
                    Departments = string.IsNullOrEmpty(user.Department)? new List<string>(): user.Department.Split(',').Select(d => d.Trim()).ToList(),
                    IsActive = user.IsActive,
                    CreatedDate = user.CreatedDate,
                    Roles = roles.ToList()
                });
            }

            // Apply role filter (post-query because roles are in separate table)
            if (roleFilter != null && roleFilter.Any())
            {
                userViewModels = userViewModels.Where(u => u.Roles.Any(r => roleFilter.Contains(r))).ToList();
                totalUsers = userViewModels.Count; // Recalculate total after role filter
            }

            // Get available filter options - split comma-separated departments
            var allDepartmentsRaw = await _userManager.Users
                .Where(u => u.Department != null && u.Department != "")
                .Select(u => u.Department)
                .ToListAsync();

            // Split comma-separated departments and flatten the list
            var allDepartments = allDepartmentsRaw
                .SelectMany(d => d.Split(',').Select(dept => dept.Trim()))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var roleManager = HttpContext.RequestServices.GetRequiredService<RoleManager<IdentityRole>>();
            var availableRoles = await roleManager.Roles.Select(r => r.Name).ToListAsync();

            // Build view model
            var viewModel = new UserListViewModel
            {
                Users = userViewModels,
                CurrentPage = page,
                PageSize = pageSize,
                TotalUsers = totalUsers,
                TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize),
                SortBy = sortBy,
                SortOrder = sortOrder,
                SearchTerm = searchTerm,
                StatusFilter = statusFilter ?? new List<string>(),
                RoleFilter = roleFilter ?? new List<string>(),
                DepartmentFilter = departmentFilter ?? new List<string>(),
                AvailableDepartments = allDepartments,
                AvailableRoles = availableRoles
            };

            return View(viewModel);
        }

        // GET: UserManagement/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            // Get all roles
            var roleManager = HttpContext.RequestServices.GetRequiredService<RoleManager<IdentityRole>>();
            var allRoles = await roleManager.Roles.Select(r => r.Name).ToListAsync();

            // Get all departments from users - split comma-separated values
            var allDepartmentsRaw = await _context.Users
                .Where(u => u.Department != null && u.Department != "")
                .Select(u => u.Department)
                .ToListAsync();

            // Split comma-separated departments and flatten the list
            var allDepartments = allDepartmentsRaw
                .SelectMany(d => d.Split(',').Select(dept => dept.Trim()))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            // Add departments from Departments table if it exists
            if (_context.Departments != null)
            {
                var managedDepartments = await _context.Departments
                    .Where(d => d.IsActive)
                    .Select(d => d.Name)
                    .ToListAsync();

                allDepartments = allDepartments.Union(managedDepartments).Distinct().OrderBy(d => d).ToList();
            }
            else
            {
                allDepartments = allDepartments.OrderBy(d => d).ToList();
            }

            var model = new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                Departments = string.IsNullOrEmpty(user.Department)
                    ? new List<string>()
                    : user.Department.Split(',').Select(d => d.Trim()).ToList(),
                IsActive = user.IsActive,
                CurrentRoles = userRoles.ToList(),
                AvailableRoles = allRoles.Select(role => new RoleSelectionViewModel
                {
                    RoleName = role,
                    IsSelected = userRoles.Contains(role)
                }).ToList(),
                AvailableDepartments = allDepartments
            };

            return View(model);
        }

        // POST: UserManagement/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model, List<string> selectedRoles)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                // Track all changes in a single dictionary
                var allChanges = new Dictionary<string, (string oldValue, string newValue)>();

                // Track user field changes
                if (user.FullName != model.FullName)
                {
                    allChanges["FullName"] = (user.FullName, model.FullName);
                    user.FullName = model.FullName;
                }

                if (user.Email != model.Email)
                {
                    allChanges["Email"] = (user.Email, model.Email);
                    user.Email = model.Email;
                    user.EmailConfirmed = false; // Require re-confirmation
                }

                if (user.UserName != model.UserName)
                {
                    allChanges["UserName"] = (user.UserName, model.UserName);
                    user.UserName = model.UserName;
                }

                // Handle multiple departments - join with comma
                var newDepartments = model.Departments != null && model.Departments.Any()
                    ? string.Join(",", model.Departments)
                    : null;

                if (user.Department != newDepartments)
                {
                    allChanges["Department"] = (user.Department ?? "", newDepartments ?? "");
                    user.Department = newDepartments;
                }

                user.LastModifiedDate = DateTime.UtcNow;
                user.LastModifiedBy = User.Identity.Name;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Track role changes in the same dictionary
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    selectedRoles = selectedRoles ?? new List<string>();

                    var rolesToRemove = currentRoles.Except(selectedRoles).ToList();
                    var rolesToAdd = selectedRoles.Except(currentRoles).ToList();

                    if (rolesToRemove.Any())
                    {
                        await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                        allChanges["Roles Removed"] = ("", string.Join(", ", rolesToRemove));
                    }

                    if (rolesToAdd.Any())
                    {
                        await _userManager.AddToRolesAsync(user, rolesToAdd);
                        allChanges["Roles Added"] = ("", string.Join(", ", rolesToAdd));
                    }

                    // Log all changes in a SINGLE audit entry
                    if (allChanges.Any())
                    {
                        await _auditService.LogBulkUserUpdateAsync(user.Id, allChanges);
                    }

                    TempData["SuccessMessage"] = "User updated successfully.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // POST: UserManagement/Deactivate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            // Prevent user from deactivating their own account
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (!user.IsActive)
            {
                TempData["ErrorMessage"] = "User is already deactivated.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = false;
            user.DeactivatedDate = DateTime.UtcNow;
            user.DeactivatedBy = User.Identity.Name;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _auditService.LogUserDeactivationAsync(user.Id, User.Identity.Name);
                TempData["SuccessMessage"] = $"User {user.UserName} has been deactivated.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to deactivate user.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: UserManagement/Reactivate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (user.IsActive)
            {
                TempData["ErrorMessage"] = "User is already active.";
                return RedirectToAction(nameof(Index));
            }

            // Check if deactivation was due to password expiry
            bool wasPasswordExpired = user.DeactivatedBy != null && user.DeactivatedBy.Contains("Password Expired");

            user.IsActive = true;
            user.DeactivatedDate = null;
            user.DeactivatedBy = null;

            // Reset password expiry date if reactivating from password expiry
            if (wasPasswordExpired)
            {
                user.PasswordChangedDate = DateTime.UtcNow;
                user.PasswordExpiryDays = 30; // CHANGE THIS VALUE - Set to 30 days or any number you want
                TempData["SuccessMessage"] = $"User {user.UserName} has been reactivated. Password expiry has been reset.";
            }
            else
            {
                TempData["SuccessMessage"] = $"User {user.UserName} has been reactivated.";
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var details = wasPasswordExpired
                    ? $"User reactivated and password expiry reset by {User.Identity.Name}"
                    : $"User reactivated by {User.Identity.Name}";

                await _auditService.LogUserReactivationAsync(user.Id, User.Identity.Name);

                if (wasPasswordExpired)
                {
                    await _auditService.LogAsync(
                        "Password Expiry Reset",
                        "ApplicationUser",
                        user.Id,
                        "PasswordChangedDate",
                        user.PasswordChangedDate?.ToString() ?? "null",
                        DateTime.UtcNow.ToString(),
                        details
                    );
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to reactivate user.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: UserManagement/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            // Prevent user from deleting their own account
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            await _auditService.LogUserDeletionAsync(user.Id, User.Identity.Name);

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User {user.UserName} has been permanently deleted.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete user.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: UserManagement/AuditLogs/5
        public async Task<IActionResult> AuditLogs(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            ViewBag.UserName = user.UserName;
            ViewBag.UserId = id;

            return View();
        }
    }
}