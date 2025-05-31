using System;
using System.ComponentModel.DataAnnotations;

namespace UGroLifeAPI.Models
{
    public class User
    {
        [Key] 
        public int Id { get; set; }
        [Required]
        public string Mobile { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)] 
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(255)] 
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)] 
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}