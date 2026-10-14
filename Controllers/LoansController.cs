
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
[Route("api/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly LibraryDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly BookCacheVersionService _cacheVersion;
    private readonly FineCalculationService _fineCalculator;

    public LoansController(
        LibraryDbContext db,
        IMemoryCache cache,
        BookCacheVersionService cacheVersion,
        FineCalculationService fineCalculator)
    {
        _db = db;
        _cache = cache;
        _cacheVersion = cacheVersion;
        _fineCalculator = fineCalculator;
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

    private async Task<int?> GetCurrentBorrowerId()
    {
        var userId = CurrentUserId;

        if (userId is null)
            return null;

        return await _db.LibraryUsers
            .Where(u => u.Id == userId.Value && u.IsActive)
            .Select(u => u.BorrowerId)
            .SingleOrDefaultAsync();
    }

    private void InvalidateBookCache(int bookId)
    {
        _cacheVersion.InvalidateLists();
        _cache.Remove($"books:detail:{bookId}");
    }

    // GET /api/loans/my
    // Student loan history, including a current fine estimate.
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyLoans()
    {
        var borrowerId = await GetCurrentBorrowerId();

        if (borrowerId is null)
            return Ok(Array.Empty<object>());

        var today = DateTime.UtcNow;

        var loans = await _db.Loans
            .AsNoTracking()
            .Where(l => l.BorrowerId == borrowerId.Value)
            .OrderByDescending(l => l.IssueDate)
            .Select(l => new
            {
                l.Id,
                l.BookId,
                BookTitle = l.Book.Title,
                l.Book.Author,
                l.Book.CoverImageUrl,
                l.IssueDate,
                l.DueDate,
                l.ReturnDate,
                l.ReturnRequestedAt,
                l.ReturnRequestStatus,
                l.OverdueDaysAtRequest,
                l.FineAmount,
                IsReturned = l.ReturnDate != null,
                IsOverdue = l.ReturnDate == null && l.DueDate < today
            })
            .ToListAsync();

        var result = loans.Select(l =>
        {
            var estimate = l.ReturnDate == null
                ? _fineCalculator.Calculate(l.DueDate, today)
                : (OverdueDays: l.OverdueDaysAtRequest ?? 0,
                   FineAmount: l.FineAmount ?? 0m);

            return new
            {
                l.Id,
                l.BookId,
                l.BookTitle,
                l.Author,
                l.CoverImageUrl,
                l.IssueDate,
                l.DueDate,
                l.ReturnDate,
                l.ReturnRequestedAt,
                l.ReturnRequestStatus,
                l.OverdueDaysAtRequest,
                l.FineAmount,
                l.IsReturned,
                l.IsOverdue,
                EstimatedOverdueDays = l.ReturnDate == null
                    ? estimate.OverdueDays
                    : l.OverdueDaysAtRequest ?? 0,
                EstimatedFineAmount = l.ReturnDate == null
                    ? estimate.FineAmount
                    : l.FineAmount ?? 0m
            };
        });

        return Ok(result);
    }

    // GET /api/loans
    // Admin loan history.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllLoans()
    {
        var today = DateTime.UtcNow;

        var loans = await _db.Loans
            .AsNoTracking()
            .OrderByDescending(l => l.IssueDate)
            .Select(l => new
            {
                l.Id,
                l.BookId,
                BookTitle = l.Book.Title,
                l.Book.Author,
                l.BorrowerId,
                StudentName = l.Borrower.FullName,
                StudentEmail = l.Borrower.Email,
                StudentMobile = l.Borrower.MobileNumber,
                l.IssueDate,
                l.DueDate,
                l.ReturnDate,
                l.IssuedByAdminId,
                IssuedByAdminName = l.IssuedByAdmin.FullName,
                l.ReturnRequestedAt,
                l.ReturnRequestStatus,
                l.OverdueDaysAtRequest,
                l.FineAmount,
                IsReturned = l.ReturnDate != null,
                IsOverdue = l.ReturnDate == null && l.DueDate < today
            })
            .ToListAsync();

        return Ok(loans);
    }

    // POST /api/loans/{id}/request-return
    // Student requests to return their own active loan.
    [HttpPost("{id:int}/request-return")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> RequestReturn(int id)
    {
        var borrowerId = await GetCurrentBorrowerId();

        if (borrowerId is null)
            return Unauthorized(new { message = "Active student account not found." });

        var loan = await _db.Loans
            .SingleOrDefaultAsync(l =>
                l.Id == id &&
                l.BorrowerId == borrowerId.Value);

        if (loan is null)
            return NotFound(new { message = "Loan not found for this student." });

        if (loan.ReturnDate is not null)
            return Conflict(new { message = "This book has already been returned." });

        if (string.Equals(
            loan.ReturnRequestStatus,
            "Pending",
            StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "A return request is already pending." });
        }

        var requestTime = DateTime.UtcNow;
        var fine = _fineCalculator.Calculate(loan.DueDate, requestTime);

        loan.ReturnRequestedAt = requestTime;
        loan.ReturnRequestStatus = "Pending";
        loan.OverdueDaysAtRequest = fine.OverdueDays;
        loan.FineAmount = fine.FineAmount;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Return request submitted successfully.",
            loan.Id,
            loan.BookId,
            loan.ReturnRequestedAt,
            loan.ReturnRequestStatus,
            loan.OverdueDaysAtRequest,
            loan.FineAmount
        });
    }

    // GET /api/loans/return-requests
    // Admin sees requests awaiting physical book receipt.
    [HttpGet("return-requests")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPendingReturnRequests()
    {
        var requests = await _db.Loans
            .AsNoTracking()
            .Where(l =>
                l.ReturnDate == null &&
                l.ReturnRequestStatus == "Pending")
            .OrderBy(l => l.ReturnRequestedAt)
            .Select(l => new
            {
                l.Id,
                l.BookId,
                BookTitle = l.Book.Title,
                BookAuthor = l.Book.Author,
                l.Book.CoverImageUrl,
                l.BorrowerId,
                StudentName = l.Borrower.FullName,
                StudentEmail = l.Borrower.Email,
                StudentMobile = l.Borrower.MobileNumber,
                l.IssueDate,
                l.DueDate,
                l.ReturnRequestedAt,
                l.OverdueDaysAtRequest,
                l.FineAmount,
                l.ReturnRequestStatus
            })
            .ToListAsync();

        return Ok(requests);
    }

    // POST /api/loans/{id}/confirm-return
    // Admin confirms that the physical book has been received.
    [HttpPost("{id:int}/confirm-return")]
    [Authorize(Roles = "Admin")]
public async Task<IActionResult> ConfirmReturn(int id)
    {
        var loan = await _db.Loans
            .SingleOrDefaultAsync(l => l.Id == id);

        if (loan is null)
            return NotFound(new { message = "Loan not found." });

        if (loan.ReturnDate is not null)
        {
            return Conflict(new
            {
                message = "This loan has already been returned.",
                loan.ReturnDate
            });
        }

        if (!string.Equals(
            loan.ReturnRequestStatus,
            "Pending",
            StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                message = "There is no pending student return request for this loan."
            });
        }

        // Mark the loan as returned
        loan.ReturnDate = DateTime.UtcNow;
        loan.ReturnRequestStatus = "Completed";

        await _db.SaveChangesAsync();

        // Refresh cached book inventory data
        InvalidateBookCache(loan.BookId);

        // Calculate availability from active loans
        var activeLoans = await _db.Loans
            .CountAsync(l =>
                l.BookId == loan.BookId &&
                l.ReturnDate == null);

        var book = await _db.Books
            .SingleOrDefaultAsync(b => b.Id == loan.BookId);

        if (book is null)
            return NotFound(new { message = "Book not found." });

        var availableCopies = Math.Max(
            0,
            book.TotalCopies - activeLoans
        );

        return Ok(new
        {
            message = "Book receipt confirmed and return completed.",
            loan.Id,
            loan.BookId,
            loan.BorrowerId,
            loan.ReturnDate,
            loan.OverdueDaysAtRequest,
            loan.FineAmount,
            loan.ReturnRequestStatus,
            AvailableCopies = availableCopies,
            book.TotalCopies
        });
    }

    // POST /api/loans/{id}/return
    // Retained for compatibility with the existing admin return feature.
    [HttpPost("{id:int}/return")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReturnBook(int id)
    {
        var loan = await _db.Loans
            .SingleOrDefaultAsync(l => l.Id == id);

        if (loan is null)
            return NotFound(new { message = "Loan not found." });

        if (loan.ReturnDate is not null)
        {
            return Conflict(new
            {
                message = "This loan has already been returned.",
                loan.ReturnDate
            });
        }

        // If a student submitted a return request, preserve its
        // recorded fine. Otherwise calculate it at admin receipt time.
        if (loan.ReturnRequestedAt is null)
        {
            var fine = _fineCalculator.Calculate(
                loan.DueDate,
                DateTime.UtcNow);

            loan.ReturnRequestedAt = DateTime.UtcNow;
            loan.OverdueDaysAtRequest = fine.OverdueDays;
            loan.FineAmount = fine.FineAmount;
        }

        loan.ReturnDate = DateTime.UtcNow;
        loan.ReturnRequestStatus = "Completed";

        await _db.SaveChangesAsync();

        InvalidateBookCache(loan.BookId);

        return Ok(new
        {
            message = "Book returned successfully.",
            loan.Id,
            loan.BookId,
            loan.BorrowerId,
            loan.ReturnDate,
            loan.OverdueDaysAtRequest,
            loan.FineAmount,
            loan.ReturnRequestStatus
        });
    }
}
