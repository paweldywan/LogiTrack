using FluentValidation;
using FluentValidation.Results;

using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;
using LogiTrack.Web.Controllers;
using LogiTrack.Web.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using NSubstitute;

namespace LogiTrack.Web.Tests.Controllers;

public class InventoryControllerTests
{
    private readonly IInventoryRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InventoryController> _logger;
    private readonly IValidator<InventoryItem> _validator;
    private readonly InventoryController _controller;
    private readonly PaginationQuery _defaultPagination = new();

    public InventoryControllerTests()
    {
        _repository = Substitute.For<IInventoryRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<InventoryController>>();
        _validator = Substitute.For<IValidator<InventoryItem>>();
        _validator.ValidateAsync(Arg.Any<InventoryItem>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _controller = new InventoryController(_repository, _cache, _logger, _validator);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithItems()
    {
        // Arrange
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Name = "Item1", Quantity = 10, Location = "A1" },
            new() { ItemId = 2, Name = "Item2", Quantity = 20, Location = "B2" }
        };
        _repository.GetAllAsync().Returns(items);

        // Act
        var result = await _controller.GetAll(_defaultPagination);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pagedResult = Assert.IsType<PagedResult<InventoryItem>>(okResult.Value);
        Assert.Equal(2, pagedResult.Items.Count());
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoItems()
    {
        // Arrange
        _repository.GetAllAsync().Returns([]);

        // Act
        var result = await _controller.GetAll(_defaultPagination);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pagedResult = Assert.IsType<PagedResult<InventoryItem>>(okResult.Value);
        Assert.Empty(pagedResult.Items);
    }

    [Fact]
    public async Task GetAll_CachesResult_OnFirstCall()
    {
        // Arrange
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Name = "Item1", Quantity = 10, Location = "A1" }
        };
        _repository.GetAllAsync().Returns(items);

        // Act
        await _controller.GetAll(_defaultPagination);

        // Assert - Cache should contain the items with version key
        var version = _cache.Get<int>("inventory_cache_version");
        Assert.True(_cache.TryGetValue($"inventory_all_v{version}_page1_size20", out var cachedItems));
        Assert.NotNull(cachedItems);
    }

    [Fact]
    public async Task GetAll_ReturnsCachedResult_OnSubsequentCalls()
    {
        // Arrange
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Name = "Item1", Quantity = 10, Location = "A1" }
        };
        _repository.GetAllAsync().Returns(items);

        // Act - Call twice
        await _controller.GetAll(_defaultPagination);
        await _controller.GetAll(_defaultPagination);

        // Assert - Repository should only be called once (second call uses cache)
        await _repository.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task GetById_ReturnsOkWithItem_WhenItemExists()
    {
        // Arrange
        var item = new InventoryItem { ItemId = 1, Name = "Item1", Quantity = 10, Location = "A1" };
        _repository.GetByIdAsync(1).Returns(item);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedItem = Assert.IsType<InventoryItem>(okResult.Value);
        Assert.Equal(1, returnedItem.ItemId);
        Assert.Equal("Item1", returnedItem.Name);
    }

    [Fact]
    public async Task GetById_ReturnsProblem_WhenItemNotFound()
    {
        // Arrange
        _repository.GetByIdAsync(999).Returns((InventoryItem?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WithCreatedItem()
    {
        // Arrange
        var newItem = new InventoryItem { Name = "NewItem", Quantity = 5, Location = "C3" };
        var createdItem = new InventoryItem { ItemId = 1, Name = "NewItem", Quantity = 5, Location = "C3" };
        _repository.CreateAsync(newItem).Returns(createdItem);

        // Act
        var result = await _controller.Create(newItem);

        // Assert
        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(InventoryController.GetById), createdAtResult.ActionName);
        Assert.Equal(1, createdAtResult.RouteValues?["id"]);
        var returnedItem = Assert.IsType<InventoryItem>(createdAtResult.Value);
        Assert.Equal(1, returnedItem.ItemId);
    }

    [Fact]
    public async Task Create_InvalidatesCache()
    {
        // Arrange
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Name = "Item1", Quantity = 10, Location = "A1" }
        };
        _repository.GetAllAsync().Returns(items);

        var newItem = new InventoryItem { Name = "NewItem", Quantity = 5, Location = "C3" };
        var createdItem = new InventoryItem { ItemId = 2, Name = "NewItem", Quantity = 5, Location = "C3" };
        _repository.CreateAsync(newItem).Returns(createdItem);

        // Populate cache - version starts at 0
        await _controller.GetAll(_defaultPagination);
        var initialVersion = _cache.Get<int>("inventory_cache_version");
        Assert.True(_cache.TryGetValue($"inventory_all_v{initialVersion}_page1_size20", out _));

        // Act
        await _controller.Create(newItem);

        // Assert - Cache version should be incremented
        var newVersion = _cache.Get<int>("inventory_cache_version");
        Assert.True(newVersion > initialVersion);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenItemDeleted()
    {
        // Arrange
        _repository.DeleteAsync(1).Returns(true);

        // Act
        var result = await _controller.Delete(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsProblem_WhenItemNotFound()
    {
        // Arrange
        _repository.DeleteAsync(999).Returns(false);

        // Act
        var result = await _controller.Delete(999);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task Delete_InvalidatesCache_WhenItemDeleted()
    {
        // Arrange
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Name = "Item1", Quantity = 10, Location = "A1" }
        };
        _repository.GetAllAsync().Returns(items);
        _repository.DeleteAsync(1).Returns(true);

        // Populate cache
        await _controller.GetAll(_defaultPagination);
        var initialVersion = _cache.Get<int>("inventory_cache_version");
        Assert.True(_cache.TryGetValue($"inventory_all_v{initialVersion}_page1_size20", out _));

        // Act
        await _controller.Delete(1);

        // Assert - Cache version should be incremented
        var newVersion = _cache.Get<int>("inventory_cache_version");
        Assert.True(newVersion > initialVersion);
    }

    [Fact]
    public async Task Delete_DoesNotInvalidateCache_WhenItemNotFound()
    {
        // Arrange
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Name = "Item1", Quantity = 10, Location = "A1" }
        };
        _repository.GetAllAsync().Returns(items);
        _repository.DeleteAsync(999).Returns(false);

        // Populate cache
        await _controller.GetAll(_defaultPagination);
        var initialVersion = _cache.Get<int>("inventory_cache_version");
        Assert.True(_cache.TryGetValue($"inventory_all_v{initialVersion}_page1_size20", out _));

        // Act
        await _controller.Delete(999);

        // Assert - Cache version should remain the same (delete failed)
        var newVersion = _cache.Get<int>("inventory_cache_version");
        Assert.Equal(initialVersion, newVersion);
    }
}
