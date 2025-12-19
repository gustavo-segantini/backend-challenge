using CnabApi.Common;
using CnabApi.Models;

namespace CnabApi.Services;

/// <summary>
/// Implementation of CNAB parser service.
/// Handles parsing of CNAB format files with 8-field transaction records.
/// </summary>
public class CnabParserService : ICnabParserService
{
    /// <summary>
    /// Parses CNAB file content and extracts transaction data.
    /// Expected format: 8 fixed-width fields per transaction line.
    /// </summary>
    public Result<List<Transaction>> ParseCnabFile(string fileContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileContent))
                return Result<List<Transaction>>.Failure("Conteúdo do arquivo está vazio.");

            var transactions = new List<Transaction>();
            var lines = fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.Length < 80)
                {
                    return Result<List<Transaction>>.Failure(
                        $"Linha inválida: esperado mínimo 80 caracteres, obtido {line.Length}.");
                }

                var transaction = ParseTransaction(line);
                if (transaction != null)
                {
                    transactions.Add(transaction);
                }
            }

            if (transactions.Count == 0)
                return Result<List<Transaction>>.Failure("Nenhuma transação válida foi encontrada no arquivo.");

            return Result<List<Transaction>>.Success(transactions);
        }
        catch (Exception ex)
        {
            return Result<List<Transaction>>.Failure($"Erro ao processar arquivo CNAB: {ex.Message}");
        }
    }

    private Transaction? ParseTransaction(string line)
    {
        try
        {
            // CNAB format fields (fixed positions - 0-based indexing):
            // [0:1]: Type (1 char) - Nature code
            // [1:9]: Date (8 chars - YYYYMMDD)
            // [9:19]: Amount (10 chars, with last 2 as decimals)
            // [19:30]: CPF (11 chars)
            // [30:42]: Card (12 chars)
            // [42:48]: Time (6 chars - HHMMSS)
            // [48:62]: Store Owner (14 chars)
            // [62:80]: Store Name (18 chars) - documentation says 19, but line is only 80 chars

            var type = line.Substring(0, 1).Trim();
            var dateStr = line.Substring(1, 8).Trim();
            var amountStr = line.Substring(9, 10).Trim();
            var cpfCnpj = line.Substring(19, 11).Trim();
            var card = line.Substring(30, 12).Trim();
            var timeStr = line.Substring(42, 6).Trim();
            var storeOwner = line.Substring(48, 14).Trim();
            var storeName = line.Substring(62, 18).Trim();

            // Parse and validate amount
            if (!decimal.TryParse(amountStr, out var amount))
                return null;

            amount = amount / 100; // Convert from cents to decimal

            // Parse and validate date (YYYYMMDD format)
            if (!ParseDate(dateStr, out var date))
                return null;

            // Parse and validate time (HHMMSS format)
            if (!ParseTime(timeStr, out var time))
                return null;

            return new Transaction
            {
                BankCode = type,
                Cpf = cpfCnpj,
                NatureCode = type,
                Amount = amount,
                Card = card,
                StoreOwner = storeOwner,
                StoreName = storeName,
                TransactionDate = date,
                TransactionTime = time,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool ParseDate(string dateStr, out DateTime date)
    {
        date = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(dateStr) || dateStr.Length != 8)
            return false;

        if (!int.TryParse(dateStr.AsSpan(0, 4), out var year) ||
            !int.TryParse(dateStr.AsSpan(4, 2), out var month) ||
            !int.TryParse(dateStr.AsSpan(6, 2), out var day))
            return false;

        try
        {
            date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ParseTime(string timeStr, out TimeSpan time)
    {
        time = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(timeStr) || timeStr.Length != 6)
            return false;

        if (!int.TryParse(timeStr.AsSpan(0, 2), out var hours) ||
            !int.TryParse(timeStr.AsSpan(2, 2), out var minutes) ||
            !int.TryParse(timeStr.AsSpan(4, 2), out var seconds))
            return false;

        try
        {
            time = new TimeSpan(0, hours, minutes, seconds);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
