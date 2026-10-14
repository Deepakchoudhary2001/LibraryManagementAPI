namespace LibraryManagement.Api.Models
{
    public class Loan
    {
        public int Id { get; set; }

        public int BookId { get; set; }

        public Book Book { get; set; } = null!;

        public int BorrowerId { get; set; }

        public Borrower Borrower { get; set; } = null!;

        public int IssuedByAdminId { get; set; }

        public Admin IssuedByAdmin { get; set; } = null!;

        public DateTime IssueDate { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime? ReturnDate { get; set; }

        public string? Notes { get; set; }

        public DateTime? ReturnRequestedAt { get; set; }

        public string? ReturnRequestStatus { get; set; }

        public int? OverdueDaysAtRequest { get; set; }

        public decimal? FineAmount { get; set; }
    }
}
