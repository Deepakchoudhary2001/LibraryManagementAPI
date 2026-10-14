
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LibraryManagement.Api.Data;
using LibraryManagement.Api.DTOs;
using LibraryManagement.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LibraryManagement.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly LibraryDbContext _db;
    private readonly IPasswordHasher<Admin> _adminHasher;
    private readonly IPasswordHasher<LibraryUser> _userHasher;
    private readonly IConfiguration _configuration;

    public AuthController(
        LibraryDbContext db,
        IPasswordHasher<Admin> adminHasher,
        IPasswordHasher<LibraryUser> userHasher,
        IConfiguration configuration)
    {
        _db = db;
        _adminHasher = adminHasher;
        _userHasher = userHasher;
        _configuration = configuration;
    }

    // Existing staff/admin login
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(
        LoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var admin = await _db.Admins
            .SingleOrDefaultAsync(a => a.Email == email);

        if (admin is null ||
            _adminHasher.VerifyHashedPassword(
                admin, admin.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return CreateToken(
            admin.Id, admin.Email, admin.FullName, "Admin");
    }

    // Student registration
    [HttpPost("student/register")]
    public async Task<IActionResult> RegisterStudent(
        StudentRegisterRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _db.LibraryUsers
            .AnyAsync(u => u.Email == email);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }


        var borrower = new Borrower
        {
            FullName = request.FullName.Trim(),
            Email = email,
            MobileNumber = request.MobileNumber.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var user = new LibraryUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            MobileNumber = request.MobileNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Borrower = borrower
        };

        user.PasswordHash = _userHasher.HashPassword(
            user,
            request.Password);

        _db.LibraryUsers.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "Unable to create the account. The email may already be registered."
            });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.MobileNumber,
            user.CreatedAt
        });

    }

    // Student login
    [HttpPost("student/login")]
    public async Task<ActionResult<LoginResponseDto>> StudentLogin(
        LoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.LibraryUsers
            .SingleOrDefaultAsync(u => u.Email == email);

        if (user is null || !user.IsActive ||
            _userHasher.VerifyHashedPassword(
                user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return CreateToken(
            user.Id, user.Email, user.FullName, "Student");
    }

    private ActionResult<LoginResponseDto> CreateToken(
        int id,
        string email,
        string fullName,
        string role)
    {
        var keyValue = _configuration["Jwt:Key"];
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(keyValue) ||
            string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "JWT configuration is missing.");
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(keyValue));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return Ok(new LoginResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            Email = email,
            FullName = fullName
        });
    }
}
