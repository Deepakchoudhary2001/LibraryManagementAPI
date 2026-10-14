using LibraryManagement.Api.Controllers;
using Microsoft.Extensions.Caching.Memory;
using LibraryManagement.Api.Services;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace LibraryManagement.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly LibraryDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly BookCacheVersionService _cacheVersion;

    public BooksController(
        LibraryDbContext context,
        IMemoryCache cache,
        BookCacheVersionService cacheVersion)
    {
        _context = context;
        _cache = cache;
        _cacheVersion = cacheVersion;
    }

    // GET: /api/books
    // Optional filters: ?search=clean&category=Technology
    // Pagination: ?page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetBooks(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(
                "Page must be at least 1 and pageSize must be between 1 and 100.");
        }

        var cacheKey =
            $"books:list:v{_cacheVersion.CurrentVersion}:" +
            $"search={Uri.EscapeDataString(search?.Trim() ?? string.Empty)}:" +
            $"category={Uri.EscapeDataString(category?.Trim() ?? string.Empty)}:" +
            $"page={page}:size={pageSize}";

        if (_cache.TryGetValue(cacheKey, out object? cachedResponse)
            && cachedResponse is not null)
        {
            return Ok(cachedResponse);
        }

        var query = _context.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(b =>
                b.Title.Contains(term) ||
                b.Author.Contains(term) ||
                (b.ISBN != null && b.ISBN.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var selectedCategory = category.Trim();

            query = query.Where(b => b.Category == selectedCategory);
        }

        var totalCount = await query.CountAsync();

        var books = await query
            .OrderBy(b => b.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BookResponseDto
            {
                Id = b.Id,
                Title = b.Title,
                Author = b.Author,
                ISBN = b.ISBN,
                Description = b.Description,
                Category = b.Category,
                CoverImageUrl = b.CoverImageUrl,
                PublishedYear = b.PublishedYear,
                TotalCopies = b.TotalCopies,
                AvailableCopies = b.TotalCopies -
                    b.Loans.Count(l => l.ReturnDate == null),
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        var response = new
        {
            page,
            pageSize,
            totalCount,
            items = books
        };

        _cache.Set(
            cacheKey,
            response,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

        return Ok(response);
    }

    // GET: /api/books/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookResponseDto>> GetBookById(int id)
    {
        var cacheKey = $"books:detail:{id}";

        if (_cache.TryGetValue(
                cacheKey,
                out BookResponseDto? cachedBook)
            && cachedBook is not null)
        {
            return Ok(cachedBook);
        }

        var book = await _context.Books
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BookResponseDto
            {
                Id = b.Id,
                Title = b.Title,
                Author = b.Author,
                ISBN = b.ISBN,
                Description = b.Description,
                Category = b.Category,
                CoverImageUrl = b.CoverImageUrl,
                PublishedYear = b.PublishedYear,
                TotalCopies = b.TotalCopies,
                AvailableCopies = b.TotalCopies -
                    b.Loans.Count(l => l.ReturnDate == null),
                CreatedAt = b.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (book == null)
        {
            return NotFound(new { message = $"Book with ID {id} was not found." });
        }

        _cache.Set(
            cacheKey,
            book,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

        return Ok(book);
    }

    // POST: /api/books
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<BookResponseDto>> CreateBook(
        CreateBookDto dto)
    {
        var book = new Book
        {
            Title = dto.Title.Trim(),
            Author = dto.Author.Trim(),
            ISBN = NormalizeIsbn(dto.ISBN),
            Description = dto.Description?.Trim(),
            Category = dto.Category?.Trim(),
            CoverImageUrl = dto.CoverImageUrl?.Trim(),
            PublishedYear = dto.PublishedYear,
            TotalCopies = dto.TotalCopies,
            CreatedAt = DateTime.UtcNow
        };

        _context.Books.Add(book);

        try
        {
            await _context.SaveChangesAsync();
            _cacheVersion.InvalidateLists();

        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "The book could not be saved. Check whether its ISBN already exists."
            });
        }

        var response = new BookResponseDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            ISBN = book.ISBN,
            Description = book.Description,
            Category = book.Category,
            CoverImageUrl = book.CoverImageUrl,
            PublishedYear = book.PublishedYear,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.TotalCopies,
            CreatedAt = book.CreatedAt
        };

        return CreatedAtAction(
            nameof(GetBookById),
            new { id = book.Id },
            response);
    }

    // PUT: /api/books/1
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<BookResponseDto>> UpdateBook(
        int id,
        UpdateBookDto dto)
    {
        var book = await _context.Books
            .Include(b => b.Loans)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (book == null)
        {
            return NotFound(new { message = $"Book with ID {id} was not found." });
        }

        var activeLoans = book.Loans.Count(l => l.ReturnDate == null);

        if (dto.TotalCopies < activeLoans)
        {
            return BadRequest(new
            {
                message = $"Total copies cannot be less than the {activeLoans} copies currently on loan."
            });
        }

        book.Title = dto.Title.Trim();
        book.Author = dto.Author.Trim();
        book.ISBN = NormalizeIsbn(dto.ISBN);
        book.Description = dto.Description?.Trim();
        book.Category = dto.Category?.Trim();
        book.CoverImageUrl = dto.CoverImageUrl?.Trim();
        book.PublishedYear = dto.PublishedYear;
        book.TotalCopies = dto.TotalCopies;

        try
        {
            await _context.SaveChangesAsync();
            _cacheVersion.InvalidateLists();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "The book could not be updated. Check whether its ISBN already exists."
            });
        }

        return Ok(new BookResponseDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            ISBN = book.ISBN,
            Description = book.Description,
            Category = book.Category,
            CoverImageUrl = book.CoverImageUrl,
            PublishedYear = book.PublishedYear,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.TotalCopies - activeLoans,
            CreatedAt = book.CreatedAt
        });
    }

    // DELETE: /api/books/1
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        var book = await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id);

        if (book == null)
        {
            return NotFound(new { message = $"Book with ID {id} was not found." });
        }

        var hasLoans = await _context.Loans
            .AnyAsync(l => l.BookId == id);

        if (hasLoans)
        {
            return Conflict(new
            {
                message = "This book has loan history and cannot be deleted."
            });
        }

        _context.Books.Remove(book);
        await _context.SaveChangesAsync();
        _cacheVersion.InvalidateLists();

        return NoContent();
    }

    private static string? NormalizeIsbn(string? isbn)
    {
        return string.IsNullOrWhiteSpace(isbn)
            ? null
            : isbn.Trim();
    }
}