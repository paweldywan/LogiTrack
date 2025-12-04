namespace LogiTrack.Domain.Models;

public class Order : IAuditableEntity
{
    private const int DefaultItemCapacity = 10;

    public int OrderId { get; set; }

    public required string CustomerName { get; set; }

    public DateTime DatePlaced { get; set; }

    public HashSet<InventoryItem> Items { get; set; } = new(DefaultItemCapacity);

    // Audit fields
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public void AddItem(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Items.Add(item);
    }

    public void RemoveItem(int itemId) => Items.RemoveWhere(i => i.ItemId == itemId);

    public override string ToString() => $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | Placed: {DatePlaced.ToString("M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture)}";
}
