namespace LibraryManagement.Api.Models
{
    public class Book
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string? ISBN { get; set; }

        public string? Description { get; set; }

        public string? Category { get; set; }

        public string? CoverImageUrl { get; set; }

        public int? PublishedYear { get; set; }

        public int TotalCopies { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property: one book can have many loan records.
        public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    }
}
