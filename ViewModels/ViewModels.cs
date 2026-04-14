using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MyWeb.ViewModels
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public List<string> Departments { get; set; } = new List<string>();
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Full Name")]
        [StringLength(100)]
        public string FullName { get; set; }

        [Display(Name = "Department")]
        public List<string> Departments { get; set; } = new List<string>();

        public bool IsActive { get; set; }

        // Role management
        public List<string> CurrentRoles { get; set; } = new List<string>();
        public List<RoleSelectionViewModel> AvailableRoles { get; set; } = new List<RoleSelectionViewModel>();

        // Department dropdown
        public List<string> AvailableDepartments { get; set; } = new List<string>();
    }

    public class RoleSelectionViewModel
    {
        public string RoleName { get; set; }
        public bool IsSelected { get; set; }
    }

    public class UserListViewModel
    {
        public List<UserViewModel> Users { get; set; } = new List<UserViewModel>();

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public int TotalUsers { get; set; }

        // Sorting
        public string SortBy { get; set; } = "UserName";
        public string SortOrder { get; set; } = "asc";

        // Filtering
        public string SearchTerm { get; set; }
        public List<string> StatusFilter { get; set; } = new List<string>();
        public List<string> RoleFilter { get; set; } = new List<string>();
        public List<string> DepartmentFilter { get; set; } = new List<string>();

        // Available filter options
        public List<string> AvailableDepartments { get; set; } = new List<string>();
        public List<string> AvailableRoles { get; set; } = new List<string>();

        // Helper properties
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public int StartRecord => (CurrentPage - 1) * PageSize + 1;
        public int EndRecord => Math.Min(CurrentPage * PageSize, TotalUsers);
    }

    public class DashboardViewModel
    {
        public string Username { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public Dictionary<string, int> RoleDistribution { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> DepartmentDistribution { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> RegistrationTrend { get; set; } = new Dictionary<string, int>();
        public List<PasswordExpiryWarning> PasswordExpiryWarnings { get; set; } = new List<PasswordExpiryWarning>();
        public List<PasswordExpiryWarning> AllPasswordExpiryWarnings { get; set; } = new List<PasswordExpiryWarning>();

        // Activity Insights
        public Dictionary<string, int> LoginActivityLast30Days { get; set; } = new Dictionary<string, int>();
        public List<FailedLoginAttempt> FailedLoginAttempts { get; set; } = new List<FailedLoginAttempt>();
        public List<RecentUser> RecentlyCreatedUsers { get; set; } = new List<RecentUser>();
        public List<RecentUser> RecentlyDeactivatedUsers { get; set; } = new List<RecentUser>();
        public int TotalLoginsLast30Days { get; set; }
        public int TotalFailedLoginsLast30Days { get; set; }
    }

    public class PasswordExpiryWarning
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public int DaysUntilExpiry { get; set; }
        public DateTime ExpiryDate { get; set; }
    }

    public class FailedLoginAttempt
    {
        public string UserName { get; set; }
        public DateTime AttemptTime { get; set; }
        public string IpAddress { get; set; }
        public int AttemptCount { get; set; }
    }

    public class RecentUser
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public DateTime Date { get; set; }
        public string PerformedBy { get; set; }
    }

    public class BulkUserUploadViewModel
    {
        [Required]
        [Display(Name = "User File (CSV or Excel)")]
        public IFormFile UserFile { get; set; }

        public List<string> AvailableRoles { get; set; } = new List<string>();
        public List<string> AvailableDepartments { get; set; } = new List<string>();
    }

    public class BulkRoleAssignmentViewModel
    {
        // Not required - only needed for manual selection
        public List<string> SelectedUserIds { get; set; } = new List<string>();

        // Not required - only needed for manual selection (REMOVED [Required])
        public string RoleToAssign { get; set; }

        // Not required - only needed for manual selection (REMOVED [Required])
        public string Action { get; set; } // "Add" or "Remove"

        // For file upload
        public IFormFile UserFile { get; set; }

        public List<UserViewModel> AvailableUsers { get; set; } = new List<UserViewModel>();
        public List<string> AvailableRoles { get; set; } = new List<string>();
    }

    public class BulkUploadResult
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> SuccessfulUsers { get; set; } = new List<string>();
    }

    public class PaginatedUserListViewModel
    {
        public List<UserDetailViewModel> Users { get; set; } = new List<UserDetailViewModel>();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalUsers { get; set; }
        public int TotalPages { get; set; }
        public string FilterType { get; set; }
        public string FilterValue { get; set; }
    }

    public class UserDetailViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public string CreatedDate { get; set; }
    }
}