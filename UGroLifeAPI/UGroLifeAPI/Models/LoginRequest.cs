using System.ComponentModel.DataAnnotations;

namespace UGroLifeAPI.Models
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email or Username is required.")]
        public string EmailOrUsername { get; set; } 

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}