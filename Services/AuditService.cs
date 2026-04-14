using Microsoft.AspNetCore.Http;
using MyWeb.Data;
using MyWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyWeb.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, string entityType, string entityId, string fieldName = null, string oldValue = null, string newValue = null, string details = null);
        Task LogUserUpdateAsync(string userId, string fieldName, string oldValue, string newValue);
        Task LogBulkUserUpdateAsync(string userId, Dictionary<string, (string oldValue, string newValue)> changes);
        Task LogUserDeactivationAsync(string userId, string performedBy);
        Task LogUserReactivationAsync(string userId, string performedBy);
        Task LogUserDeletionAsync(string userId, string performedBy);
        Task LogActivityAsync(string userId, string userName, string activityType, string ipAddress, string details = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string entityType, string entityId, string fieldName = null, string oldValue = null, string newValue = null, string details = null)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            var auditLog = new AuditLog
            {
                UserId = userId ?? "System",
                UserName = userName ?? "System",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                IpAddress = ipAddress,
                Details = details,
                ChangesJson = null, // Add this line
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task LogBulkUserUpdateAsync(string userId, Dictionary<string, (string oldValue, string newValue)> changes)
        {
            if (changes == null || !changes.Any())
                return;

            var currentUserId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            // Create a summary of all changes for FieldName
            var changesSummary = string.Join(", ", changes.Select(c => c.Key));

            // Create JSON objects for old and new values
            var oldValues = new Dictionary<string, string>();
            var newValues = new Dictionary<string, string>();

            foreach (var change in changes)
            {
                oldValues[change.Key] = change.Value.oldValue ?? "";
                newValues[change.Key] = change.Value.newValue ?? "";
            }

            // Serialize to formatted JSON - MAKE SURE THESE ARE NOT NULL
            var oldValueJson = JsonSerializer.Serialize(oldValues, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            var newValueJson = JsonSerializer.Serialize(newValues, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            // Debug: Log to console to verify JSON is created
            Console.WriteLine($"Old Value JSON: {oldValueJson}");
            Console.WriteLine($"New Value JSON: {newValueJson}");

            var auditLog = new AuditLog
            {
                UserId = currentUserId ?? "System",
                UserName = userName ?? "System",
                Action = "User Update (Multiple Fields)",
                EntityType = "ApplicationUser",
                EntityId = userId,
                FieldName = changesSummary,
                OldValue = oldValueJson,  // Make sure this is not null
                NewValue = newValueJson,  // Make sure this is not null
                IpAddress = ipAddress,
                Details = $"Updated {changes.Count} field(s) by {userName}",
                ChangesJson = JsonSerializer.Serialize(changes),
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task LogUserUpdateAsync(string userId, string fieldName, string oldValue, string newValue)
        {
            await LogAsync("User Update", "ApplicationUser", userId, fieldName, oldValue, newValue, $"Updated {fieldName}");
        }

        public async Task LogUserDeactivationAsync(string userId, string performedBy)
        {
            await LogAsync("User Deactivated", "ApplicationUser", userId, "IsActive", "True", "False", $"User account deactivated by {performedBy}");
        }

        public async Task LogUserReactivationAsync(string userId, string performedBy)
        {
            await LogAsync("User Reactivated", "ApplicationUser", userId, "IsActive", "False", "True", $"User account reactivated by {performedBy}");
        }

        public async Task LogUserDeletionAsync(string userId, string performedBy)
        {
            await LogAsync("User Deleted", "ApplicationUser", userId, null, null, null, $"User account permanently deleted by {performedBy}");
        }

        public async Task LogActivityAsync(string userId, string userName, string activityType, string ipAddress, string details = null)
        {
            var activity = new ActivityInsight
            {
                UserId = userId,
                UserName = userName,
                ActivityType = activityType,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                Details = details
            };

            _context.ActivityInsights.Add(activity);
            await _context.SaveChangesAsync();
        }
    }
}