using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;

using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data.Tests.Repositories;

public class OrderRepositoryTests
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
        var repository = new OrderRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_WithOrders_ReturnsAllOrders()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.Orders.AddRange(
            new Order { CustomerName = "Alice", DatePlaced = DateTime.UtcNow },
            new Order { CustomerName = "Bob", DatePlaced = DateTime.UtcNow },
            new Order { CustomerName = "Charlie", DatePlaced = DateTime.UtcNow }
        );

        await context.SaveChangesAsync();

        var repository = new OrderRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingOrder_ReturnsOrder()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var order = new Order { CustomerName = "Alice", DatePlaced = DateTime.UtcNow };

        context.Orders.Add(order);

        await context.SaveChangesAsync();

        var repository = new OrderRepository(context);

        // Act
        var result = await repository.GetByIdAsync(order.OrderId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alice", result.CustomerName);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingOrder_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new OrderRepository(context);

        // Act
        var result = await repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesItems()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<LogiTrackContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        int orderId;

        // Seed data with one context
        using (var seedContext = new LogiTrackContext(options))
        {
            var order = new Order { CustomerName = "Alice", DatePlaced = DateTime.UtcNow };

            seedContext.Orders.Add(order);

            var item1 = new InventoryItem { Name = "Pallet Jack", Quantity = 2, Location = "Warehouse A", OrderId = 1 };
            var item2 = new InventoryItem { Name = "Forklift", Quantity = 1, Location = "Warehouse B", OrderId = 1 };

            seedContext.InventoryItems.AddRange(item1, item2);

            await seedContext.SaveChangesAsync();

            orderId = order.OrderId;
        }

        // Use a new context to ensure Include is working (not just tracking)
        using var context = new LogiTrackContext(options);

        var repository = new OrderRepository(context);

        // Act
        var result = await repository.GetByIdAsync(orderId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task CreateAsync_ValidOrder_AddsOrderToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new OrderRepository(context);

        var order = new Order { CustomerName = "Alice" };

        // Act
        var result = await repository.CreateAsync(order);

        // Assert
        Assert.True(result.OrderId > 0);

        var savedOrder = await context.Orders.FindAsync(result.OrderId);

        Assert.NotNull(savedOrder);
        Assert.Equal("Alice", savedOrder.CustomerName);
    }

    [Fact]
    public async Task CreateAsync_SetsDatePlacedToUtcNow()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new OrderRepository(context);

        var beforeCreate = DateTime.UtcNow;

        var order = new Order { CustomerName = "Alice" };

        // Act
        var result = await repository.CreateAsync(order);

        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.InRange(result.DatePlaced, beforeCreate, afterCreate);
    }

    [Fact]
    public async Task CreateAsync_OverwritesExistingDatePlaced()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new OrderRepository(context);

        var oldDate = new DateTime(2020, 1, 1);
        var order = new Order { CustomerName = "Alice", DatePlaced = oldDate };

        // Act
        var result = await repository.CreateAsync(order);

        // Assert
        Assert.NotEqual(oldDate, result.DatePlaced);
        Assert.True(result.DatePlaced > oldDate);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedOrder()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new OrderRepository(context);

        var order = new Order { CustomerName = "Bob" };

        // Act
        var result = await repository.CreateAsync(order);

        // Assert
        Assert.Same(order, result);
        Assert.Equal("Bob", result.CustomerName);
    }

    [Fact]
    public async Task DeleteAsync_ExistingOrder_SoftDeletesOrderAndReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var order = new Order { CustomerName = "Alice", DatePlaced = DateTime.UtcNow };

        context.Orders.Add(order);

        await context.SaveChangesAsync();

        var repository = new OrderRepository(context);

        // Act
        var result = await repository.DeleteAsync(order.OrderId);

        // Assert
        Assert.True(result);

        // FindAsync bypasses query filters, so check IsDeleted flag
        var deletedOrder = await context.Orders.FindAsync(order.OrderId);

        Assert.NotNull(deletedOrder);
        Assert.True(deletedOrder.IsDeleted);
        Assert.NotNull(deletedOrder.DeletedAt);
        
        // Verify query filter excludes soft-deleted orders
        var allOrders = await context.Orders.ToListAsync();
        Assert.DoesNotContain(allOrders, o => o.OrderId == order.OrderId);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingOrder_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new OrderRepository(context);

        // Act
        var result = await repository.DeleteAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherOrders()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var order1 = new Order { CustomerName = "Alice", DatePlaced = DateTime.UtcNow };
        var order2 = new Order { CustomerName = "Bob", DatePlaced = DateTime.UtcNow };

        context.Orders.AddRange(order1, order2);

        await context.SaveChangesAsync();

        var repository = new OrderRepository(context);

        // Act
        await repository.DeleteAsync(order1.OrderId);

        // Assert
        var remainingOrders = await context.Orders.ToListAsync();

        Assert.Single(remainingOrders);
        Assert.Equal("Bob", remainingOrders[0].CustomerName);
    }
}
