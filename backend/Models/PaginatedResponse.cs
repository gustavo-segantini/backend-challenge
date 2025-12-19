namespace CnabApi.Models
{
    /// <summary>
    /// Represents a paginated response with cursor-based pagination.
    /// </summary>
    public class PaginatedResponse<T>
    {
        /// <summary>
        /// Gets or sets the items in the current page.
        /// </summary>
        public IEnumerable<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Gets or sets the cursor for the next page.
        /// </summary>
        public string? NextCursor { get; set; }

        /// <summary>
        /// Gets or sets the cursor for the previous page.
        /// </summary>
        public string? PreviousCursor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there are more items.
        /// </summary>
        public bool HasMore { get; set; }

        /// <summary>
        /// Gets or sets the total count of items (optional, used only with counts).
        /// </summary>
        public int? TotalCount { get; set; }
    }
}
