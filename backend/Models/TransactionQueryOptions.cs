using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Models;

/// <summary>
/// Query options for transaction listings.
/// Note: Currently not used in the API, but kept for potential future use.
/// </summary>
[ExcludeFromCodeCoverage]
public class TransactionQueryOptions
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string>? NatureCodes { get; set; }
    public string SortDirection { get; set; } = "desc"; // asc|desc
}
