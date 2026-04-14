using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Data;
using MyWeb.Models;
using MyWeb.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MyWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public HomeController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            // Get role distribution
            var roles = await _roleManager.Roles.ToListAsync();
            var roleDistribution = new Dictionary<string, int>();

            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                roleDistribution[role.Name] = usersInRole.Count;
            }

            // Get department distribution
            var users = await _userManager.Users.Where(u => u.Department != null).ToListAsync();
            var departmentDistribution = new Dictionary<string, int>();

            foreach (var user in users)
            {
                if (!string.IsNullOrEmpty(user.Department))
                {
                    var departments = user.Department.Split(',').Select(d => d.Trim());
                    foreach (var dept in departments)
                    {
                        if (departmentDistribution.ContainsKey(dept))
                        {
                            departmentDistribution[dept]++;
                        }
                        else
                        {
                            departmentDistribution[dept] = 1;
                        }
                    }
                }
            }

            // Get user registration trend (last 6 months)
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var allUsers = await _userManager.Users.ToListAsync();
            var usersByMonth = allUsers
                .Where(u => u.CreatedDate >= sixMonthsAgo)
                .GroupBy(u => new { u.CreatedDate.Year, u.CreatedDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            var registrationTrend = new Dictionary<string, int>();
            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.UtcNow.AddMonths(-i);
                var monthKey = date.ToString("MMM yyyy");
                registrationTrend[monthKey] = 0;
            }

            foreach (var item in usersByMonth)
            {
                var monthKey = new DateTime(item.Year, item.Month, 1).ToString("MMM yyyy");
                if (registrationTrend.ContainsKey(monthKey))
                {
                    registrationTrend[monthKey] = item.Count;
                }
            }

            // Get password expiry warnings (including expired)
            // Get password expiry warnings (including expired) - EXCLUDE ADMIN USERS
            var passwordExpiryWarnings = new List<PasswordExpiryWarning>();

            foreach (var user in allUsers)
            {
                // Skip admin users - their passwords don't expire
                var userRoles = await _userManager.GetRolesAsync(user);
                if (userRoles.Contains("Admin"))
                {
                    continue;
                }

                if (user.IsActive || (!user.IsActive && user.DeactivatedBy != null && user.DeactivatedBy.Contains("Password Expired")))
                {
                    DateTime expiryDate;

                    if (user.PasswordChangedDate.HasValue)
                    {
                        expiryDate = user.PasswordChangedDate.Value.AddDays(user.PasswordExpiryDays);
                    }
                    else
                    {
                        expiryDate = user.CreatedDate.AddDays(user.PasswordExpiryDays);
                    }

                    var daysUntilExpiry = (expiryDate - DateTime.UtcNow).Days;

                    // Show all warnings (we'll filter in the view with JavaScript)
                    if (daysUntilExpiry <= 30) // Extended to 30 days for filtering
                    {
                        passwordExpiryWarnings.Add(new PasswordExpiryWarning
                        {
                            UserId = user.Id,
                            UserName = user.UserName,
                            Email = user.Email,
                            FullName = user.FullName,
                            DaysUntilExpiry = daysUntilExpiry,
                            ExpiryDate = expiryDate
                        });
                    }
                }
            }

            passwordExpiryWarnings = passwordExpiryWarnings.OrderBy(x => x.DaysUntilExpiry).ToList();

            // Store all warnings
            var allPasswordExpiryWarnings = passwordExpiryWarnings.ToList();

            // === USER ACTIVITY INSIGHTS ===
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            // Get login activity for last 30 days
            var loginActivities = await _context.ActivityInsights
                .Where(a => a.ActivityType == "Login" && a.Timestamp >= thirtyDaysAgo)
                .GroupBy(a => a.Timestamp.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var loginActivityLast30Days = new Dictionary<string, int>();
            for (int i = 29; i >= 0; i--)
            {
                var date = DateTime.UtcNow.AddDays(-i).Date;
                var dateKey = date.ToString("MMM dd");
                loginActivityLast30Days[dateKey] = 0;
            }

            foreach (var item in loginActivities)
            {
                var dateKey = item.Date.ToString("MMM dd");
                if (loginActivityLast30Days.ContainsKey(dateKey))
                {
                    loginActivityLast30Days[dateKey] = item.Count;
                }
            }

            // Get failed login attempts
            var failedLogins = await _context.ActivityInsights
                .Where(a => a.ActivityType == "FailedLogin" && a.Timestamp >= thirtyDaysAgo)
                .GroupBy(a => new { a.UserName, a.IpAddress })
                .Select(g => new FailedLoginAttempt
                {
                    UserName = g.Key.UserName,
                    IpAddress = g.Key.IpAddress,
                    AttemptTime = g.Max(x => x.Timestamp),
                    AttemptCount = g.Count()
                })
                .OrderByDescending(x => x.AttemptTime)
                .Take(5)
                .ToListAsync();

            // Get recently created users
            var recentlyCreatedUsers = await _userManager.Users
                .Where(u => u.CreatedDate >= sevenDaysAgo)
                .OrderByDescending(u => u.CreatedDate)
                .Take(5)
                .Select(u => new RecentUser
                {
                    UserId = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    FullName = u.FullName,
                    Date = u.CreatedDate,
                    PerformedBy = "Self-Registration"
                })
                .ToListAsync();

            // Get recently deactivated users
            var recentlyDeactivatedUsers = await _userManager.Users
                .Where(u => !u.IsActive && u.DeactivatedDate.HasValue && u.DeactivatedDate >= sevenDaysAgo)
                .OrderByDescending(u => u.DeactivatedDate)
                .Take(5)
                .Select(u => new RecentUser
                {
                    UserId = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    FullName = u.FullName,
                    Date = u.DeactivatedDate.Value,
                    PerformedBy = u.DeactivatedBy ?? "Unknown"
                })
                .ToListAsync();

            // Get detailed user data for chart interactions
            var allUsersWithDetails = new List<object>();
            foreach (var user in allUsers)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                var etTime = TimeZoneInfo.ConvertTimeFromUtc(user.CreatedDate, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

                allUsersWithDetails.Add(new
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FullName = user.FullName,
                    Department = user.Department,
                    IsActive = user.IsActive,
                    Roles = userRoles.ToList(),
                    CreatedDate = etTime.ToString("MM/dd/yyyy hh:mm tt")
                });
            }

            ViewBag.AllUsersData = System.Text.Json.JsonSerializer.Serialize(allUsersWithDetails);

            var viewModel = new DashboardViewModel
            {
                Username = User.Identity.Name,
                TotalUsers = await _userManager.Users.CountAsync(),
                ActiveUsers = await _userManager.Users.Where(u => u.IsActive).CountAsync(),
                InactiveUsers = await _userManager.Users.Where(u => !u.IsActive).CountAsync(),
                RoleDistribution = roleDistribution.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value),
                DepartmentDistribution = departmentDistribution.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value),
                RegistrationTrend = registrationTrend,
                PasswordExpiryWarnings = passwordExpiryWarnings.Where(w => w.DaysUntilExpiry <= 30).ToList(), // Default: 30 days
                AllPasswordExpiryWarnings = allPasswordExpiryWarnings, // All warnings for filtering

                // Activity Insights
                LoginActivityLast30Days = loginActivityLast30Days,
                FailedLoginAttempts = failedLogins,
                RecentlyCreatedUsers = recentlyCreatedUsers,
                RecentlyDeactivatedUsers = recentlyDeactivatedUsers,
                TotalLoginsLast30Days = loginActivities.Sum(x => x.Count),
                TotalFailedLoginsLast30Days = failedLogins.Sum(x => x.AttemptCount)
            };

            return View(viewModel);
        }

        // API endpoint for paginated user data
        [HttpGet]
        public async Task<IActionResult> GetFilteredUsers(string filterType, string filterValue, int page = 1, int pageSize = 5, string searchTerm = "")
        {
            var allUsers = await _userManager.Users.ToListAsync();
            var filteredUsers = new List<UserDetailViewModel>();

            foreach (var user in allUsers)
            {
                var userRoles = await _userManager.GetRolesAsync(user);

                bool matches = false;

                if (filterType == "role")
                {
                    matches = userRoles.Contains(filterValue);
                }
                else if (filterType == "department")
                {
                    if (!string.IsNullOrEmpty(user.Department))
                    {
                        var depts = user.Department.Split(',').Select(d => d.Trim());
                        matches = depts.Contains(filterValue);
                    }
                }

                if (matches)
                {
                    var etTime = TimeZoneInfo.ConvertTimeFromUtc(user.CreatedDate, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

                    var userDetail = new UserDetailViewModel
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        FullName = user.FullName,
                        Department = user.Department,
                        IsActive = user.IsActive,
                        Roles = userRoles.ToList(),
                        CreatedDate = etTime.ToString("MM/dd/yyyy hh:mm tt")
                    };

                    filteredUsers.Add(userDetail);
                }
            }

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                filteredUsers = filteredUsers.Where(u =>
                    (u.UserName?.ToLower().Contains(searchTerm) ?? false) ||
                    (u.Email?.ToLower().Contains(searchTerm) ?? false) ||
                    (u.FullName?.ToLower().Contains(searchTerm) ?? false) ||
                    (u.Department?.ToLower().Contains(searchTerm) ?? false)
                ).ToList();
            }

            // Calculate pagination
            var totalUsers = filteredUsers.Count;
            var totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
            var paginatedUsers = filteredUsers
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PaginatedUserListViewModel
            {
                Users = paginatedUsers,
                CurrentPage = page,
                PageSize = pageSize,
                TotalUsers = totalUsers,
                TotalPages = totalPages,
                FilterType = filterType,
                FilterValue = filterValue
            };

            return Json(result);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}