using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CnabApi.Models;

/// <summary>
/// Represents a parsed CNAB transaction record.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Static lookup dictionaries for transaction codes to improve performance and testability.
    /// </summary>
    private static readonly Dictionary<string, string> NatureDescriptions = new()
    {
        { "1", "Debit" },
        { "2", "Boleto" },
        { "3", "Financing" },
        { "4", "Credit" },
        { "5", "Loan Receipt" },
        { "6", "Sales" },
        { "7", "TED Receipt" },
        { "8", "DOC Receipt" },
        { "9", "Rent" }
    };

    private static readonly Dictionary<string, string> NatureClassifications = new()
    {
        { "1", "Income" },
        { "2", "Expense" },
        { "3", "Expense" },
        { "4", "Income" },
        { "5", "Income" },
        { "6", "Income" },
        { "7", "Income" },
        { "8", "Income" },
        { "9", "Expense" }
    };

    /// <summary>
    /// Unique identifier for the transaction record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Bank code (4 characters).
    /// </summary>
    [MaxLength(4)]
    public string BankCode { get; set; } = string.Empty;

    /// <summary>
    /// Customer CPF (11 characters).
    /// </summary>
    [MaxLength(11)]
    public string Cpf { get; set; } = string.Empty;

    /// <summary>
    /// Nature code that identifies transaction type (12 characters - Card).
    /// Determines if transaction is debit or credit.
    /// </summary>
    [MaxLength(12)]
    public string NatureCode { get; set; } = string.Empty;

    /// <summary>
    /// Transaction amount in the original currency.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Card number or identifier (12 characters).
    /// </summary>
    [MaxLength(12)]
    public string Card { get; set; } = string.Empty;

    /// <summary>
    /// Store owner name (14 characters).
    /// </summary>
    [MaxLength(14)]
    public string StoreOwner { get; set; } = string.Empty;

    /// <summary>
    /// Store name (18 characters).
    /// </summary>
    [MaxLength(18)]
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// Date when the transaction occurred.
    /// </summary>
    [Column(TypeName = "timestamp with time zone")]
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// Time when the transaction occurred.
    /// </summary>
    public TimeSpan TransactionTime { get; set; }

    /// <summary>
    /// Timestamp when the record was created in the system.
    /// </summary>
    [Column(TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Calculated properties for display and business logic

    /// <summary>
    /// Human-readable description of the transaction based on nature code.
    /// </summary>
    public string TransactionDescription => GetTransactionDescription();

    /// <summary>
    /// Signed amount (positive for income, negative for expense).
    /// Used for balance calculations.
    /// </summary>
    public decimal SignedAmount => GetSignedAmount();

    private string GetTransactionDescription() =>
        NatureDescriptions.TryGetValue(NatureCode, out var description) ? description : "Transaction";

    private string GetTransactionNature() =>
        NatureClassifications.TryGetValue(NatureCode, out var nature) ? nature : "Neutral";

    private decimal GetSignedAmount() => GetTransactionNature() switch
    {
        "Income" => Amount,
        "Expense" => -Amount,
        _ => 0
    };
}
