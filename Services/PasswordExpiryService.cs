using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyWeb.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyWeb.Services
{
    public class PasswordExpiryService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PasswordExpiryService> _logger;

        public PasswordExpiryService(IServiceProvider serviceProvider, ILogger<PasswordExpiryService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Password Expiry Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckExpiredPasswordsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking expired passwords.");
                }

                // Check every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task CheckExpiredPasswordsAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

                var users = userManager.Users.Where(u => u.IsActive).ToList();
                var now = DateTime.UtcNow;

                foreach (var user in users)
                {
                    // Skip admin users - their passwords don't expire
                    var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
                    if (isAdmin)
                    {
                        continue;
                    }

                    DateTime expiryDate;

                    if (user.PasswordChangedDate.HasValue)
                    {
                        expiryDate = user.PasswordChangedDate.Value.AddDays(user.PasswordExpiryDays);
                    }
                    else
                    {
                        expiryDate = user.CreatedDate.AddDays(user.PasswordExpiryDays);
                    }

                    // Check if password has expired
                    if (now > expiryDate)
                    {
                        user.IsActive = false;
                        user.DeactivatedDate = DateTime.UtcNow;
                        user.DeactivatedBy = "System (Password Expired)";

                        await userManager.UpdateAsync(user);

                        await auditService.LogAsync(
                            "User Auto-Deactivated (Password Expired)",
                            "ApplicationUser",
                            user.Id,
                            "IsActive",
                            "True",
                            "False",
                            $"User {user.UserName} automatically deactivated due to password expiry"
                        );

                        _logger.LogInformation($"User {user.UserName} deactivated due to password expiry.");
                    }
                }
            }
        }
    }
}