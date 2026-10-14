namespace LibraryManagement.Api.Models
{
    public class Borrower
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string MobileNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    }
}
