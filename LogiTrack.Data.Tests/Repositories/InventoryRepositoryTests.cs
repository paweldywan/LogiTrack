using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;

using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data.Tests.Repositories;

public class InventoryRepositoryTests
{
    private static LogiTrackContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<LogiTrackContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new LogiTrackContext(options);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new InventoryRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_WithItems_ReturnsAllItems()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.InventoryItems.AddRange(
            new InventoryItem { Name = "Pallet Jack", Quantity = 5, Location = "Warehouse A" },
            new InventoryItem { Name = "Forklift", Quantity = 2, Location = "Warehouse B" },
            new InventoryItem { Name = "Hand Truck", Quantity = 10, Location = "Warehouse A" }
        );

        await context.SaveChangesAsync();

        var repository = new InventoryRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingItem_ReturnsItem()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var item = new InventoryItem { Name = "Pallet Jack", Quantity = 5, Location = "Warehouse A" };

        context.InventoryItems.Add(item);

        await context.SaveChangesAsync();

        var repository = new InventoryRepository(context);

        // Act
        var result = await repository.GetByIdAsync(item.ItemId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pallet Jack", result.Name);
        Assert.Equal(5, result.Quantity);
        Assert.Equal("Warehouse A", result.Location);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingItem_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new InventoryRepository(context);

        // Act
        var result = await repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ValidItem_AddsItemToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new InventoryRepository(context);

        var item = new InventoryItem { Name = "Pallet Jack", Quantity = 5, Location = "Warehouse A" };

        // Act
        var result = await repository.CreateAsync(item);

        // Assert
        Assert.True(result.ItemId > 0);

        var savedItem = await context.InventoryItems.FindAsync(result.ItemId);

        Assert.NotNull(savedItem);
        Assert.Equal("Pallet Jack", savedItem.Name);
        Assert.Equal(5, savedItem.Quantity);
        Assert.Equal("Warehouse A", savedItem.Location);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedItem()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new InventoryRepository(context);

        var item = new InventoryItem { Name = "Forklift", Quantity = 2, Location = "Warehouse B" };

        // Act
        var result = await repository.CreateAsync(item);

        // Assert
        Assert.Same(item, result);
        Assert.Equal("Forklift", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_ExistingItem_SoftDeletesItemAndReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var item = new InventoryItem { Name = "Pallet Jack", Quantity = 5, Location = "Warehouse A" };

        context.InventoryItems.Add(item);

        await context.SaveChangesAsync();

        var repository = new InventoryRepository(context);

        // Act
        var result = await repository.DeleteAsync(item.ItemId);

        // Assert
        Assert.True(result);

        // FindAsync bypasses query filters, so check IsDeleted flag
        var deletedItem = await context.InventoryItems.FindAsync(item.ItemId);

        Assert.NotNull(deletedItem);
        Assert.True(deletedItem.IsDeleted);
        Assert.NotNull(deletedItem.DeletedAt);
        
        // Verify query filter excludes soft-deleted items
        var allItems = await context.InventoryItems.ToListAsync();
        Assert.DoesNotContain(allItems, i => i.ItemId == item.ItemId);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingItem_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new InventoryRepository(context);

        // Act
        var result = await repository.DeleteAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherItems()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var item1 = new InventoryItem { Name = "Pallet Jack", Quantity = 5, Location = "Warehouse A" };
        var item2 = new InventoryItem { Name = "Forklift", Quantity = 2, Location = "Warehouse B" };

        context.InventoryItems.AddRange(item1, item2);

        await context.SaveChangesAsync();

        var repository = new InventoryRepository(context);

        // Act
        await repository.DeleteAsync(item1.ItemId);

        // Assert
        var remainingItems = await context.InventoryItems.ToListAsync();

        Assert.Single(remainingItems);
        Assert.Equal("Forklift", remainingItems[0].Name);
    }
}
