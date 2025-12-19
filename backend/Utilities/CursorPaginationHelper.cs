using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;using CnabApi.Models;
namespace CnabApi.Utilities
{
    /// <summary>
    /// Utility class for handling cursor-based pagination.
    /// </summary>
    public static class CursorPaginationHelper
    {
        /// <summary>
        /// Encodes a cursor value to a base64 string.
        /// </summary>
        /// <param name="value">The value to encode (e.g., the ID of the last item).</param>
        /// <returns>A base64-encoded cursor string.</returns>
    public static string? EncodeCursor(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var plainTextBytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(plainTextBytes);
    }

    /// <summary>
    /// Decodes a base64-encoded cursor string back to its original value.
    /// </summary>
    /// <param name="cursor">The base64-encoded cursor string.</param>
    /// <returns>The decoded cursor value.</returns>
    public static string? DecodeCursor(string? cursor)
            {
                var base64EncodedBytes = Convert.FromBase64String(cursor);
                return Encoding.UTF8.GetString(base64EncodedBytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a paginated response with the given items and pagination metadata.
        /// </summary>
        /// <typeparam name="T">The type of items in the response.</typeparam>
        /// <param name="items">The list of items to paginate.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <param name="currentCursor">The current cursor (used to identify starting point).</param>
        /// <param name="cursorSelector">A function to extract the cursor value from an item.</param>
        /// <returns>A paginated response with next/previous cursors.</returns>
        public static PaginatedResponse<T> CreatePaginatedResponse<T>(
            IEnumerable<T> items,
            int pageSize,
            string currentCursor,
            Func<T, string> cursorSelector)
        {
            var itemsList = items.ToList();

            if (!itemsList.Any())
                return new PaginatedResponse<T>
                {
                    Items = itemsList,
                    NextCursor = null,
                    PreviousCursor = null,
                    HasMore = false
                };

            // Take one extra item to determine if there are more items
            var pageItems = itemsList.Take(pageSize + 1).ToList();
            var hasMore = pageItems.Count > pageSize;

            // Return only the requested page size
            var resultItems = pageItems.Take(pageSize).ToList();

            var nextCursor = hasMore
                ? EncodeCursor(cursorSelector(resultItems.Last()))
                : null;

            var previousCursor = !string.IsNullOrEmpty(currentCursor)
                ? currentCursor
                : null;

            return new PaginatedResponse<T>
            {
                Items = resultItems,
                NextCursor = nextCursor,
                PreviousCursor = previousCursor,
                HasMore = hasMore
            };
        }
    }
}
