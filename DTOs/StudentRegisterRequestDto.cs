using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.DTOs
{
    public class StudentRegisterRequestDto
    {
        [Required]
        [StringLength(150, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20, MinimumLength = 7)]
        public string MobileNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;
    }
}
