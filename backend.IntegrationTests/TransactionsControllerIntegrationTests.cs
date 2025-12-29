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
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
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
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
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
    public async Task UploadCnabFile_WithInvalidLineLength_ReturnsUnprocessableEntity()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsync();
        var cnabContent = "123"; // Too short
        
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(cnabContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "invalid.txt");

        // Act
        var response = await client.PostAsync("/api/v1/transactions/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Invalid");
        responseContent.Should().Contain("80");
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
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        uploadContent.Add(fileContent, "file", "test.txt");
        
        await client.PostAsync("/api/v1/transactions/upload", uploadContent);

        // Act
        var deleteResponse = await client.DeleteAsync("/api/v1/transactions");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
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
