namespace LibraryManagement.Api.DTOs
{
    public class BookResponseDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string? ISBN { get; set; }

        public string? Description { get; set; }

        public string? Category { get; set; }

        public string? CoverImageUrl { get; set; }

        public int? PublishedYear { get; set; }

        public int TotalCopies { get; set; }

        public int AvailableCopies { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
