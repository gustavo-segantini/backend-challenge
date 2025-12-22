using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using FluentAssertions;
using CnabApi.Models;

namespace CnabApi.IntegrationTests;

/// <summary>
/// Integration tests for the Transactions API endpoints.
/// Tests the full request-response cycle including database operations.
/// </summary>
public class TransactionsControllerIntegrationTests(CnabApiFactory factory) : IClassFixture<CnabApiFactory>
{
    private readonly CnabApiFactory _factory = factory;

    [Fact]
    public async Task UploadCnabFile_WithValidFile_ReturnsSuccessAndCount()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        var cnabContent = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       \n" +
                         "5201903010000013200556418150633123****7687145607MARIA JOSEFINALOJA DO Ó - MATRIZ";
        
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(cnabContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        // Act
        var response = await client.PostAsync("/api/v1/transactions/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result.Message.Should().Contain("Successfully imported 2 transactions");
    }

    [Fact]
    public async Task UploadCnabFile_WithEmptyFile_ReturnsBadRequest()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "empty.txt");

        // Act
        var response = await client.PostAsync("/api/v1/transactions/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        result.Should().NotBeNull();
        result!.Error.Should().Contain("not provided or is empty");
    }

    [Fact]
    public async Task UploadCnabFile_WithInvalidLineLength_ReturnsBadRequest()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        var cnabContent = "123"; // Too short
        
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(cnabContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "invalid.txt");

        // Act
        var response = await client.PostAsync("/api/v1/transactions/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Invalid line");
        responseContent.Should().Contain("80");
    }

    [Fact]
    public async Task GetTransactionsByCpf_WithExistingCpf_ReturnsTransactions()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        
        // Upload test data first
        var cnabContent = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       \n" +
                         "1201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ";
        
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(cnabContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        uploadContent.Add(fileContent, "file", "test.txt");
        
        await client.PostAsync("/api/v1/transactions/upload", uploadContent);

        // Act
        var response = await client.GetAsync("/api/v1/transactions/09620676017?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        paged.Should().NotBeNull();
        paged!.Items.Count.Should().Be(2);
        paged.TotalCount.Should().Be(2);
        paged.Items.All(t => t.Cpf == "09620676017").Should().BeTrue();
    }

    [Fact]
    public async Task GetTransactionsByCpf_WithNonExistingCpf_ReturnsEmptyList()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/transactions/99999999999?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        paged.Should().NotBeNull();
        paged!.Items.Should().BeEmpty();
        paged.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBalance_WithExistingCpf_ReturnsCorrectBalance()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        
        // Upload test data: 
        // Type 3 (Financing - Expense): -142.00
        // Type 1 (Debit - Income): +152.00
        // Expected balance: +10.00
        var cnabContent = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       \n" +
                         "1201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       ";
        
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(cnabContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        uploadContent.Add(fileContent, "file", "test.txt");
        
        await client.PostAsync("/api/v1/transactions/upload", uploadContent);

        // Act
        var response = await client.GetAsync("/api/v1/transactions/09620676017/balance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result.Should().NotBeNull();
        result!.Balance.Should().Be(10.00m); // 152 - 142 = 10
    }

    [Fact]
    public async Task GetBalance_WithNonExistingCpf_ReturnsZero()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/transactions/99999999999/balance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result.Should().NotBeNull();
        result!.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task ClearData_RemovesAllTransactions()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        
        // Upload test data
        var cnabContent = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(cnabContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        uploadContent.Add(fileContent, "file", "test.txt");
        
        await client.PostAsync("/api/v1/transactions/upload", uploadContent);

        // Act
        var deleteResponse = await client.DeleteAsync("/api/v1/transactions");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify data is cleared
        var getResponse = await client.GetAsync("/api/v1/transactions/09620676017?page=1&pageSize=10");
        var transactions = await getResponse.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        transactions.Should().NotBeNull();
        transactions!.Items.Should().BeEmpty();
        transactions.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FullWorkflow_UploadQueryBalanceClear_WorksCorrectly()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        var cnabContent = 
            "1201903010000015200096206760171234****7890233000JOÃO MACEDO   BAR DO JOÃO       \n" + // +152
            "2201903010000011200096206760173648****0099234234JOÃO MACEDO   BAR DO JOÃO       \n" + // -112
            "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       \n" + // -142
            "4201906010000050617845152540731234****2231100000MARCOS PEREIRAMERCADO DA AVENIDA";   // +506.17

        // Act & Assert - Upload
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(cnabContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        uploadContent.Add(fileContent, "file", "workflow.txt");
        
        var uploadResponse = await client.PostAsync("/api/v1/transactions/upload", uploadContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        uploadResult!.Count.Should().Be(4);

        // Act & Assert - Query CPF 1
        var queryResponse1 = await client.GetAsync("/api/v1/transactions/09620676017?page=1&pageSize=10");
        queryResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var transactions1 = await queryResponse1.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        transactions1!.Items.Count.Should().Be(3);
        transactions1.TotalCount.Should().Be(3);

        // Act & Assert - Balance CPF 1
        var balanceResponse1 = await client.GetAsync("/api/v1/transactions/09620676017/balance");
        var balance1 = await balanceResponse1.Content.ReadFromJsonAsync<BalanceResponse>();
        balance1!.Balance.Should().Be(-102.00m); // 152 - 112 - 142 = -102

        // Act & Assert - Query CPF 2
        var queryResponse2 = await client.GetAsync("/api/v1/transactions/84515254073?page=1&pageSize=10");
        var transactions2 = await queryResponse2.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        transactions2!.Items.Count.Should().Be(1);
        transactions2.TotalCount.Should().Be(1);

        // Act & Assert - Balance CPF 2
        var balanceResponse2 = await client.GetAsync("/api/v1/transactions/84515254073/balance");
        var balance2 = await balanceResponse2.Content.ReadFromJsonAsync<BalanceResponse>();
        balance2!.Balance.Should().Be(506.17m);

        // Act & Assert - Clear
        var clearResponse = await client.DeleteAsync("/api/v1/transactions");
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify all data cleared
        var finalQuery = await client.GetAsync("/api/v1/transactions/09620676017?page=1&pageSize=10");
        var finalTransactions = await finalQuery.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        finalTransactions!.Items.Should().BeEmpty();
        finalTransactions.TotalCount.Should().Be(0);
    }

    #region Response DTOs

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var client = _factory.CreateClientWithCleanDatabase();

        var loginRequest = new LoginRequest
        {
            Username = "admin",
            Password = "Admin123!"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private class PagedTransactionsResponse
    {
        public List<Transaction> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    private class UploadResponse
    {
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private class ErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;
    }

    private class BalanceResponse
    {
        public decimal Balance { get; set; }
    }

    private class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    private class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    #endregion
}
