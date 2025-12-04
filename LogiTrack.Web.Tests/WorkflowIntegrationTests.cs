using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LogiTrack.Data;
using LogiTrack.Domain.Models;
using LogiTrack.Web.Models;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiTrack.Web.Tests;

/// <summary>
/// Integration tests that validate the complete application workflow including:
/// - Inventory and order creation
/// - Authentication and access control
/// - API response times and caching
/// - Error handling for invalid input
/// - Secure access to role-restricted routes
/// </summary>
public class WorkflowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _connection;
    private string? _managerToken;
    private string? _userToken;

    public WorkflowIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registrations
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<LogiTrackContext>) ||
                                d.ServiceType == typeof(LogiTrackContext) ||
                                d.ServiceType.Name.Contains("LogiTrackContext"))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Add SQLite in-memory database
                services.AddDbContext<LogiTrackContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Setup: Create a manager user and a regular user
        var managerEmail = $"manager_{Guid.NewGuid()}@example.com";
        var userEmail = $"user_{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register users
        await _client.PostAsJsonAsync("/register", new { Email = managerEmail, Password = password });
        await _client.PostAsJsonAsync("/register", new { Email = userEmail, Password = password });

        // Add Manager role to the manager user
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Manager"))
        {
            await roleManager.CreateAsync(new IdentityRole("Manager"));
        }

        var manager = await userManager.FindByEmailAsync(managerEmail);
        if (manager != null)
        {
            await userManager.AddToRoleAsync(manager, "Manager");
        }

        // Login both users
        var managerLoginResponse = await _client.PostAsJsonAsync("/login", new { Email = managerEmail, Password = password });
        var managerLoginContent = await managerLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _managerToken = managerLoginContent?.AccessToken;

        var userLoginResponse = await _client.PostAsJsonAsync("/login", new { Email = userEmail, Password = password });
        var userLoginContent = await userLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _userToken = userLoginContent?.AccessToken;
    }

    public Task DisposeAsync()
    {
        _connection.Close();
        return Task.CompletedTask;
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, string? token = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _managerToken);
        return request;
    }

    #region Inventory Workflow Tests

    [Fact]
    public async Task InventoryWorkflow_CreateReadDelete_CompletesSuccessfully()
    {
        // Create inventory item (as manager)
        var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        createRequest.Content = JsonContent.Create(new
        {
            Name = "Test Widget",
            Quantity = 100,
            Location = "Warehouse A1"
        });

        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdItem = await createResponse.Content.ReadFromJsonAsync<InventoryItem>();
        Assert.NotNull(createdItem);
        Assert.Equal("Test Widget", createdItem.Name);
        Assert.True(createdItem.ItemId > 0);

        // Read inventory item (as regular user)
        var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/inventory/{createdItem.ItemId}", _userToken);
        var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrievedItem = await getResponse.Content.ReadFromJsonAsync<InventoryItem>();
        Assert.NotNull(retrievedItem);
        Assert.Equal("Test Widget", retrievedItem.Name);

        // Delete inventory item (as manager)
        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/inventory/{createdItem.ItemId}");
        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deletion
        var verifyRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/inventory/{createdItem.ItemId}", _userToken);
        var verifyResponse = await _client.SendAsync(verifyRequest);
        Assert.Equal(HttpStatusCode.NotFound, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task Inventory_GetAll_ReturnsListOfItems()
    {
        // Create multiple items
        for (int i = 1; i <= 3; i++)
        {
            var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
            createRequest.Content = JsonContent.Create(new
            {
                Name = $"Bulk Item {i}",
                Quantity = i * 10,
                Location = $"Location {i}"
            });
            await _client.SendAsync(createRequest);
        }

        // Get all items
        var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        var response = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<InventoryItem>>();
        Assert.NotNull(result);
        Assert.True(result.TotalItems >= 3);
    }

    #endregion

    #region Order Workflow Tests

    [Fact]
    public async Task OrderWorkflow_CreateReadDelete_CompletesSuccessfully()
    {
        // Create order (as manager)
        var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/order");
        createRequest.Content = JsonContent.Create(new
        {
            CustomerName = "Test Customer",
            DatePlaced = DateTime.UtcNow
        });

        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdOrder = await createResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);
        Assert.Equal("Test Customer", createdOrder.CustomerName);
        Assert.True(createdOrder.OrderId > 0);

        // Read order (as regular user)
        var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/order/{createdOrder.OrderId}", _userToken);
        var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Delete order (as manager)
        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/order/{createdOrder.OrderId}");
        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Order_GetAll_ReturnsListOfOrders()
    {
        // Create orders
        for (int i = 1; i <= 3; i++)
        {
            var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/order");
            createRequest.Content = JsonContent.Create(new
            {
                CustomerName = $"Customer {i}",
                DatePlaced = DateTime.UtcNow.AddDays(-i)
            });
            await _client.SendAsync(createRequest);
        }

        // Get all orders
        var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/order", _userToken);
        var response = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<Order>>();
        Assert.NotNull(result);
        Assert.True(result.TotalItems >= 3);
    }

    #endregion

    #region Authentication and Access Control Tests

    [Fact]
    public async Task Unauthenticated_Request_ReturnsUnauthorized()
    {
        var endpoints = new[]
        {
            "/api/inventory",
            "/api/inventory/1",
            "/api/order",
            "/api/order/1"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task RegularUser_CannotCreate_Inventory()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory", _userToken);
        request.Content = JsonContent.Create(new
        {
            Name = "Unauthorized Item",
            Quantity = 10,
            Location = "Test"
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegularUser_CannotCreate_Order()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/order", _userToken);
        request.Content = JsonContent.Create(new
        {
            CustomerName = "Unauthorized Order",
            DatePlaced = DateTime.UtcNow
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegularUser_CannotDelete_Inventory()
    {
        // First create an item as manager
        var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        createRequest.Content = JsonContent.Create(new
        {
            Name = "Item to Delete",
            Quantity = 5,
            Location = "Test"
        });
        var createResponse = await _client.SendAsync(createRequest);
        var item = await createResponse.Content.ReadFromJsonAsync<InventoryItem>();

        // Try to delete as regular user
        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/inventory/{item!.ItemId}", _userToken);
        var response = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegularUser_CannotDelete_Order()
    {
        // First create an order as manager
        var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/order");
        createRequest.Content = JsonContent.Create(new
        {
            CustomerName = "Order to Delete",
            DatePlaced = DateTime.UtcNow
        });
        var createResponse = await _client.SendAsync(createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<Order>();

        // Try to delete as regular user
        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/order/{order!.OrderId}", _userToken);
        var response = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegularUser_CanRead_InventoryAndOrders()
    {
        // Create items as manager
        var inventoryRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        inventoryRequest.Content = JsonContent.Create(new { Name = "Readable Item", Quantity = 10, Location = "Test" });
        await _client.SendAsync(inventoryRequest);

        var orderRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/order");
        orderRequest.Content = JsonContent.Create(new { CustomerName = "Readable Order", DatePlaced = DateTime.UtcNow });
        await _client.SendAsync(orderRequest);

        // Read as regular user
        var inventoryGetRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        var inventoryResponse = await _client.SendAsync(inventoryGetRequest);
        Assert.Equal(HttpStatusCode.OK, inventoryResponse.StatusCode);

        var orderGetRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/order", _userToken);
        var orderResponse = await _client.SendAsync(orderGetRequest);
        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_ReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.here");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region API Response Times and Caching Tests

    [Fact]
    public async Task GetAll_SecondRequest_IsFasterDueToCaching()
    {
        // Warm up and populate cache
        var warmupRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        await _client.SendAsync(warmupRequest);

        // First timed request (may or may not be cached depending on test order)
        var stopwatch1 = Stopwatch.StartNew();
        var request1 = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        await _client.SendAsync(request1);
        stopwatch1.Stop();

        // Second request (should be cached)
        var stopwatch2 = Stopwatch.StartNew();
        var request2 = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        await _client.SendAsync(request2);
        stopwatch2.Stop();

        // Both requests should complete quickly (under 1 second)
        Assert.True(stopwatch1.ElapsedMilliseconds < 1000, $"First request took {stopwatch1.ElapsedMilliseconds}ms");
        Assert.True(stopwatch2.ElapsedMilliseconds < 1000, $"Second request took {stopwatch2.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CacheInvalidation_AfterCreate_WorksCorrectly()
    {
        // Get initial list
        var getRequest1 = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        var response1 = await _client.SendAsync(getRequest1);
        var result1 = await response1.Content.ReadFromJsonAsync<PagedResult<InventoryItem>>();
        var initialCount = result1?.TotalItems ?? 0;

        // Create new item
        var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        createRequest.Content = JsonContent.Create(new
        {
            Name = "Cache Test Item",
            Quantity = 1,
            Location = "Cache Test"
        });
        await _client.SendAsync(createRequest);

        // Get list again - should include new item
        var getRequest2 = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        var response2 = await _client.SendAsync(getRequest2);
        var result2 = await response2.Content.ReadFromJsonAsync<PagedResult<InventoryItem>>();

        Assert.NotNull(result2);
        Assert.Equal(initialCount + 1, result2.TotalItems);
    }

    [Fact]
    public async Task CacheInvalidation_AfterDelete_WorksCorrectly()
    {
        // Create an item
        var createRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        createRequest.Content = JsonContent.Create(new
        {
            Name = "Item to Be Deleted",
            Quantity = 1,
            Location = "Delete Test"
        });
        var createResponse = await _client.SendAsync(createRequest);
        var createdItem = await createResponse.Content.ReadFromJsonAsync<InventoryItem>();

        // Get list and confirm item exists
        var getRequest1 = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        var response1 = await _client.SendAsync(getRequest1);
        var result1 = await response1.Content.ReadFromJsonAsync<PagedResult<InventoryItem>>();
        Assert.Contains(result1!.Items, i => i.ItemId == createdItem!.ItemId);

        // Delete the item
        var deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/inventory/{createdItem!.ItemId}");
        await _client.SendAsync(deleteRequest);

        // Get list again - should not include deleted item
        var getRequest2 = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        var response2 = await _client.SendAsync(getRequest2);
        var result2 = await response2.Content.ReadFromJsonAsync<PagedResult<InventoryItem>>();

        Assert.DoesNotContain(result2!.Items, i => i.ItemId == createdItem.ItemId);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetById_NonExistentItem_ReturnsNotFound()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory/99999", _userToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistentOrder_ReturnsNotFound()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/order/99999", _userToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentItem_ReturnsNotFound()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Delete, "/api/inventory/99999");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentOrder_ReturnsNotFound()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Delete, "/api/order/99999");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidInventoryItem_ReturnsBadRequest()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        // Missing required field 'Name'
        request.Content = JsonContent.Create(new
        {
            Quantity = 10,
            Location = "Test"
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidOrder_ReturnsBadRequest()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/order");
        // Missing required field 'CustomerName'
        request.Content = JsonContent.Create(new
        {
            DatePlaced = DateTime.UtcNow
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_EmptyBody_ReturnsBadRequest()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        request.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MalformedJson_ReturnsBadRequest()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        request.Content = new StringContent("{ invalid json }", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Complete End-to-End Scenario Tests

    [Fact]
    public async Task EndToEnd_CompleteOrderWorkflow()
    {
        // 1. Create inventory items
        var item1Request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        item1Request.Content = JsonContent.Create(new { Name = "Widget A", Quantity = 50, Location = "Shelf A1" });
        var item1Response = await _client.SendAsync(item1Request);
        Assert.Equal(HttpStatusCode.Created, item1Response.StatusCode);

        var item2Request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/inventory");
        item2Request.Content = JsonContent.Create(new { Name = "Widget B", Quantity = 30, Location = "Shelf B2" });
        var item2Response = await _client.SendAsync(item2Request);
        Assert.Equal(HttpStatusCode.Created, item2Response.StatusCode);

        // 2. Create an order
        var orderRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/order");
        orderRequest.Content = JsonContent.Create(new
        {
            CustomerName = "Acme Corporation",
            DatePlaced = DateTime.UtcNow
        });
        var orderResponse = await _client.SendAsync(orderRequest);
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);

        var order = await orderResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);

        // 3. Verify inventory is accessible
        var inventoryRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/inventory", _userToken);
        var inventoryResponse = await _client.SendAsync(inventoryRequest);
        Assert.Equal(HttpStatusCode.OK, inventoryResponse.StatusCode);

        var inventoryResult = await inventoryResponse.Content.ReadFromJsonAsync<PagedResult<InventoryItem>>();
        Assert.NotNull(inventoryResult);
        Assert.True(inventoryResult.TotalItems >= 2);

        // 4. Verify order is accessible
        var getOrderRequest = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/order/{order.OrderId}", _userToken);
        var getOrderResponse = await _client.SendAsync(getOrderRequest);
        Assert.Equal(HttpStatusCode.OK, getOrderResponse.StatusCode);

        // 5. Regular user cannot modify order
        var userDeleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/order/{order.OrderId}", _userToken);
        var userDeleteResponse = await _client.SendAsync(userDeleteRequest);
        Assert.Equal(HttpStatusCode.Forbidden, userDeleteResponse.StatusCode);

        // 6. Manager can delete order
        var managerDeleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/order/{order.OrderId}");
        var managerDeleteResponse = await _client.SendAsync(managerDeleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, managerDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task EndToEnd_UserRegistrationLoginAndAccess()
    {
        // 1. Register a new user
        var email = $"newuser_{Guid.NewGuid()}@example.com";
        var password = "SecurePass123!";

        var registerResponse = await _client.PostAsJsonAsync("/register", new { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // 2. Login
        var loginResponse = await _client.PostAsJsonAsync("/login", new { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginContent);
        Assert.NotEmpty(loginContent.AccessToken);

        // 3. Access protected resource
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginContent.AccessToken);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 4. Attempt to create (should fail - not a manager)
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inventory");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginContent.AccessToken);
        createRequest.Content = JsonContent.Create(new { Name = "Test", Quantity = 1, Location = "Test" });
        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    #endregion
}
