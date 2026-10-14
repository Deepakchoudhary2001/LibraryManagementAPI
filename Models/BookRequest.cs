
namespace LibraryManagement.Api.Models;

public static class BookRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public class BookRequest
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int LibraryUserId { get; set; }
    public LibraryUser LibraryUser { get; set; } = null!;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = BookRequestStatuses.Pending;

    public int? ReviewedByAdminId { get; set; }
    public Admin? ReviewedByAdmin { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    // Populated only when approval creates a loan.
    public int? LoanId { get; set; }
}
