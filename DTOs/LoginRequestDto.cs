using System.ComponentModel.DataAnnotations;
namespace LibraryManagement.Api.DTOs
{
    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public string Password { get; set; } = string.Empty;
    }
}
