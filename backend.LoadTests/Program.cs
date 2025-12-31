using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Http.CSharp;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace backend.LoadTests;

/// <summary>
/// Load testing scenarios for CNAB API using NBomber.
/// Tests various endpoints under different load conditions.
/// </summary>
class Program
{
    private static string ApiBaseUrl { get; set; } = "http://localhost:5000/api/v1";
    private static string? AccessToken { get; set; }

    static void Main(string[] args)
    {
        // Load configuration (optional - uses defaults if file doesn't exist)
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var configuration = configurationBuilder.Build();

        ApiBaseUrl = configuration["LoadTest:ApiBaseUrl"] ?? ApiBaseUrl;
        
        // Use default test credentials (no configuration needed)
        var testUsername = configuration["LoadTest:TestUser:Username"] ?? "loadtest@example.com";
        var testPassword = configuration["LoadTest:TestUser:Password"] ?? "LoadTest123!";

        Console.WriteLine("🚀 Starting CNAB API Load Tests");
        Console.WriteLine($"API Base URL: {ApiBaseUrl}");
        Console.WriteLine();

        // Check if API is accessible
        Console.WriteLine("🔍 Checking API accessibility...");
        if (!IsApiAccessible())
        {
            Console.WriteLine();
            Console.WriteLine("❌ API is not accessible. Please ensure the API is running.");
            Console.WriteLine();
            Console.WriteLine("Troubleshooting:");
            Console.WriteLine($"   1. Verify API is running: docker-compose ps api");
            Console.WriteLine($"   2. Check API logs: docker-compose logs api");
            Console.WriteLine($"   3. Test manually: curl {ApiBaseUrl}/health");
            Console.WriteLine($"   4. If running locally: dotnet run (in backend directory)");
            Console.WriteLine($"   5. Expected URL: {ApiBaseUrl}");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("✅ API is accessible");
        Console.WriteLine();

        // Authenticate (with automatic user creation if needed)
        Console.WriteLine("🔐 Authenticating...");
        try
        {
            AccessToken = AuthenticateOrCreateUserAsync(testUsername, testPassword).GetAwaiter().GetResult();
            
            if (string.IsNullOrEmpty(AccessToken))
            {
                Console.WriteLine();
                Console.WriteLine("❌ Failed to authenticate. Please check API logs for details.");
                Console.WriteLine();
                Console.WriteLine("Troubleshooting:");
                Console.WriteLine($"   1. Check API logs: docker-compose logs api | Select-Object -Last 20");
                Console.WriteLine($"   2. Test registration manually:");
                Console.WriteLine($"      curl -X POST {ApiBaseUrl}/auth/register \\");
                Console.WriteLine($"        -H \"Content-Type: application/json\" \\");
                Console.WriteLine($"        -d '{{\"username\":\"{testUsername}\",\"password\":\"{testPassword}\"}}'");
                Console.WriteLine();
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ Error during authentication: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine();
            return;
        }

        Console.WriteLine("✅ Authentication successful");
        Console.WriteLine();

        // Run load test scenarios
        var scenario1 = CreateHealthCheckScenario();
        var scenario2 = CreateGetUploadsScenario();
        var scenario3 = CreateGetTransactionsScenario();
        var scenario4 = CreateUploadFileScenario();

        NBomberRunner
            .RegisterScenarios(scenario1, scenario2, scenario3, scenario4)
            .Run();

        Console.WriteLine();
        Console.WriteLine("✅ Load tests completed!");
    }

    private static bool IsApiAccessible()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            // Health check endpoint is at /api/v1/health
            // ApiBaseUrl is already http://localhost:5000/api/v1, so we just add /health
            var healthUrl = $"{ApiBaseUrl}/health";
            
            Console.WriteLine($"   Testing: {healthUrl}");
            
            try
            {
                // Use GetStringAsync for simpler error handling
                var response = httpClient.GetAsync(healthUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"   ✅ Health check successful: {response.StatusCode}");
                    return true;
                }
                
                Console.WriteLine($"   ⚠️  Health check returned: {response.StatusCode} {response.ReasonPhrase}");
                
                // Try reading response body for more info
                try
                {
                    var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(content) && content.Length < 200)
                    {
                        Console.WriteLine($"   Response: {content.Substring(0, Math.Min(content.Length, 100))}...");
                    }
                }
                catch
                {
                    // Ignore content read errors
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"   ❌ HTTP error: {httpEx.Message}");
                if (httpEx.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {httpEx.InnerException.Message}");
                }
                
                // Check if it's a connection refused error
                if (httpEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                    httpEx.Message.Contains("refused", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"   💡 API may not be running. Start with: docker-compose up -d api");
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"   ❌ Request timeout - API may be slow or not responding");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Error: {ex.GetType().Name}: {ex.Message}");
            }

