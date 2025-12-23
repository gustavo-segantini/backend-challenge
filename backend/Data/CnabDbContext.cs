using Microsoft.EntityFrameworkCore;
using CnabApi.Models;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Data;

/// <summary>
/// Entity Framework Core database context for CNAB application.
/// </summary>
[ExcludeFromCodeCoverage]
public class CnabDbContext(DbContextOptions<CnabDbContext> options) : DbContext(options)
{

    /// <summary>
    /// DbSet for Transaction entities.
    /// </summary>
    public DbSet<Transaction> Transactions { get; set; }

    /// <summary>
    /// DbSet for application users.
    /// </summary>
    public DbSet<User> Users { get; set; }

    /// <summary>
    /// DbSet for refresh tokens.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    /// <summary>
    /// DbSet for file upload tracking (for duplicate detection).
    /// </summary>
    public DbSet<FileUpload> FileUploads { get; set; }

    /// <summary>
    /// DbSet for file upload line hashes (for tracking duplicate lines).
    /// </summary>
    public DbSet<FileUploadLineHash> FileUploadLineHashes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Transaction configuration
        modelBuilder.Entity<Transaction>()
            .HasKey(t => t.Id);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.BankCode)
            .IsRequired()
            .HasMaxLength(4);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Cpf)
            .IsRequired()
            .HasMaxLength(11);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.NatureCode)
            .IsRequired()
            .HasMaxLength(12);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Amount)
            .HasColumnType("numeric(15,2)");

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Card)
            .IsRequired()
            .HasMaxLength(12);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.TransactionDate)
            .IsRequired();

        modelBuilder.Entity<Transaction>()
            .Property(t => t.TransactionTime)
            .IsRequired();

        // Create composite index for faster lookups
        modelBuilder.Entity<Transaction>()
            .HasIndex(t => new { t.TransactionDate, t.Cpf });

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.Cpf);

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.NatureCode);

        // Users configuration
        modelBuilder.Entity<User>()
            .HasKey(u => u.Id);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(30);

        modelBuilder.Entity<User>()
            .Property(u => u.PasswordHash)
            .IsRequired();

        // RefreshTokens configuration
        modelBuilder.Entity<RefreshToken>()
            .HasKey(rt => rt.Id);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .Property(rt => rt.Token)
            .IsRequired();

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FileUploads configuration
        modelBuilder.Entity<FileUpload>()
            .HasKey(fu => fu.Id);

        modelBuilder.Entity<FileUpload>()
            .HasIndex(fu => fu.FileHash)
            .IsUnique();

        modelBuilder.Entity<FileUpload>()
            .Property(fu => fu.FileHash)
            .IsRequired()
            .HasMaxLength(64); // SHA256 in hex

        modelBuilder.Entity<FileUpload>()
            .Property(fu => fu.FileName)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<FileUpload>()
            .Property(fu => fu.Status)
            .HasConversion<int>();

        // FileUploadLineHashes configuration
        modelBuilder.Entity<FileUploadLineHash>()
            .HasKey(lh => lh.Id);

        modelBuilder.Entity<FileUploadLineHash>()
            .HasIndex(lh => lh.LineHash)
            .IsUnique();

        modelBuilder.Entity<FileUploadLineHash>()
            .Property(lh => lh.LineHash)
            .IsRequired()
            .HasMaxLength(64); // SHA256 in hex

        modelBuilder.Entity<FileUploadLineHash>()
            .Property(lh => lh.LineContent)
            .IsRequired();

        // Configure relationship between FileUpload and FileUploadLineHash
        modelBuilder.Entity<FileUploadLineHash>()
            .HasOne(lh => lh.FileUpload)
            .WithMany(fu => fu.LineHashes)
            .HasForeignKey(lh => lh.FileUploadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
