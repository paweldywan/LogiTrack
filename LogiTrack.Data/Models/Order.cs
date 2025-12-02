using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Data.Models;

public class Order
{
    private const int DefaultItemCapacity = 10;

    [Key]
    public int OrderId { get; set; }

    public required string CustomerName { get; set; }

    public DateTime DatePlaced { get; set; }

    public HashSet<InventoryItem> Items { get; set; } = new(DefaultItemCapacity);

    public void AddItem(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Items.Add(item);
    }

    public void RemoveItem(int itemId) => Items.RemoveWhere(i => i.ItemId == itemId);

    public override string ToString() => $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | Placed: {DatePlaced:M/d/yyyy}";
}