            // If health check fails, try alternative endpoints
            Console.WriteLine($"   Trying alternative endpoints...");
            var baseUri = new Uri(ApiBaseUrl);
            var alternativeEndpoints = new[]
            {
                "/api/v1/health/live",
                "/swagger",
                "/health"
            };

            foreach (var endpoint in alternativeEndpoints)
            {
                try
                {
                    var testUrl = $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}{endpoint}";
                    Console.WriteLine($"   Trying: {testUrl}");
                    
                    var testResponse = httpClient.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                    if (testResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"   ✅ Alternative endpoint accessible: {endpoint}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed: {ex.Message}");
                    // Try next endpoint
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Connection error: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    private static async Task<string?> AuthenticateOrCreateUserAsync(string username, string password)
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(ApiBaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        // Step 1: Try to login first
        var loginToken = await TryLoginAsync(httpClient, username, password);
        if (!string.IsNullOrEmpty(loginToken))
        {
            return loginToken;
        }

        // Step 2: If login fails, try to create the user automatically
        Console.WriteLine("   User not found. Creating test user automatically...");
        var registerResult = await TryRegisterAsync(httpClient, username, password);
        
        if (registerResult.Success)
        {
            Console.WriteLine("   ✅ Test user created successfully");
            // Step 3: Try login again after registration
            var token = await TryLoginAsync(httpClient, username, password);
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
            // If registration succeeded but login still fails, return the token from registration if available
            return registerResult.AccessToken;
        }
        else if (registerResult.UserAlreadyExists)
        {
            // User exists but login failed - might be wrong password or user was just created
            Console.WriteLine("   ⚠️  User already exists. Trying login again...");
            // Try login one more time
            var token = await TryLoginAsync(httpClient, username, password);
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
            Console.WriteLine("   ❌ Login still failing. User may exist with different password.");
            return null;
        }
        else
        {
            var errorMsg = string.IsNullOrWhiteSpace(registerResult.ErrorMessage) 
                ? "Unknown error (empty response)" 
                : registerResult.ErrorMessage;
            Console.WriteLine($"   ❌ Failed to create user: {errorMsg}");
            return null;
        }
    }

    private static async Task<string?> TryLoginAsync(HttpClient httpClient, string username, string password)
    {
        try
        {
            var loginRequest = new
            {
                username,
                password
            };

            var json = JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var loginUrl = $"{ApiBaseUrl}/auth/login";
            var response = await httpClient.PostAsync(loginUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return loginResponse?.AccessToken;
            }
        }
        catch
        {
            // Silent fail - will try registration
        }

        return null;
    }

    private static async Task<RegisterResult> TryRegisterAsync(HttpClient httpClient, string username, string password)
    {
        try
        {
            var registerRequest = new
            {
                username,
                password
            };

            var json = JsonSerializer.Serialize(registerRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var registerUrl = $"{ApiBaseUrl}/auth/register";
            var response = await httpClient.PostAsync(registerUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var registerResponse = JsonSerializer.Deserialize<LoginResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // If registration returns a token, use it
                if (!string.IsNullOrEmpty(registerResponse?.AccessToken))
                {
                    return new RegisterResult { Success = true, AccessToken = registerResponse.AccessToken };
                }

                return new RegisterResult { Success = true };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // User already exists (409 Conflict)
                return new RegisterResult { UserAlreadyExists = true };
            }
            else
            {
                // Try to parse error message
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    var errorMessage = errorResponse?.ContainsKey("detail") == true 
                        ? errorResponse["detail"]?.ToString() 
                        : responseContent;

                    return new RegisterResult { ErrorMessage = errorMessage ?? "Unknown error" };
                }
                catch
                {
                    return new RegisterResult { ErrorMessage = responseContent };
                }
            }
        }
        catch (Exception ex)
        {
            return new RegisterResult { ErrorMessage = ex.Message };
        }
    }

    private class RegisterResult
    {
        public bool Success { get; set; }
        public bool UserAlreadyExists { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AccessToken { get; set; }
    }

    private static ScenarioProps CreateHealthCheckScenario()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(ApiBaseUrl);

        var scenario = Scenario.Create("Health Check", async context =>
        {
            // Create a new request for each iteration
            var healthCheck = Http.CreateRequest("GET", $"{ApiBaseUrl}/health")
                .WithHeader("Accept", "application/json");
            
            var response = await httpClient.SendAsync(healthCheck);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );
        
        return scenario;
    }

    private static ScenarioProps CreateGetUploadsScenario()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(ApiBaseUrl);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

        var scenario = Scenario.Create("Get Uploads", async context =>
        {
            // Create a new request for each iteration
            var getUploads = Http.CreateRequest("GET", $"{ApiBaseUrl}/transactions/uploads?page=1&pageSize=50")
                .WithHeader("Accept", "application/json");
            
            var response = await httpClient.SendAsync(getUploads);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );
        
        return scenario;
    }

    private static ScenarioProps CreateGetTransactionsScenario()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(ApiBaseUrl);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

        var scenario = Scenario.Create("Get Transactions", async context =>
        {
            // Create a new request for each iteration with a new GUID
            var getTransactions = Http.CreateRequest("GET", $"{ApiBaseUrl}/transactions/stores/{Guid.NewGuid()}?page=1&pageSize=20")
                .WithHeader("Accept", "application/json");
            
            var response = await httpClient.SendAsync(getTransactions);
            // 404 is acceptable for this test (upload may not exist)
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? Response.Ok()
                : Response.Fail();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );
        
        return scenario;
    }

    private static ScenarioProps CreateUploadFileScenario()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(ApiBaseUrl);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        httpClient.Timeout = TimeSpan.FromMinutes(5); // Uploads may take longer

        var scenario = Scenario.Create("Upload CNAB File", async context =>
        {
            // Generate a unique CNAB file with multiple lines (1-5 lines) to ensure unique hash
            // This guarantees each file has a unique SHA256 hash and avoids duplicate detection
            var random = new Random();
            var lineCount = random.Next(1, 6); // Generate 1-5 lines per file
            var lines = new List<string>();
            
            for (int i = 0; i < lineCount; i++)
            {
                lines.Add(GenerateUniqueCnabLine());
            }
            
            var fileContent = string.Join("\n", lines);
            var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent));
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(byteContent, "file", $"test_{Guid.NewGuid():N}.txt");

            try
            {
                var response = await httpClient.PostAsync($"{ApiBaseUrl}/transactions/upload", content);
                // 200 OK, 202 Accepted (async processing), and 409 Conflict (duplicate detection working)
                // are all valid responses that indicate the system is working correctly
                return response.IsSuccessStatusCode 
                    || response.StatusCode == System.Net.HttpStatusCode.Accepted
                    || response.StatusCode == System.Net.HttpStatusCode.Conflict
                    ? Response.Ok()
                    : Response.Fail();
            }
            catch
            {
                return Response.Fail();
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 1, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(120))
        );
        
        return scenario;
    }

