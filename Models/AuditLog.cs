using System.ComponentModel.DataAnnotations;

namespace MyWeb.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; }

        [StringLength(100)]
        public string? UserName { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; }

        [StringLength(100)]
        public string? EntityType { get; set; }

        [StringLength(450)]
        public string? EntityId { get; set; }

        [StringLength(100)]
        public string? FieldName { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? IpAddress { get; set; }

        public string? Details { get; set; }

        public string? ChangesJson { get; set; }

    }
}
