namespace CnabApi.Models;

/// <summary>
/// Represents transactions grouped by store with calculated balance.
/// </summary>
public class StoreGroupedTransactions
{
    /// <summary>
    /// Store name.
    /// </summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// Store owner name.
    /// </summary>
    public string StoreOwner { get; set; } = string.Empty;

    /// <summary>
    /// List of transactions for this store.
    /// </summary>
    public List<Transaction> Transactions { get; set; } = [];

    /// <summary>
    /// Calculated balance for this store (sum of all signed amounts).
    /// </summary>
    public decimal Balance { get; set; }
}

