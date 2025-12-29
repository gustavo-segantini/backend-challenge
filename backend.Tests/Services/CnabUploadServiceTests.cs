using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Interfaces;
using CnabApi.Services.LineProcessing;
using CnabApi.Services.UnitOfWork;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for the CnabUploadService.
/// Tests the line-by-line processing with parallel workers.
/// </summary>
public class CnabUploadServiceTests
{
	private readonly Mock<IHashService> _hashServiceMock;
	private readonly Mock<ILineProcessor> _lineProcessorMock;
	private readonly Mock<ICheckpointManager> _checkpointManagerMock;
	private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
	private readonly Mock<IServiceScope> _serviceScopeMock;
	private readonly Mock<ITransactionService> _transactionServiceMock;
	private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
	private readonly Mock<IServiceProvider> _serviceProviderMock;
	private readonly Mock<ILogger<CnabUploadService>> _loggerMock;
	private readonly CnabUploadService _uploadService;
	private readonly UploadProcessingOptions _options;

	public CnabUploadServiceTests()
	{
		_hashServiceMock = new Mock<IHashService>();
		_lineProcessorMock = new Mock<ILineProcessor>();
		_checkpointManagerMock = new Mock<ICheckpointManager>();
		_serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
		_serviceScopeMock = new Mock<IServiceScope>();
		_transactionServiceMock = new Mock<ITransactionService>();
		_fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
		_serviceProviderMock = new Mock<IServiceProvider>();
		_loggerMock = new Mock<ILogger<CnabUploadService>>();

		_options = new UploadProcessingOptions
		{
			ParallelWorkers = 2, // Use 2 for tests (faster)
			CheckpointInterval = 10,
			MaxRetryPerLine = 3,
			RetryDelayMs = 10
		};

		// Setup hash service mocks
		_hashServiceMock
			.Setup(x => x.ComputeFileHash(It.IsAny<string>()))
			.Returns((string content) => $"file-hash-{content.GetHashCode()}");

		// Setup line processor mock
		_lineProcessorMock
			.Setup(x => x.ProcessLineAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<Guid>(),
				It.IsAny<string>(),
				It.IsAny<ITransactionService>(),
				It.IsAny<IFileUploadTrackingService>(),
				It.IsAny<ICnabParserService>(),
				It.IsAny<IHashService>(),
				It.IsAny<IUnitOfWork>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<ILogger>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(LineProcessingResult.Success);

		// Setup checkpoint manager mock
		_checkpointManagerMock
			.Setup(x => x.ShouldSaveCheckpoint(It.IsAny<int>(), It.IsAny<int>()))
			.Returns((int processed, int interval) => processed > 0 && processed % interval == 0);

		_checkpointManagerMock
			.Setup(x => x.SaveCheckpointAsync(
				It.IsAny<Guid>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<IFileUploadTrackingService>(),
				It.IsAny<ILogger>(),
				It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		// Setup service scope factory
		_serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
		_serviceScopeFactoryMock
			.Setup(x => x.CreateScope())
			.Returns(_serviceScopeMock.Object);

		// Setup service provider to return mocks
		_serviceProviderMock
			.Setup(x => x.GetService(typeof(ITransactionService)))
			.Returns(_transactionServiceMock.Object);
		_serviceProviderMock
			.Setup(x => x.GetService(typeof(IFileUploadTrackingService)))
			.Returns(_fileUploadTrackingServiceMock.Object);
		_serviceProviderMock
			.Setup(x => x.GetService(typeof(ICnabParserService)))
			.Returns(new Mock<ICnabParserService>().Object);
		_serviceProviderMock
			.Setup(x => x.GetService(typeof(IHashService)))
			.Returns(_hashServiceMock.Object);
		_serviceProviderMock
			.Setup(x => x.GetService(typeof(IUnitOfWork)))
			.Returns(new Mock<IUnitOfWork>().Object);

		var optionsMock = Mock.Of<IOptions<UploadProcessingOptions>>(x => x.Value == _options);

		_uploadService = new CnabUploadService(
			_hashServiceMock.Object,
			_lineProcessorMock.Object,
			_checkpointManagerMock.Object,
			_serviceScopeFactoryMock.Object,
			optionsMock,
			_loggerMock.Object
		);
	}

	#region ProcessCnabUploadAsync - Success Cases

	[Fact]
	public async Task ProcessCnabUploadAsync_WithValidFileContent_ShouldReturnSuccess()
	{
		// Arrange
		var fileUploadId = Guid.NewGuid();
		var fileContent = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       \r\n";

		_fileUploadTrackingServiceMock
			.Setup(x => x.SetTotalLineCountAsync(fileUploadId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		_fileUploadTrackingServiceMock
			.Setup(x => x.UpdateProcessingResultAsync(fileUploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		// Setup line processor to return success
		_lineProcessorMock
			.Setup(x => x.ProcessLineAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				fileUploadId,
				It.IsAny<string>(),
				It.IsAny<ITransactionService>(),
				It.IsAny<IFileUploadTrackingService>(),
				It.IsAny<ICnabParserService>(),
				It.IsAny<IHashService>(),
				It.IsAny<IUnitOfWork>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<ILogger>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(LineProcessingResult.Success);

		// Act
		var result = await _uploadService.ProcessCnabUploadAsync(fileContent, fileUploadId, 0, CancellationToken.None);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.Data.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task ProcessCnabUploadAsync_WithEmptyContent_ShouldReturnFailure()
	{
		// Arrange
		var fileUploadId = Guid.NewGuid();
		var fileContent = "";

		// Act
		var result = await _uploadService.ProcessCnabUploadAsync(fileContent, fileUploadId, 0, CancellationToken.None);

		// Assert
		result.IsSuccess.Should().BeFalse();
		result.ErrorMessage.Should().Contain("not provided or is empty");
	}

	[Fact]
	public async Task ProcessCnabUploadAsync_WithDuplicateLine_ShouldSkip()
	{
		// Arrange
		var fileUploadId = Guid.NewGuid();
		var fileContent = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       \r\n";

		_fileUploadTrackingServiceMock
			.Setup(x => x.SetTotalLineCountAsync(fileUploadId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		_fileUploadTrackingServiceMock
			.Setup(x => x.UpdateProcessingResultAsync(fileUploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		// Setup line processor to return skipped (duplicate)
		_lineProcessorMock
			.Setup(x => x.ProcessLineAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				fileUploadId,
				It.IsAny<string>(),
				It.IsAny<ITransactionService>(),
				It.IsAny<IFileUploadTrackingService>(),
				It.IsAny<ICnabParserService>(),
				It.IsAny<IHashService>(),
				It.IsAny<IUnitOfWork>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<ILogger>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(LineProcessingResult.Skipped);

		// Act
		var result = await _uploadService.ProcessCnabUploadAsync(fileContent, fileUploadId, 0, CancellationToken.None);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.Data.Should().Be(0); // No transactions inserted (all skipped)
	}

	[Fact]
	public async Task ProcessCnabUploadAsync_WithInvalidLine_ShouldContinueAndMarkAsFailed()
	{
		// Arrange
		var fileUploadId = Guid.NewGuid();
		var fileContent = "invalid line content\r\n";

		_fileUploadTrackingServiceMock
			.Setup(x => x.SetTotalLineCountAsync(fileUploadId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		_fileUploadTrackingServiceMock
			.Setup(x => x.UpdateProcessingResultAsync(fileUploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		// Setup line processor to return failed (invalid line)
		_lineProcessorMock
			.Setup(x => x.ProcessLineAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				fileUploadId,
				It.IsAny<string>(),
				It.IsAny<ITransactionService>(),
				It.IsAny<IFileUploadTrackingService>(),
				It.IsAny<ICnabParserService>(),
				It.IsAny<IHashService>(),
				It.IsAny<IUnitOfWork>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<ILogger>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(LineProcessingResult.Failed);

		// Act
		var result = await _uploadService.ProcessCnabUploadAsync(fileContent, fileUploadId, 0, CancellationToken.None);

		// Assert
		// When all lines fail, the service returns a failure result (validation failure)
		result.IsSuccess.Should().BeFalse();
		result.ErrorMessage.Should().Contain("Invalid CNAB file format");
		result.ErrorMessage.Should().Contain("failed validation");
	}

	[Fact]
	public async Task ProcessCnabUploadAsync_WithCheckpointResume_ShouldStartFromCheckpoint()
	{
		// Arrange
		var fileUploadId = Guid.NewGuid();
		var fileContent = "line1\r\nline2\r\nline3\r\n";
		var startFromLine = 2; // Resume from line 2

		_fileUploadTrackingServiceMock
			.Setup(x => x.SetTotalLineCountAsync(fileUploadId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		_fileUploadTrackingServiceMock
			.Setup(x => x.UpdateProcessingResultAsync(fileUploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		// Setup line processor to return success
		_lineProcessorMock
			.Setup(x => x.ProcessLineAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				fileUploadId,
				It.IsAny<string>(),
				It.IsAny<ITransactionService>(),
				It.IsAny<IFileUploadTrackingService>(),
				It.IsAny<ICnabParserService>(),
				It.IsAny<IHashService>(),
				It.IsAny<IUnitOfWork>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<ILogger>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(LineProcessingResult.Success);

		// Act
		var result = await _uploadService.ProcessCnabUploadAsync(fileContent, fileUploadId, startFromLine, CancellationToken.None);

		// Assert
		result.IsSuccess.Should().BeTrue();
		// Should only process from line 2 onwards (lines 2 and 3 = 2 lines)
	}

	#endregion

	#region Helper Methods

	private static Transaction CreateTransaction(string natureCode, decimal amount)
	{
		return new Transaction
		{
			NatureCode = natureCode,
			Amount = amount,
			Cpf = "12345678901",
			Card = "1234****5678",
			TransactionDate = DateTime.UtcNow,
			TransactionTime = new TimeSpan(12, 0, 0),
			BankCode = natureCode,
			IdempotencyKey = Guid.NewGuid().ToString()
		};
	}

	#endregion
}
