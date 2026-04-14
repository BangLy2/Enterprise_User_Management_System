using System;

namespace MyWeb.Models
{
    public class ActivityInsight
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string ActivityType { get; set; } // Login, FailedLogin, UserCreated, UserDeactivated, etc.
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; }
        public string Details { get; set; }
    }
}