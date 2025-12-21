namespace CnabApi.Models;

/// <summary>
/// Query options for transaction listings.
/// </summary>
public class TransactionQueryOptions
{
    public string Cpf { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string>? NatureCodes { get; set; }
    public string SortDirection { get; set; } = "desc"; // asc|desc
}
