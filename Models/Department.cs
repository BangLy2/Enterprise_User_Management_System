using System;
using System.ComponentModel.DataAnnotations;

namespace MyWeb.Models
{
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        public string? LastModifiedBy { get; set; }
    }
}