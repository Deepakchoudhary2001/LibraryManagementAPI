using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.DTOs
{
    public class UpdateBookDto
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(150, MinimumLength = 1)]
        public string Author { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ISBN { get; set; }

        public string? Description { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }

        [Url]
        [StringLength(2048)]
        public string? CoverImageUrl { get; set; }

        [Range(1, 9999)]
        public int? PublishedYear { get; set; }

        [Range(1, 10000)]
        public int TotalCopies { get; set; } = 1;
    }
}
