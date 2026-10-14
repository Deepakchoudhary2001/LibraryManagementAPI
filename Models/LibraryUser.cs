using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.Models
{
    public class LibraryUser
    {
        public int Id { get; set; }

        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string MobileNumber { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Links the student's login account to their borrowing record.
        public int? BorrowerId { get; set; }

        public Borrower? Borrower { get; set; }
    }
}
