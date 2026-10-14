using LibraryManagement.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Api.Data
{
    public static class AdminSeeder
    {
        public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
        {
            var email = configuration["AdminBootstrap:Email"];
            var password = configuration["AdminBootstrap:Password"];
            var fullName = configuration["AdminBootstrap:FullName"];

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fullName))
            {
                throw new InvalidOperationException(
                    "AdminBootstrap secrets must be configured.");
            }

            using var scope = services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<LibraryDbContext>();

            var hasher = scope.ServiceProvider
                .GetRequiredService<IPasswordHasher<Admin>>();

            var normalizedEmail = email.Trim().ToLowerInvariant();

            var existingAdmin = await db.Admins.FirstOrDefaultAsync(
                a => a.Email.ToLower() == normalizedEmail);

            if (existingAdmin is not null)
            {
                return;
            }

            var admin = new Admin
            {
                FullName = fullName.Trim(),
                Email = normalizedEmail,
                CreatedAt = DateTime.UtcNow
            };

            admin.PasswordHash = hasher.HashPassword(admin, password);

            db.Admins.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}
