using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace CnabApi.Utilities;

/// <summary>
/// Helper class for API versioning utilities.
/// Note: API versioning is configured directly in Program.cs using AddApiVersioning().
/// </summary>
public static class ApiVersioningHelper
{
    // This class is kept for reference and future extensions
    // Current implementation uses attribute-based routing: [Route("api/v1/[controller]")] and [ApiVersion("1.0")]
}
