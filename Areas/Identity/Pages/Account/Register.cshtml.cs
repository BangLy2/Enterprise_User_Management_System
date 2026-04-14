using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyWeb.Data;
using MyWeb.Models;
using MyWeb.Services;

namespace MyWeb.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IAuditService auditService,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _auditService = auditService;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public List<string> AvailableDepartments { get; set; } = new List<string>();
        public List<string> AvailableRoles { get; set; } = new List<string>();

        public class InputModel
        {
            [Required]
            [Display(Name = "Username")]
            [StringLength(50, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 3)]
            public string UserName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Display(Name = "Full Name")]
            [StringLength(100)]
            public string FullName { get; set; }

            [Display(Name = "Departments")]
            public List<string> Departments { get; set; } = new List<string>();

            [Display(Name = "Roles")]
            public List<string> Roles { get; set; } = new List<string>();

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            await LoadDepartmentsAsync();
            await LoadRolesAsync();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                user.FullName = Input.FullName;
                user.Department = Input.Departments != null && Input.Departments.Any()
                    ? string.Join(",", Input.Departments)
                    : null;
                user.IsActive = true;
                user.CreatedDate = DateTime.UtcNow;
                user.PasswordChangedDate = DateTime.UtcNow;

                await _userStore.SetUserNameAsync(user, Input.UserName, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    // Add selected roles or default to "User" role if none selected
                    if (Input.Roles != null && Input.Roles.Any())
                    {
                        foreach (var role in Input.Roles)
                        {
                            await _userManager.AddToRoleAsync(user, role);
                        }
                    }
                    else
                    {
                        // Default to "User" role if no roles selected
                        await _userManager.AddToRoleAsync(user, "User");
                    }

                    // Log user creation in audit
                    var rolesAssigned = Input.Roles != null && Input.Roles.Any()
                        ? string.Join(", ", Input.Roles)
                        : "User";
                    await _auditService.LogAsync("User Created", "ApplicationUser", user.Id, null, null, null,
                        $"New user registered: {user.UserName} with roles: {rolesAssigned}");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            await LoadDepartmentsAsync();
            await LoadRolesAsync();
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }

        private async Task LoadDepartmentsAsync()
        {
            var managedDepartments = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => d.Name)
                .ToListAsync();

            var userDepartments = await _context.Users
                .Where(u => u.Department != null && u.Department != "")
                .Select(u => u.Department)
                .ToListAsync();

            var splitDepartments = userDepartments
                .SelectMany(d => d.Split(','))
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            AvailableDepartments = managedDepartments
                .Union(splitDepartments)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
        }

        private async Task LoadRolesAsync()
        {
            // Get all roles except Admin
            AvailableRoles = await _roleManager.Roles
                .Where(r => r.Name != "Admin")
                .Select(r => r.Name)
                .OrderBy(r => r)
                .ToListAsync();
        }
    }
}