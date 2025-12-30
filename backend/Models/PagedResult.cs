using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Models;

/// <summary>
/// Generic paginated result wrapper with metadata.
/// </summary>
[ExcludeFromCodeCoverage]
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// TransactionQueryOptions moved to TransactionQueryOptions.cs
