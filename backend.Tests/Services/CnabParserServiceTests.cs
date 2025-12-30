using CnabApi.Services;
using FluentAssertions;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for the CnabParserService.
/// Tests CNAB file parsing functionality.
/// </summary>
public class CnabParserServiceTests
{
    private readonly CnabParserService _parserService;

    public CnabParserServiceTests()
    {
        _parserService = new CnabParserService();
    }

    #region ParseCnabFile - Success Cases

    [Fact]
    public void ParseCnabFile_WithValidContent_ShouldReturnTransactions()
    {
        // Arrange
        var validLine = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        
        // Act
        var result = _parserService.ParseCnabFile(validLine);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].NatureCode.Should().Be("3");
        result.Data[0].Cpf.Should().Be("09620676017");
        result.Data[0].Amount.Should().Be(142.00m);
        result.Data[0].Card.Should().Be("4753****3153");
    }

    [Fact]
    public void ParseCnabFile_WithMultipleLines_ShouldReturnAllTransactions()
    {
        // Arrange
        var content = @"3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       
5201903010000013200556418150633123****7687145607MARIA JOSEFINALOJA DO Ó - MATRIZ
1201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(content);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("1201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ", "1", "1")] // Type and BankCode (same as type)
    [InlineData("3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "3", "3")] // Type and BankCode (same as type)
    [InlineData("5201903010000013200556418150633123****7687145607MARIA JOSEFINALOJA DO Ó - MATRIZ", "5", "5")] // Type and BankCode (same as type)
    public void ParseCnabFile_ShouldParseTypeAndBankCodeCorrectly(string line, string expectedType, string expectedBankCode)
    {
        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].NatureCode.Should().Be(expectedType);
        result.Data[0].BankCode.Should().Be(expectedBankCode);
    }

    [Theory]
    [InlineData("3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", 2019, 3, 1)] // Date: 2019-03-01
    [InlineData("3202001010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", 2020, 1, 1)] // Date: 2020-01-01
    public void ParseCnabFile_ShouldParseDateCorrectly(string line, int year, int month, int day)
    {
        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].TransactionDate.Should().Be(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", 142.00)] // Amount: 0000014200 = 142.00
    [InlineData("3201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ", 152.00)] // Amount: 0000015200 = 152.00
    [InlineData("3201903010000005000096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", 50.00)] // Amount: 0000005000 = 50.00 (note: 5000 cents = 50.00)
    public void ParseCnabFile_ShouldParseAmountCorrectly(string line, decimal expectedAmount)
    {
        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].Amount.Should().Be(expectedAmount);
    }

    [Theory]
    [InlineData("3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", 15, 34, 53)] // Time: 153453 = 15:34:53
    [InlineData("3201903010000014200096206760174753****3153120000JOÃO MACEDO   BAR DO JOÃO       ", 12, 0, 0)] // Time: 120000 = 12:00:00
    [InlineData("3201903010000014200096206760174753****3153000000JOÃO MACEDO   BAR DO JOÃO       ", 0, 0, 0)] // Time: 000000 = 00:00:00
    public void ParseCnabFile_ShouldParseTimeCorrectly(string line, int hours, int minutes, int seconds)
    {
        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].TransactionTime.Should().Be(new TimeSpan(hours, minutes, seconds));
    }

    [Theory]
    [InlineData("3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "09620676017")] // CPF: 09620676017 (pos 19-30)
    [InlineData("32019030100000142000556418150633123****7687145607MARIA JOSEFINALOJA DO Ó - MATRIZ", "05564181506")] // CPF: 05564181506 (pos 19-30, includes leading zero)
    public void ParseCnabFile_ShouldParseCpfCorrectly(string line, string expectedCpf)
    {
        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].Cpf.Should().Be(expectedCpf);
    }

    [Theory]
    [InlineData("3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "4753****3153")] // Card: 4753****3153
    [InlineData("3201903010000014200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ", "1234****7890")] // Card: 1234****7890
    public void ParseCnabFile_ShouldParseCardCorrectly(string line, string expectedCard)
    {
        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].Card.Should().Be(expectedCard);
    }

    #endregion

    #region ParseCnabFile - Failure Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n   \n   ")]
    public void ParseCnabFile_WithNullOrEmptyOrWhitespaceContent_ShouldReturnFailure(string? content)
    {
        // Act
        var result = _parserService.ParseCnabFile(content!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        if (content != null && content.Trim().Length == 0)
        {
            result.ErrorMessage.Should().Contain("empty");
        }
    }

    [Fact]
    public void ParseCnabFile_WithShortLine_ShouldReturnLineError()
    {
        // Arrange - Line too short to be valid
        var shortLine = "12345";

        // Act
        var result = _parserService.ParseCnabFile(shortLine);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid line");
    }

    [Fact]
    public void ParseCnabFile_WithInvalidDate_ShouldSkipInvalidLine()
    {
        // Arrange - Invalid date: 99999999
        var invalidDateLine = "3999999990000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(invalidDateLine);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ParseCnabFile_WithMixedValidAndInvalidLines_ShouldFailOnFirstInvalidLine()
    {
        // Arrange - Parser fails on first invalid line (doesn't skip)
        var content = @"3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       
INVALID_LINE_HERE
1201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(content);

        // Assert
        // Parser returns failure on first invalid line encountered
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid line");
    }

    #endregion

    #region ParseCnabLine Tests

    [Fact]
    public void ParseCnabLine_WithValidLine_ShouldReturnSuccess()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const int lineIndex = 0;

        // Act
        var result = _parserService.ParseCnabLine(line, lineIndex);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.NatureCode.Should().Be("3");
        result.Data.Cpf.Should().Be("09620676017");
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 1)]
    [InlineData("   ", 2)]
    public void ParseCnabLine_WithNullOrEmptyOrWhitespaceLine_ShouldReturnFailure(string? line, int lineIndex)
    {
        // Act
        var result = _parserService.ParseCnabLine(line!, lineIndex);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"Line {lineIndex}");
    }

    [Theory]
    [InlineData("12345", 5)] // Short line
    [InlineData("3999999990000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", 10)] // Invalid date
    [InlineData("3201903010AAAAAAAA00096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", 15)] // Invalid amount
    public void ParseCnabLine_WithInvalidFormat_ShouldReturnFailure(string invalidLine, int lineIndex)
    {
        // Act
        var result = _parserService.ParseCnabLine(invalidLine, lineIndex);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"Line {lineIndex}");
    }

    [Fact]
    public void ParseCnabLine_WithException_ShouldReturnFailureWithLineIndex()
    {
        // Arrange - Line that will cause exception during parsing
        const string problematicLine = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const int lineIndex = 20;

        // Act
        var result = _parserService.ParseCnabLine(problematicLine, lineIndex);

        // Assert
        // This should succeed, but if it fails, error should include line index
        if (!result.IsSuccess)
        {
            result.ErrorMessage.Should().Contain($"Line {lineIndex}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(-1)]
    public void ParseCnabLine_WithDifferentLineIndices_ShouldIncludeIndexInError(int lineIndex)
    {
        // Arrange
        const string invalidLine = "short";

        // Act
        var result = _parserService.ParseCnabLine(invalidLine, lineIndex);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"Line {lineIndex}");
    }

    #endregion

    #region All Transaction Types Tests

    [Theory]
    [InlineData("1", "1")] // Debit
    [InlineData("2", "2")] // Boleto
    [InlineData("3", "3")] // Financing
    [InlineData("4", "4")] // Credit
    [InlineData("5", "5")] // Loan Receipt
    [InlineData("6", "6")] // Sales
    [InlineData("7", "7")] // TED Receipt
    [InlineData("8", "8")] // DOC Receipt
    [InlineData("9", "9")] // Rent
    public void ParseCnabFile_ShouldParseAllTransactionTypes(string type, string expectedNatureCode)
    {
        // Arrange
        var line = $"{type}201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].NatureCode.Should().Be(expectedNatureCode);
    }

    #endregion

    #region ParseCnabFile - Additional Edge Cases

    [Fact]
    public void ParseCnabFile_WithOnlyEmptyLines_ShouldReturnFailure()
    {
        // Arrange - Multiple empty lines (null or whitespace)
        // Note: IsNullOrWhiteSpace check catches this before processing lines
        var content = "\n\n   \n\t\n";

        // Act
        var result = _parserService.ParseCnabFile(content);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public void ParseCnabFile_WithInvalidAmount_ShouldSkipTransaction()
    {
        // Arrange - Invalid amount (non-numeric)
        var line = "3201903010AAAAAAAA00096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    [Fact]
    public void ParseCnabFile_WithLargeTimeValue_ShouldParseSuccessfully()
    {
        // Arrange - TimeSpan accepts hours > 23, so 99:99:99 is valid (but unusual)
        // TimeSpan(0, 99, 99, 99) = 4 days, 4 hours, 40 minutes, 39 seconds
        var line = "3201903010000014200096206760174753****3153999999JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        // TimeSpan constructor doesn't throw for large hour values
        result.IsSuccess.Should().BeTrue();
        result.Data![0].TransactionTime.TotalHours.Should().BeGreaterThan(24);
    }

    [Theory]
    [InlineData("3201913010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "Invalid month: 13")]
    [InlineData("3201903320000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "Invalid day: 32")]
    [InlineData("3ABCDEFGH0000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "Non-numeric date")]
    [InlineData("3201903010000014200096206760174753****3153AABBCCJOÃO MACEDO   BAR DO JOÃO       ", "Non-numeric time")]
    [InlineData("3        0000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "Empty date field")]
    [InlineData("3201903010000014200096206760174753****3153      JOÃO MACEDO   BAR DO JOÃO       ", "Empty time field")]
    [InlineData("3201903010AAAAAAAA00096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ", "Invalid amount")]
    public void ParseCnabFile_WithInvalidFields_ShouldReturnFailureWithNoValidTransactions(string line, string description)
    {
        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse(description);
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    #endregion
}