    // Sample owners and stores (with accents preserved for realistic data)
    private static readonly string[] SampleOwners = new[]
    {
        "JOÃO MACEDO",
        "MARIA JOSEFINA",
        "MARCOS PEREIRA",
        "JOSÉ COSTA",
        "ANA SOUZA",
        "CARLOS ALMEIDA",
        "MÁRCIA LIMA",
        "FERNANDO SILVA",
        "PAULA MENDES",
        "RODRIGO ARAÚJO"
    };

    private static readonly string[] SampleStores = new[]
    {
        "BAR DO JOÃO",
        "LOJA DO Ó - MATRIZ",
        "LOJA DO Ó - FILIAL",
        "MERCADO DA AVENIDA",
        "MERCEARIA 3 IRMÃOS",
        "PADARIA PÃO DOCE",
        "SUPERMERCADO BOM PREÇO",
        "FARMÁCIA SAÚDE",
        "OFICINA CENTRAL",
        "LOJA DE ELETRÔNICOS"
    };

    private static string GenerateUniqueCnabLine()
    {
        // Generate a unique CNAB line (exactly 80 characters) based on Python generator
        // Format: [0:1] type, [1:9] date, [9:19] amount, [19:30] CPF, [30:42] card, [42:48] time, [48:62] owner, [62:80] store
        // Uses current timestamp in microseconds as seed component to ensure uniqueness
        
        // Use combination of current time and random to ensure each call generates unique values
        var seed = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
        var random = new Random(seed + Environment.TickCount);
        
        // Type: 1-9 (1 char)
        var type = random.Next(1, 10).ToString();
        
        // Date: Random date between 2019-01-01 and today (8 chars)
        // Add some randomness based on current tick to ensure variation
        var startDate = new DateTime(2019, 1, 1);
        var endDate = DateTime.UtcNow;
        var daysRange = (int)(endDate - startDate).TotalDays;
        var dayOffset = random.Next(0, Math.Max(1, daysRange));
        var randomDate = startDate.AddDays(dayOffset);
        var date = randomDate.ToString("yyyyMMdd");
        
        // Amount: Random value from 0.01 to 99,999,999.99 (10 chars, in cents)
        // Use high precision to ensure uniqueness
        var value = random.NextDouble() * 99999999.99 + 0.01;
        var amount = ((long)Math.Round(value * 100)).ToString("D10");
        
        // CPF: Valid Brazilian CPF (11 chars) - always unique due to algorithm
        var cpf = GenerateValidCpf(random);
        
        // Card: 4 digits + "****" + 4 digits = 12 chars
        var card = GenerateRandomCard(random);
        
        // Time: Random time (6 chars) - use milliseconds component for uniqueness
        var hour = random.Next(0, 24);
        var minute = random.Next(0, 60);
        var second = random.Next(0, 60);
        var time = $"{hour:D2}{minute:D2}{second:D2}";
        
        // Owner: Random from sample list, padded to 14 chars
        var ownerIndex = random.Next(SampleOwners.Length);
        var ownerStr = SampleOwners[ownerIndex].PadRight(14).Substring(0, 14);
        
        // Store: Random from sample list, padded to 18 chars (not 19, backend expects 80 total)
        var storeIndex = random.Next(SampleStores.Length);
        var storeStr = SampleStores[storeIndex].PadRight(18).Substring(0, 18);

        // Concatenate and ensure exactly 80 characters
        var line = $"{type}{date}{amount}{cpf}{card}{time}{ownerStr}{storeStr}";
        
        // Final safety check - pad or truncate to exactly 80 characters
        if (line.Length < 80)
            line = line.PadRight(80, ' ');
        else if (line.Length > 80)
            line = line.Substring(0, 80);
            
        return line;
    }

