using System.Net.Http.Json;

using LogiTrack.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiTrack.Web.Tests;

/// <summary>
/// Shared test fixture for integration tests. Configures an in-memory SQLite database
/// and provides helper methods for authentication and role management.
/// </summary>
public class TestFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    public WebApplicationFactory<Program> Factory { get; }
    public HttpClient Client { get; }

    public TestFixture()
    {
        // Create an in-memory SQLite connection that stays open
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
                    options.UseSqlite(_connection);
                });

                // Build a service provider and ensure database is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
                db.Database.EnsureCreated();
            });
        });

        Client = Factory.CreateClient();
    }

    /// <summary>
    /// Registers a new user with the specified email and password.
    /// </summary>
    public async Task RegisterUserAsync(string email, string password)
    {
        await Client.PostAsJsonAsync("/register", new { Email = email, Password = password });
    }

    /// <summary>
    /// Logs in with the specified credentials and returns the access token.
    /// </summary>
    public async Task<string> LoginAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/login", new { Email = email, Password = password });
        var content = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return content?.AccessToken ?? throw new InvalidOperationException("Login failed");
    }

    /// <summary>
    /// Registers a user and returns their access token.
    /// </summary>
    public async Task<string> RegisterAndLoginAsync(string? email = null, string password = "Test123!")
    {
        email ??= $"testuser_{Guid.NewGuid()}@example.com";
        await RegisterUserAsync(email, password);
        return await LoginAsync(email, password);
    }

    /// <summary>
    /// Assigns the Manager role to the specified user.
    /// </summary>
    public async Task AssignManagerRoleAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Manager"))
        {
            await roleManager.CreateAsync(new IdentityRole("Manager"));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user != null)
        {
            await userManager.AddToRoleAsync(user, "Manager");
        }
    }

    /// <summary>
    /// Registers a user with Manager role and returns their access token.
    /// </summary>
    public async Task<string> RegisterManagerAndLoginAsync(string? email = null, string password = "Test123!")
    {
        email ??= $"manager_{Guid.NewGuid()}@example.com";
        await RegisterUserAsync(email, password);
        await AssignManagerRoleAsync(email);
        return await LoginAsync(email, password);
    }

    /// <summary>
    /// Creates an HttpRequestMessage with Bearer authorization header.
    /// </summary>
    public HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    /// <summary>
    /// Creates an HttpRequestMessage with Bearer authorization header and JSON content.
    /// </summary>
    public HttpRequestMessage CreateAuthorizedRequest<T>(HttpMethod method, string url, string token, T content)
    {
        var request = CreateAuthorizedRequest(method, url, token);
        request.Content = JsonContent.Create(content);
        return request;
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// DTO for login response.
/// </summary>
public record LoginResponse(
    string TokenType,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);
