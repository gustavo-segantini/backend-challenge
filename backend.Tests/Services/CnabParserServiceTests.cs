using CnabApi.Common;
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

    [Fact]
    public void ParseCnabFile_ShouldParseTypeCorrectly()
    {
        // Arrange - Type "1" (Debit/Income)
        var line = "1201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].NatureCode.Should().Be("1");
        result.Data[0].BankCode.Should().Be("1");
    }

    [Fact]
    public void ParseCnabFile_ShouldParseDateCorrectly()
    {
        // Arrange - Date: 2019-03-01
        var line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].TransactionDate.Should().Be(new DateTime(2019, 3, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ParseCnabFile_ShouldParseAmountCorrectly()
    {
        // Arrange - Amount: 0000014200 = 142.00
        var line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].Amount.Should().Be(142.00m);
    }

    [Fact]
    public void ParseCnabFile_ShouldParseTimeCorrectly()
    {
        // Arrange - Time: 153453 = 15:34:53
        var line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].TransactionTime.Should().Be(new TimeSpan(15, 34, 53));
    }

    [Fact]
    public void ParseCnabFile_ShouldParseCpfCorrectly()
    {
        // Arrange - CPF: 09620676017
        var line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].Cpf.Should().Be("09620676017");
    }

    [Fact]
    public void ParseCnabFile_ShouldParseCardCorrectly()
    {
        // Arrange - Card: 4753****3153
        var line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data![0].Card.Should().Be("4753****3153");
    }

    #endregion

    #region ParseCnabFile - Failure Cases

    [Fact]
    public void ParseCnabFile_WithEmptyContent_ShouldReturnFailure()
    {
        // Arrange
        var emptyContent = "";

        // Act
        var result = _parserService.ParseCnabFile(emptyContent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("vazio");
    }

    [Fact]
    public void ParseCnabFile_WithNullContent_ShouldReturnFailure()
    {
        // Arrange
        string? nullContent = null;

        // Act
        var result = _parserService.ParseCnabFile(nullContent!);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ParseCnabFile_WithWhitespaceOnly_ShouldReturnFailure()
    {
        // Arrange
        var whitespaceContent = "   \n   \n   ";

        // Act
        var result = _parserService.ParseCnabFile(whitespaceContent);

        // Assert
        result.IsSuccess.Should().BeFalse();
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
        result.ErrorMessage.Should().Contain("Linha inválida");
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
        result.ErrorMessage.Should().Contain("Linha inválida");
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
        result.ErrorMessage.Should().Contain("vazio");
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

    [Fact]
    public void ParseCnabFile_WithInvalidDateMonth_ShouldSkipTransaction()
    {
        // Arrange - Invalid month: 13 (invalid)
        var line = "3201913010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    [Fact]
    public void ParseCnabFile_WithInvalidDateDay_ShouldSkipTransaction()
    {
        // Arrange - Invalid day: 32 (invalid)
        var line = "3201903320000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    [Fact]
    public void ParseCnabFile_WithNonNumericDate_ShouldSkipTransaction()
    {
        // Arrange - Non-numeric date: ABCDEFGH
        var line = "3ABCDEFGH0000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    [Fact]
    public void ParseCnabFile_WithNonNumericTime_ShouldSkipTransaction()
    {
        // Arrange - Non-numeric time: AABBCC
        var line = "3201903010000014200096206760174753****3153AABBCCJOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    [Fact]
    public void ParseCnabFile_WithEmptyDateField_ShouldSkipTransaction()
    {
        // Arrange - Empty/space date field
        var line = "3        0000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    [Fact]
    public void ParseCnabFile_WithEmptyTimeField_ShouldSkipTransaction()
    {
        // Arrange - Empty/space time field
        var line = "3201903010000014200096206760174753****3153      JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _parserService.ParseCnabFile(line);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No valid transactions found in the file");
    }

    #endregion
}