    /// <summary>
    /// Generates a valid CPF using the Brazilian CPF algorithm (converted from Python).
    /// </summary>
    private static string GenerateValidCpf(Random random)
    {
        // Generate first 9 digits
        var nums = new int[9];
        for (int i = 0; i < 9; i++)
        {
            nums[i] = random.Next(0, 10);
        }

        // Calculate check digits using Brazilian CPF algorithm
        int CalcDigit(int[] digits)
        {
            int factor = digits.Length + 1;
            int sum = 0;
            foreach (var d in digits)
            {
                sum += d * factor;
                factor--;
            }
            int r = 11 - (sum % 11);
            return r >= 10 ? 0 : r;
        }

        int d1 = CalcDigit(nums);
        int d2 = CalcDigit(nums.Concat(new[] { d1 }).ToArray());

        return string.Join("", nums) + d1 + d2;
    }

    /// <summary>
    /// Generates a random card number in format: 4 digits + "****" + 4 digits = 12 chars.
    /// </summary>
    private static string GenerateRandomCard(Random random)
    {
        var left = string.Join("", Enumerable.Range(0, 4).Select(_ => random.Next(0, 10)));
        var right = string.Join("", Enumerable.Range(0, 4).Select(_ => random.Next(0, 10)));
        return $"{left}****{right}";
    }

    private class LoginResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Username { get; set; }
        public string? Role { get; set; }
    }
}
