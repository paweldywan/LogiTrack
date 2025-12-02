using LogiTrack.Data;
using LogiTrack.Models;

using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Tests;

public class DbSeederTests
{
    private static LogiTrackContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<LogiTrackContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new LogiTrackContext(options);
    }

    [Fact]
    public async Task SeedAsync_EmptyDatabase_AddsInventoryItem()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var seeder = new DbSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        Assert.Single(context.InventoryItems);

        var item = await context.InventoryItems.FirstAsync();

        Assert.Equal("Pallet Jack", item.Name);

        Assert.Equal(12, item.Quantity);

        Assert.Equal("Warehouse A", item.Location);
    }

    [Fact]
    public async Task SeedAsync_DatabaseAlreadySeeded_DoesNotAddDuplicates()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.InventoryItems.Add(new InventoryItem
        {
            Name = "Existing Item",
            Quantity = 5,
            Location = "Warehouse B"
        });

        await context.SaveChangesAsync();

        var seeder = new DbSeeder(context);

        // Act
        await seeder.SeedAsync();

        // Assert
        Assert.Single(context.InventoryItems);

        var item = await context.InventoryItems.FirstAsync();

        Assert.Equal("Existing Item", item.Name);
    }

    [Fact]
    public async Task SeedAsync_CalledMultipleTimes_OnlySeedsOnce()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var seeder = new DbSeeder(context);

        // Act
        await seeder.SeedAsync();

        await seeder.SeedAsync();

        await seeder.SeedAsync();

        // Assert
        Assert.Single(context.InventoryItems);
    }
}
