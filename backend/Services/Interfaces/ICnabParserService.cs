using CnabApi.Common;
using CnabApi.Models;

namespace CnabApi.Services;

/// <summary>
/// Service responsible for parsing CNAB file format and extracting transaction data.
/// </summary>
public interface ICnabParserService
{
    /// <summary>
    /// Parses CNAB file content and extracts transaction data.
    /// </summary>
    /// <param name="fileContent">The raw content of the CNAB file.</param>
    /// <returns>Result containing list of parsed transactions or error message.</returns>
    Result<List<Transaction>> ParseCnabFile(string fileContent);
}
