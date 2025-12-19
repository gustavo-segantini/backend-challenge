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
    }
}
