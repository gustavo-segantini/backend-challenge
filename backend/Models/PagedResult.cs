namespace CnabApi.Models;

/// <summary>
/// Generic paginated result wrapper with metadata.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// TransactionQueryOptions moved to TransactionQueryOptions.cs
