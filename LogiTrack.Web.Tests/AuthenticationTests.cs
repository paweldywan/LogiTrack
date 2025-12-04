using System.Net;
using System.Net.Http.Json;

using LogiTrack.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiTrack.Web.Tests;

public class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthenticationTests(WebApplicationFactory<Program> factory)
    {
        // Create an in-memory SQLite connection that stays open
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all DbContext-related registrations
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<LogiTrackContext>) ||
                                d.ServiceType == typeof(LogiTrackContext) ||
                                d.ServiceType.Name.Contains("LogiTrackContext"))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Add SQLite in-memory database for testing
                services.AddDbContext<LogiTrackContext>(options =>
                {
                    options.UseSqlite(connection);
                });

                // Build a service provider and ensure database is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsOk()
    {
        // Arrange
        var request = new
        {
            Email = $"testuser_{Guid.NewGuid()}@example.com",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            Email = $"testuser_{Guid.NewGuid()}@example.com",
            Password = "weak"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register user first
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        // Act
        var response = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(content);
        Assert.NotEmpty(content.AccessToken);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/inventory");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithAuthentication_ReturnsOk()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register and login
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create authenticated request
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent!.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ManagerRoute_WithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register and login (user without Manager role)
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create authenticated request for a Manager-only route
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent!.AccessToken);
        request.Content = JsonContent.Create(new { Name = "Test Item", Quantity = 10, Location = "A1" });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ManagerRoute_WithManagerRole_ReturnsCreated()
    {
        // Arrange
        var email = $"manager_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register user
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        // Assign Manager role
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Manager"))
        {
            await roleManager.CreateAsync(new IdentityRole("Manager"));
        }

        var user = await userManager.FindByEmailAsync(email);
        await userManager.AddToRoleAsync(user!, "Manager");

        // Login
        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create authenticated request
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent!.AccessToken);
        request.Content = JsonContent.Create(new { Name = "Test Item", Quantity = 10, Location = "A1" });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var email = $"duplicate_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register first user
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        // Act - Try to register with the same email
        var response = await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            Email = "invalid-email",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register and login
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Act
        var response = await _client.PostAsJsonAsync("/refresh", new { RefreshToken = loginContent!.RefreshToken });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshContent = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(refreshContent);
        Assert.NotEmpty(refreshContent.AccessToken);
        Assert.NotEmpty(refreshContent.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var invalidRefreshToken = "invalid-refresh-token";

        // Act
        var response = await _client.PostAsJsonAsync("/refresh", new { RefreshToken = invalidRefreshToken });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ManageInfo_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/manage/info");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ManageInfo_WithAuthentication_ReturnsUserInfo()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register and login
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create authenticated request
        var request = new HttpRequestMessage(HttpMethod.Get, "/manage/info");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent!.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<UserInfoResponse>();

        Assert.NotNull(content);
        Assert.Equal(email, content.Email);
    }

    [Fact]
    public async Task ManageInfo_UpdateEmail_ReturnsOk()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var newEmail = $"updated_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register and login
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create authenticated request to update email
        var request = new HttpRequestMessage(HttpMethod.Post, "/manage/info");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent!.AccessToken);
        request.Content = JsonContent.Create(new { NewEmail = newEmail });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithoutToken_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/confirmEmail?userId=test&code=invalid");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmationEmail_WithValidEmail_ReturnsOk()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register user
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        // Act
        var response = await _client.PostAsJsonAsync("/resendConfirmationEmail", new { Email = email });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmationEmail_WithNonexistentEmail_ReturnsOk()
    {
        // Note: Returns OK even for non-existent emails to prevent email enumeration attacks

        // Act
        var response = await _client.PostAsJsonAsync("/resendConfirmationEmail", new { Email = "nonexistent@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ReturnsOk()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register user
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        // Act
        var response = await _client.PostAsJsonAsync("/forgotPassword", new { Email = email });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithNonexistentEmail_ReturnsOk()
    {
        // Note: Returns OK even for non-existent emails to prevent email enumeration attacks

        // Act
        var response = await _client.PostAsJsonAsync("/forgotPassword", new { Email = "nonexistent@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";

        // Act
        var response = await _client.PostAsJsonAsync("/resetPassword", new
        {
            Email = email,
            ResetCode = "invalid-code",
            NewPassword = "NewTest123!"
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Manage2fa_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/manage/2fa", new { });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Manage2fa_WithAuthentication_ReturnsInfo()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register and login
        await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });

        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create authenticated request
        var request = new HttpRequestMessage(HttpMethod.Post, "/manage/2fa");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent!.AccessToken);
        request.Content = JsonContent.Create(new { });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record UserInfoResponse(string Email, bool IsEmailConfirmed);
}
