
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.Models;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LibraryManagement.Api.Controllers;

[ApiController]
[Route("api/borrowrequests")]
[Authorize]
public class BorrowRequestsController : ControllerBase
{
    private readonly LibraryDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly BookCacheVersionService _cacheVersion;

    public BorrowRequestsController(
        LibraryDbContext db,
        IMemoryCache cache,
        BookCacheVersionService cacheVersion)
    {
        _db = db;
        _cache = cache;
        _cacheVersion = cacheVersion;
    }

    private int? CurrentUserId
    {
        get
        {
            var value =
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(value, out var id) ? id : null;
        }
    }

    // POST /api/borrowrequests
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateBookRequestDto dto)
    {
        var userId = CurrentUserId;
        if (userId is null)
            return Unauthorized();

        var user = await _db.LibraryUsers
            .Include(u => u.Borrower)
            .SingleOrDefaultAsync(u => u.Id == userId.Value);

        if (user is null || !user.IsActive)
            return Unauthorized();

        var book = await _db.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == dto.BookId);

        if (book is null)
            return NotFound(new { message = "Book not found." });

        if (user.BorrowerId is null)
        {
            // Backfill accounts created before borrower linking was added.
            var borrower = new Borrower
            {
                FullName = user.FullName,
                Email = user.Email,
                MobileNumber = user.MobileNumber,
                CreatedAt = DateTime.UtcNow
            };

            _db.Borrowers.Add(borrower);
            user.Borrower = borrower;
            await _db.SaveChangesAsync();
        }

        var borrowerId = user.BorrowerId!.Value;

        var alreadyBorrowed = await _db.Loans.AnyAsync(l =>
            l.BookId == dto.BookId &&
            l.BorrowerId == borrowerId &&
            l.ReturnDate == null);

        if (alreadyBorrowed)
        {
            return Conflict(new
            {
                message = "You already have an active loan for this book."
            });
        }

        var pendingExists = await _db.BookRequests.AnyAsync(r =>
            r.BookId == dto.BookId &&
            r.LibraryUserId == user.Id &&
            r.Status == BookRequestStatuses.Pending);

        if (pendingExists)
        {
            return Conflict(new
            {
                message = "You already have a pending request for this book."
            });
        }

        // A request can be made when all copies are currently issued.
        // Availability is checked again during approval.
        var request = new BookRequest
        {
            BookId = dto.BookId,
            LibraryUserId = user.Id,
            RequestedAt = DateTime.UtcNow,
            Status = BookRequestStatuses.Pending
        };

        _db.BookRequests.Add(request);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new
        {
            request.Id,
            request.BookId,
            book.Title,
            request.RequestedAt,
            request.Status
        });
    }

    // GET /api/borrowrequests/my
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyRequests()
    {
        var userId = CurrentUserId;
        if (userId is null)
            return Unauthorized();

        var requests = await _db.BookRequests
            .AsNoTracking()
            .Where(r => r.LibraryUserId == userId.Value)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id,
                r.BookId,
                BookTitle = r.Book.Title,
                r.Book.CoverImageUrl,
                r.RequestedAt,
                r.Status,
                r.RejectionReason,
                r.LoanId
            })
            .ToListAsync();

        return Ok(requests);
    }

    // GET /api/borrowrequests/pending
    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var requests = await _db.BookRequests
            .AsNoTracking()
            .Where(r => r.Status == BookRequestStatuses.Pending)
            .OrderBy(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id,
                r.RequestedAt,
                r.Status,
                Book = new
                {
                    r.Book.Id,
                    r.Book.Title,
                    r.Book.Author,
                    r.Book.CoverImageUrl,
                    r.Book.TotalCopies,
                    AvailableCopies = r.Book.TotalCopies -
                        r.Book.Loans.Count(l => l.ReturnDate == null)
                },
                Student = new
                {
                    r.LibraryUser.Id,
                    r.LibraryUser.FullName,
                    r.LibraryUser.Email,
                    r.LibraryUser.MobileNumber
                }
            })
            .ToListAsync();

        return Ok(requests);
    }

    // POST /api/borrowrequests/{id}/approve
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveRequest(int id)
    {
        var adminId = CurrentUserId;
        if (adminId is null)
            return Unauthorized();

        // Serializable isolation helps prevent concurrent approvals
        // from issuing more copies than the library owns.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var request = await _db.BookRequests
            .Include(r => r.Book)
            .Include(r => r.LibraryUser)
            .SingleOrDefaultAsync(r => r.Id == id);

        if (request is null)
            return NotFound(new { message = "Request not found." });

        if (request.Status != BookRequestStatuses.Pending)
        {
            return Conflict(new
            {
                message = "This request has already been reviewed."
            });
        }

        if (request.LibraryUser.BorrowerId is null)
        {
            return Conflict(new
            {
                message = "The student has no linked borrower record."
            });
        }

        var borrowerId = request.LibraryUser.BorrowerId.Value;

        var alreadyBorrowed = await _db.Loans.AnyAsync(l =>
            l.BookId == request.BookId &&
            l.BorrowerId == borrowerId &&
            l.ReturnDate == null);

        if (alreadyBorrowed)
        {
            return Conflict(new
            {
                message = "The student already has this book on loan."
            });
        }

        var activeLoans = await _db.Loans.CountAsync(l =>
            l.BookId == request.BookId &&
            l.ReturnDate == null);

        if (activeLoans >= request.Book.TotalCopies)
        {
            return Conflict(new
            {
                message = "No copies are currently available. The request remains pending."
            });
        }

        var issueDate = DateTime.UtcNow;
        var dueDate = issueDate.AddDays(14);

        var loan = new Loan
        {
            BookId = request.BookId,
            BorrowerId = borrowerId,
            IssuedByAdminId = adminId.Value,
            IssueDate = issueDate,
            DueDate = dueDate,
            Notes = $"Approved from request #{request.Id}"
        };

        _db.Loans.Add(loan);
        await _db.SaveChangesAsync();

        request.Status = BookRequestStatuses.Approved;
        request.ReviewedByAdminId = adminId.Value;
        request.ReviewedAt = DateTime.UtcNow;
        request.LoanId = loan.Id;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        _cacheVersion.InvalidateLists();
        _cache.Remove($"books:detail:{request.BookId}");

        return Ok(new
        {
            message = "Request approved and book issued.",
            requestId = request.Id,
            loanId = loan.Id,
            bookId = request.BookId,
            studentName = request.LibraryUser.FullName,
            issueDate = loan.IssueDate,
            dueDate = loan.DueDate
        });
    }

    // POST /api/borrowrequests/{id}/reject
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectRequest(
        int id,
        [FromBody] RejectBookRequestDto dto)
    {
        var adminId = CurrentUserId;
        if (adminId is null)
            return Unauthorized();

        var request = await _db.BookRequests
            .SingleOrDefaultAsync(r => r.Id == id);

        if (request is null)
            return NotFound(new { message = "Request not found." });

        if (request.Status != BookRequestStatuses.Pending)
        {
            return Conflict(new
            {
                message = "This request has already been reviewed."
            });
        }

        request.Status = BookRequestStatuses.Rejected;
        request.ReviewedByAdminId = adminId.Value;
        request.ReviewedAt = DateTime.UtcNow;
        request.RejectionReason =
            string.IsNullOrWhiteSpace(dto.Reason)
                ? null
                : dto.Reason.Trim();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Request rejected.",
            request.Id,
            request.Status,
            request.RejectionReason
        });
    }
}

public class CreateBookRequestDto
{
    public int BookId { get; set; }
}

public class RejectBookRequestDto
{
    public string? Reason { get; set; }
}
