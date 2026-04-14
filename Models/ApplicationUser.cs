using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MyWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string? FullName { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? DeactivatedDate { get; set; }

        public string? DeactivatedBy { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        public string? LastModifiedBy { get; set; }

        public DateTime? PasswordChangedDate { get; set; }
        public int PasswordExpiryDays { get; set; } = 90; // Default 90 days



    }
}
