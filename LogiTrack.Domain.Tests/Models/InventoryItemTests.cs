using LogiTrack.Domain.Models;

namespace LogiTrack.Domain.Tests.Models;

public class InventoryItemTests
{
    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var item = new InventoryItem
        {
            ItemId = 1,
            Name = "Pallet Jack",
            Quantity = 12,
            Location = "Warehouse A"
        };

        var result = item.ToString();

        Assert.Equal("Item: Pallet Jack | Quantity: 12 | Location: Warehouse A", result);
    }

    [Fact]
    public void Properties_SetCorrectly()
    {
        var item = new InventoryItem
        {
            ItemId = 5,
            Name = "Forklift",
            Quantity = 3,
            Location = "Warehouse B"
        };

        Assert.Equal(5, item.ItemId);

        Assert.Equal("Forklift", item.Name);

        Assert.Equal(3, item.Quantity);

        Assert.Equal("Warehouse B", item.Location);
    }
}
