using LibraryManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace LibraryManagement.Api.Data;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options): base(options)
    {
    }

    public DbSet<Admin> Admins => Set<Admin>();

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Borrower> Borrowers => Set<Borrower>();

    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<BookRequest> BookRequests => Set<BookRequest>();

    public DbSet<LibraryUser> LibraryUsers => Set<LibraryUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Admin configuration
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.FullName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(a => a.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.HasIndex(a => a.Email)
                .IsUnique();

            entity.Property(a => a.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();
        });

        // Book configuration
        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(b => b.Author)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(b => b.ISBN)
                .HasMaxLength(20);

            entity.HasIndex(b => b.ISBN)
                .IsUnique()
                .HasFilter("[ISBN] IS NOT NULL");

            entity.Property(b => b.Category)
                .HasMaxLength(100);

            entity.Property(b => b.CoverImageUrl)
                .HasMaxLength(2048);

            entity.Property(b => b.TotalCopies)
                .IsRequired();
        });

        // Borrower configuration
        modelBuilder.Entity<Borrower>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(b => b.MobileNumber)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(b => b.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(b => b.Address)
                .HasMaxLength(500);
        });

        // Loan relationships and constraints
        modelBuilder.Entity<Loan>(entity =>
        {
            entity.HasKey(l => l.Id);

            entity.HasOne(l => l.Book)
                .WithMany(b => b.Loans)
                .HasForeignKey(l => l.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Borrower)
                .WithMany(b => b.Loans)
                .HasForeignKey(l => l.BorrowerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.IssuedByAdmin)
                .WithMany()
                .HasForeignKey(l => l.IssuedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(l => l.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(l => l.BookId);
            entity.HasIndex(l => l.BorrowerId);
            entity.HasIndex(l => l.DueDate);
        });

        modelBuilder.Entity<LibraryUser>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.MobileNumber)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();
        });

        modelBuilder.Entity<LibraryUser>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .HasMaxLength(150).IsRequired();

            entity.Property(u => u.Email)
                .HasMaxLength(256).IsRequired();

            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.MobileNumber)
                .HasMaxLength(20).IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(500).IsRequired();

            entity.HasOne(u => u.Borrower)
                .WithOne()
                .HasForeignKey<LibraryUser>(u => u.BorrowerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(u => u.BorrowerId)
                .IsUnique()
                .HasFilter("[BorrowerId] IS NOT NULL");
        });

        modelBuilder.Entity<BookRequest>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasOne(r => r.Book)
                .WithMany()
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.LibraryUser)
                .WithMany()
                .HasForeignKey(r => r.LibraryUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(r => r.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(r => r.RejectionReason)
                .HasMaxLength(500);

            entity.HasOne<Loan>()
                .WithOne()
                .HasForeignKey<BookRequest>(r => r.LoanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.LibraryUserId, r.BookId })
                .IsUnique()
                .HasFilter("[Status] = 'Pending'");

            entity.HasIndex(r => r.BookId);
            entity.HasIndex(r => r.LibraryUserId);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.LoanId)
                .IsUnique()
                .HasFilter("[LoanId] IS NOT NULL");
        });

    }
}