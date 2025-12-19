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
    }
}
