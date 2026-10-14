namespace LibraryManagement.Api.Models
{
    public class Admin
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // Store a password hash, never a plain-text password.
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
