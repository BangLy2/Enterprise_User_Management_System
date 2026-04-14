using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MyWeb.Models;
using MyWeb.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace MyWeb.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IAuditService _auditService;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditService = auditService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Username")]
            public string Username { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var user = await _userManager.FindByEmailAsync(Input.Username);

                if (user == null)
                {
                    user = await _userManager.FindByNameAsync(Input.Username);
                }

                if (user != null)
                {
                    // Check if user is Admin - admins don't have password expiry
                    var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                    if (!isAdmin) // Only check password expiry for non-admin users
                    {
                        // Check if password has expired and deactivate if necessary
                        DateTime expiryDate;
                        if (user.PasswordChangedDate.HasValue)
                        {
                            expiryDate = user.PasswordChangedDate.Value.AddDays(user.PasswordExpiryDays);
                        }
                        else
                        {
                            expiryDate = user.CreatedDate.AddDays(user.PasswordExpiryDays);
                        }

                        if (DateTime.UtcNow > expiryDate && user.IsActive)
                        {
                            // Auto-deactivate due to password expiry
                            user.IsActive = false;
                            user.DeactivatedDate = DateTime.UtcNow;
                            user.DeactivatedBy = "System (Password Expired)";
                            await _userManager.UpdateAsync(user);

                            await _auditService.LogAsync(
                                "User Auto-Deactivated (Password Expired)",
                                "ApplicationUser",
                                user.Id,
                                "IsActive",
                                "True",
                                "False",
                                $"User {user.UserName} automatically deactivated at login due to password expiry"
                            );

                            ModelState.AddModelError(string.Empty, "Your password has expired. Your account has been deactivated. Please contact an administrator.");
                            return Page();
                        }
                    }

                    // Check if user is active
                    if (!user.IsActive)
                    {
                        var message = user.DeactivatedBy != null && user.DeactivatedBy.Contains("Password Expired")
                            ? "Your password has expired and your account has been deactivated. Please contact an administrator to reactivate your account."
                            : "Your account has been deactivated. Please contact an administrator.";

                        ModelState.AddModelError(string.Empty, message);
                        return Page();
                    }

                    var result = await _signInManager.PasswordSignInAsync(user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User logged in.");
                        await _auditService.LogActivityAsync(user.Id, user.UserName, "Login", ipAddress, "Successful login");
                        return RedirectToAction("Dashboard", "Home");
                    }
                    if (result.RequiresTwoFactor)
                    {
                        return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                    }
                    if (result.IsLockedOut)
                    {
                        _logger.LogWarning("User account locked out.");
                        return RedirectToPage("./Lockout");
                    }
                    else
                    {
                        await _auditService.LogActivityAsync(user.Id, user.UserName, "FailedLogin", ipAddress, "Invalid password");
                    }
                }
                else
                {
                    await _auditService.LogActivityAsync(Input.Username, Input.Username, "FailedLogin", ipAddress, "User not found");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            return Page();
        }
    }
}