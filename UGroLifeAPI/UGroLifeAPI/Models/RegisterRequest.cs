using System.ComponentModel.DataAnnotations; 

namespace UGroLifeAPI.Models
{
    public class RegisterRequest
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        [Required]
        public string Mobile { get; set; } = string.Empty;
        [Required]
        [EmailAddress] 
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")] 
        public string Password { get; set; } = string.Empty;

    }
}