using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Data;
using MyWeb.Models;
using MyWeb.Services;
using MyWeb.ViewModels;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BulkOperationsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;

        public BulkOperationsController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
            _context = context;

            //// Set EPPlus license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // GET: BulkOperations/UploadUsers
        public async Task<IActionResult> UploadUsers()
        {
            var viewModel = new BulkUserUploadViewModel
            {
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                AvailableDepartments = await GetAllDepartmentsAsync()
            };

            return View(viewModel);
        }

        // POST: BulkOperations/UploadUsers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadUsers(BulkUserUploadViewModel model)
        {
            if (model.UserFile == null || model.UserFile.Length == 0)
            {
                ModelState.AddModelError("UserFile", "Please select a CSV or Excel file.");
                model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                model.AvailableDepartments = await GetAllDepartmentsAsync();
                return View(model);
            }

            var fileExtension = Path.GetExtension(model.UserFile.FileName).ToLower();
            if (fileExtension != ".csv" && fileExtension != ".xlsx" && fileExtension != ".xls")
            {
                TempData["ErrorMessage"] = "Please upload a CSV or Excel file (.csv, .xlsx, .xls)";
                return RedirectToAction(nameof(UploadUsers));
            }

            var result = new BulkUploadResult();

            try
            {
                List<Dictionary<string, string>> userData;

                if (fileExtension == ".csv")
                {
                    userData = await ParseCsvFile(model.UserFile);
                }
                else
                {
                    userData = await ParseExcelFile(model.UserFile);
                }

                await ProcessUserData(userData, result);

                TempData["UploadResult"] = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                return RedirectToAction(nameof(UploadResult));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
                return RedirectToAction(nameof(UploadUsers));
            }
        }

        // GET: BulkOperations/UploadResult
        public IActionResult UploadResult()
        {
            if (TempData["UploadResult"] == null)
            {
                return RedirectToAction(nameof(UploadUsers));
            }

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<BulkUploadResult>(TempData["UploadResult"].ToString());
            return View(result);
        }

        // GET: BulkOperations/RoleAssignment
        public async Task<IActionResult> RoleAssignment()
        {
            var users = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.UserName)
                .ToListAsync();

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
                    Roles = roles.ToList()
                });
            }

            var viewModel = new BulkRoleAssignmentViewModel
            {
                AvailableUsers = userViewModels,
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync()
            };

            return View(viewModel);
        }

        // POST: BulkOperations/RoleAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleAssignment(BulkRoleAssignmentViewModel model)
        {
            // Debug: Check what was received
            var hasFile = model.UserFile != null && model.UserFile.Length > 0;

            // Check if file upload or manual selection
            if (hasFile)
            {
                return await ProcessRoleAssignmentFromFile(model);
            }
            else
            {
                return await ProcessRoleAssignmentManual(model);
            }
        }

        // Manual selection of users
        private async Task<IActionResult> ProcessRoleAssignmentManual(BulkRoleAssignmentViewModel model)
        {
            // Validate manual selection inputs
            if (string.IsNullOrWhiteSpace(model.RoleToAssign) || string.IsNullOrWhiteSpace(model.Action))
            {
                TempData["ErrorMessage"] = "Please select a role and action, or upload a file.";
                return RedirectToAction(nameof(RoleAssignment));
            }

            if (!model.SelectedUserIds.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one user.";
                return RedirectToAction(nameof(RoleAssignment));
            }

            int successCount = 0;
            int failureCount = 0;
            var errors = new List<string>();
            bool isAddAction = model.Action == "Add";

            foreach (var userId in model.SelectedUserIds)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    errors.Add($"User with ID {userId} not found.");
                    failureCount++;
                    continue;
                }

                if (isAddAction)
                {
                    if (await _userManager.IsInRoleAsync(user, model.RoleToAssign))
                    {
                        errors.Add($"{user.UserName} already has role '{model.RoleToAssign}'.");
                        failureCount++;
                        continue;
                    }

                    var result = await _userManager.AddToRoleAsync(user, model.RoleToAssign);
                    if (result.Succeeded)
                    {
                        await _auditService.LogAsync("Bulk Role Assigned", "ApplicationUser", user.Id, "Role", null, model.RoleToAssign,
                            $"Role '{model.RoleToAssign}' assigned to {user.UserName} via bulk operation by {User.Identity.Name}");
                        successCount++;
                    }
                    else
                    {
                        var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
                        errors.Add($"{user.UserName}: {errorMessages}");
                        failureCount++;
                    }
                }
                else
                {
                    if (!await _userManager.IsInRoleAsync(user, model.RoleToAssign))
                    {
                        errors.Add($"{user.UserName} does not have role '{model.RoleToAssign}'.");
                        failureCount++;
                        continue;
                    }

                    var result = await _userManager.RemoveFromRoleAsync(user, model.RoleToAssign);
                    if (result.Succeeded)
                    {
                        await _auditService.LogAsync("Bulk Role Removed", "ApplicationUser", user.Id, "Role", model.RoleToAssign, null,
                            $"Role '{model.RoleToAssign}' removed from {user.UserName} via bulk operation by {User.Identity.Name}");
                        successCount++;
                    }
                    else
                    {
                        var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
                        errors.Add($"{user.UserName}: {errorMessages}");
                        failureCount++;
                    }
                }
            }

            if (successCount > 0)
            {
                var actionText = isAddAction ? "assigned to" : "removed from";
                TempData["SuccessMessage"] = $"Successfully {actionText} {successCount} user(s) for role '{model.RoleToAssign}'.";
            }

            if (failureCount > 0)
            {
                TempData["ErrorMessage"] = $"{failureCount} operation(s) failed. " + string.Join(" ", errors.Take(5));
                if (errors.Count > 5)
                {
                    TempData["ErrorMessage"] += $" ... and {errors.Count - 5} more error(s).";
                }
            }

            return RedirectToAction(nameof(RoleAssignment));
        }

        // File upload for role assignment
        private async Task<IActionResult> ProcessRoleAssignmentFromFile(BulkRoleAssignmentViewModel model)
        {
            var fileExtension = Path.GetExtension(model.UserFile.FileName).ToLower();
            if (fileExtension != ".csv" && fileExtension != ".xlsx" && fileExtension != ".xls")
            {
                TempData["ErrorMessage"] = "Please upload a CSV or Excel file (.csv, .xlsx, .xls)";
                return RedirectToAction(nameof(RoleAssignment));
            }

            try
            {
                List<Dictionary<string, string>> fileData;

                if (fileExtension == ".csv")
                {
                    fileData = await ParseCsvFile(model.UserFile);
                }
                else
                {
                    fileData = await ParseExcelFile(model.UserFile);
                }

                int successCount = 0;
                int failureCount = 0;
                var errors = new List<string>();

                foreach (var row in fileData)
                {
                    var username = GetValueFromRow(row, "username");
                    var roleToAssign = GetValueFromRow(row, "role");
                    var action = GetValueFromRow(row, "action")?.ToLower(); // "add" or "remove"

                    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(roleToAssign) || string.IsNullOrWhiteSpace(action))
                    {
                        errors.Add($"Missing required fields for user: {username}");
                        failureCount++;
                        continue;
                    }

                    var user = await _userManager.FindByNameAsync(username);
                    if (user == null)
                    {
                        errors.Add($"User '{username}' not found.");
                        failureCount++;
                        continue;
                    }

                    if (action == "add")
                    {
                        if (await _userManager.IsInRoleAsync(user, roleToAssign))
                        {
                            errors.Add($"{user.UserName} already has role '{roleToAssign}'.");
                            failureCount++;
                            continue;
                        }

                        var result = await _userManager.AddToRoleAsync(user, roleToAssign);
                        if (result.Succeeded)
                        {
                            await _auditService.LogAsync("Bulk Role Assigned (File Upload)", "ApplicationUser", user.Id, "Role", null, roleToAssign,
                                $"Role '{roleToAssign}' assigned to {user.UserName} via file upload by {User.Identity.Name}");
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"{user.UserName}: Failed to add role");
                            failureCount++;
                        }
                    }
                    else if (action == "remove")
                    {
                        if (!await _userManager.IsInRoleAsync(user, roleToAssign))
                        {
                            errors.Add($"{user.UserName} does not have role '{roleToAssign}'.");
                            failureCount++;
                            continue;
                        }

                        var result = await _userManager.RemoveFromRoleAsync(user, roleToAssign);
                        if (result.Succeeded)
                        {
                            await _auditService.LogAsync("Bulk Role Removed (File Upload)", "ApplicationUser", user.Id, "Role", roleToAssign, null,
                                $"Role '{roleToAssign}' removed from {user.UserName} via file upload by {User.Identity.Name}");
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"{user.UserName}: Failed to remove role");
                            failureCount++;
                        }
                    }
                }

                if (successCount > 0)
                {
                    TempData["SuccessMessage"] = $"Successfully processed {successCount} role assignment(s) from file.";
                }

                if (failureCount > 0)
                {
                    TempData["ErrorMessage"] = $"{failureCount} operation(s) failed. " + string.Join(" ", errors.Take(5));
                }

                return RedirectToAction(nameof(RoleAssignment));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
                return RedirectToAction(nameof(RoleAssignment));
            }
        }

        // GET: BulkOperations/DownloadUserTemplate
        public IActionResult DownloadUserTemplate(string format = "csv")
        {
            if (format.ToLower() == "excel")
            {
                return DownloadUserExcelTemplate();
            }
            else
            {
                return DownloadUserCsvTemplate();
            }
        }

        // GET: BulkOperations/DownloadRoleTemplate
        public IActionResult DownloadRoleTemplate(string format = "csv")
        {
            if (format.ToLower() == "excel")
            {
                return DownloadRoleExcelTemplate();
            }
            else
            {
                return DownloadRoleCsvTemplate();
            }
        }

        private IActionResult DownloadUserCsvTemplate()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Username,Email,Password,FullName,Department,Roles");
            csv.AppendLine("john.doe,john.doe@example.com,Password123!,John Doe,IT,User");
            csv.AppendLine("jane.smith,jane.smith@example.com,Password123!,Jane Smith,HR,User;Manager");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "bulk_user_upload_template.csv");
        }

        private IActionResult DownloadUserExcelTemplate()
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Users");

                // Headers
                worksheet.Cells[1, 1].Value = "Username";
                worksheet.Cells[1, 2].Value = "Email";
                worksheet.Cells[1, 3].Value = "Password";
                worksheet.Cells[1, 4].Value = "FullName";
                worksheet.Cells[1, 5].Value = "Department";
                worksheet.Cells[1, 6].Value = "Roles";

                // Style headers
                using (var range = worksheet.Cells[1, 1, 1, 6])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Sample data
                worksheet.Cells[2, 1].Value = "john.doe";
                worksheet.Cells[2, 2].Value = "john.doe@example.com";
                worksheet.Cells[2, 3].Value = "Password123!";
                worksheet.Cells[2, 4].Value = "John Doe";
                worksheet.Cells[2, 5].Value = "IT";
                worksheet.Cells[2, 6].Value = "User";

                worksheet.Cells[3, 1].Value = "jane.smith";
                worksheet.Cells[3, 2].Value = "jane.smith@example.com";
                worksheet.Cells[3, 3].Value = "Password123!";
                worksheet.Cells[3, 4].Value = "Jane Smith";
                worksheet.Cells[3, 5].Value = "HR";
                worksheet.Cells[3, 6].Value = "User;Manager";

                worksheet.Cells.AutoFitColumns();

                var bytes = package.GetAsByteArray();
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bulk_user_upload_template.xlsx");
            }
        }

        private IActionResult DownloadRoleCsvTemplate()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Username,Role,Action");
            csv.AppendLine("john.doe,Manager,add");
            csv.AppendLine("jane.smith,Supervisor,add");
            csv.AppendLine("bob.johnson,User,remove");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "bulk_role_assignment_template.csv");
        }

        private IActionResult DownloadRoleExcelTemplate()
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Role Assignments");

                // Headers
                worksheet.Cells[1, 1].Value = "Username";
                worksheet.Cells[1, 2].Value = "Role";
                worksheet.Cells[1, 3].Value = "Action";

                // Style headers
                using (var range = worksheet.Cells[1, 1, 1, 3])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Sample data
                worksheet.Cells[2, 1].Value = "john.doe";
                worksheet.Cells[2, 2].Value = "Manager";
                worksheet.Cells[2, 3].Value = "add";

                worksheet.Cells[3, 1].Value = "jane.smith";
                worksheet.Cells[3, 2].Value = "Supervisor";
                worksheet.Cells[3, 3].Value = "add";

                worksheet.Cells[4, 1].Value = "bob.johnson";
                worksheet.Cells[4, 2].Value = "User";
                worksheet.Cells[4, 3].Value = "remove";

                worksheet.Cells.AutoFitColumns();

                var bytes = package.GetAsByteArray();
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bulk_role_assignment_template.xlsx");
            }
        }

        // Helper methods for parsing files
        private async Task<List<Dictionary<string, string>>> ParseCsvFile(IFormFile file)
        {
            var result = new List<Dictionary<string, string>>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                var headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(headerLine))
                {
                    return result;
                }

                var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToArray();
                string line;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var values = ParseCsvLine(line);
                    var row = new Dictionary<string, string>();

                    for (int i = 0; i < headers.Length && i < values.Length; i++)
                    {
                        row[headers[i]] = values[i];
                    }

                    result.Add(row);
                }
            }

            return result;
        }

        private async Task<List<Dictionary<string, string>>> ParseExcelFile(IFormFile file)
        {
            var result = new List<Dictionary<string, string>>();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;
                    var colCount = worksheet.Dimension.Columns;

                    // Read headers
                    var headers = new List<string>();
                    for (int col = 1; col <= colCount; col++)
                    {
                        headers.Add(worksheet.Cells[1, col].Value?.ToString()?.Trim().ToLower() ?? "");
                    }

                    // Read data rows
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowData = new Dictionary<string, string>();
                        bool hasData = false;

                        for (int col = 1; col <= colCount; col++)
                        {
                            var value = worksheet.Cells[row, col].Value?.ToString()?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(value))
                            {
                                hasData = true;
                            }
                            rowData[headers[col - 1]] = value;
                        }

                        if (hasData)
                        {
                            result.Add(rowData);
                        }
                    }
                }
            }

            return result;
        }

        private async Task ProcessUserData(List<Dictionary<string, string>> userData, BulkUploadResult result)
        {
            result.TotalRows = userData.Count;

            foreach (var row in userData)
            {
                try
                {
                    var username = GetValueFromRow(row, "username");
                    var email = GetValueFromRow(row, "email");
                    var password = GetValueFromRow(row, "password");
                    var fullName = GetValueFromRow(row, "fullname");
                    var department = GetValueFromRow(row, "department");
                    var roles = GetValueFromRow(row, "roles");

                    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    {
                        result.Errors.Add($"Row {result.TotalRows - userData.Count + 1}: Username, Email, and Password are required.");
                        result.FailureCount++;
                        continue;
                    }

                    var existingUser = await _userManager.FindByNameAsync(username);
                    if (existingUser != null)
                    {
                        result.Errors.Add($"User '{username}' already exists.");
                        result.FailureCount++;
                        continue;
                    }

                    var existingEmail = await _userManager.FindByEmailAsync(email);
                    if (existingEmail != null)
                    {
                        result.Errors.Add($"Email '{email}' is already in use.");
                        result.FailureCount++;
                        continue;
                    }

                    var user = new ApplicationUser
                    {
                        UserName = username,
                        Email = email,
                        FullName = fullName,
                        Department = department,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        PasswordChangedDate = DateTime.UtcNow
                    };

                    var createResult = await _userManager.CreateAsync(user, password);

                    if (createResult.Succeeded)
                    {
                        var roleList = string.IsNullOrWhiteSpace(roles)
                            ? new[] { "User" }
                            : roles.Split(';').Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r)).ToArray();

                        foreach (var role in roleList)
                        {
                            if (await _roleManager.RoleExistsAsync(role))
                            {
                                await _userManager.AddToRoleAsync(user, role);
                            }
                        }

                        await _auditService.LogAsync("Bulk User Created", "ApplicationUser", user.Id, null, null, null,
                            $"User '{username}' created via bulk upload by {User.Identity.Name}");

                        result.SuccessCount++;
                        result.SuccessfulUsers.Add(username);
                    }
                    else
                    {
                        var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                        result.Errors.Add($"{username}: {errors}");
                        result.FailureCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error: {ex.Message}");
                    result.FailureCount++;
                }
            }
        }

        private string GetValueFromRow(Dictionary<string, string> row, string columnName)
        {
            return row.ContainsKey(columnName.ToLower()) ? row[columnName.ToLower()] : string.Empty;
        }

        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var currentValue = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(currentValue.ToString());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }

            values.Add(currentValue.ToString());
            return values.ToArray();
        }

        private async Task<List<string>> GetAllDepartmentsAsync()
        {
            var managedDepartments = await _context.Departments
                .Where(d => d.IsActive)
                .Select(d => d.Name)
                .ToListAsync();

            var userDepartments = await _context.Users
                .Where(u => u.Department != null && u.Department != "")
                .Select(u => u.Department)
                .ToListAsync();

            var splitDepartments = userDepartments
                .SelectMany(d => d.Split(',').Select(dept => dept.Trim()))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            return managedDepartments
                .Union(splitDepartments)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
        }
    }
}