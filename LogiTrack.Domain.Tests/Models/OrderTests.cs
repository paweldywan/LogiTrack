using LogiTrack.Domain.Models;

namespace LogiTrack.Domain.Tests.Models;

public class OrderTests
{
    [Fact]
    public void AddItem_AddsItemToList()
    {
        var order = new Order
        {
            OrderId = 1001,
            CustomerName = "Samir",
            DatePlaced = new DateTime(2025, 4, 5)
        };

        var item = new InventoryItem
        {
            ItemId = 1,
            Name = "Pallet Jack",
            Quantity = 2,
            Location = "Warehouse A"
        };

        order.AddItem(item);

        Assert.Single(order.Items);

        Assert.Contains(item, order.Items);
    }

    [Fact]
    public void RemoveItem_RemovesItemById()
    {
        var order = new Order
        {
            OrderId = 1001,
            CustomerName = "Samir",
            DatePlaced = new DateTime(2025, 4, 5)
        };

        var item1 = new InventoryItem
        {
            ItemId = 1,
            Name = "Pallet Jack",
            Quantity = 2,
            Location = "Warehouse A"
        };

        var item2 = new InventoryItem
        {
            ItemId = 2,
            Name = "Forklift",
            Quantity = 1,
            Location = "Warehouse B"
        };

        order.AddItem(item1);

        order.AddItem(item2);

        order.RemoveItem(1);

        Assert.Single(order.Items);

        Assert.DoesNotContain(item1, order.Items);

        Assert.Contains(item2, order.Items);
    }

    [Fact]
    public void ToString_ReturnsFormattedSummary()
    {
        var order = new Order
        {
            OrderId = 1001,
            CustomerName = "Samir",
            DatePlaced = new DateTime(2025, 4, 5)
        };

        var item1 = new InventoryItem
        {
            ItemId = 1,
            Name = "Pallet Jack",
            Quantity = 2,
            Location = "Warehouse A"
        };

        var item2 = new InventoryItem
        {
            ItemId = 2,
            Name = "Forklift",
            Quantity = 1,
            Location = "Warehouse B"
        };

        order.AddItem(item1);

        order.AddItem(item2);

        var result = order.ToString();

        Assert.Equal("Order #1001 for Samir | Items: 2 | Placed: 4/5/2025", result);
    }

    [Fact]
    public void Properties_SetCorrectly()
    {
        var order = new Order
        {
            OrderId = 1001,
            CustomerName = "Samir",
            DatePlaced = new DateTime(2025, 4, 5)
        };

        Assert.Equal(1001, order.OrderId);

        Assert.Equal("Samir", order.CustomerName);

        Assert.Equal(new DateTime(2025, 4, 5), order.DatePlaced);

        Assert.Empty(order.Items);
    }
}
