namespace LogiTrack.Models;

public class InventoryItem : IEquatable<InventoryItem>
{
    public int ItemId { get; set; }

    public required string Name { get; set; }

    public int Quantity { get; set; }

    public required string Location { get; set; }

    public int? OrderId { get; set; }

    public Order? Order { get; set; }

    public override string ToString() => $"Item: {Name} | Quantity: {Quantity} | Location: {Location}";

    public bool Equals(InventoryItem? other) => other is not null && ItemId == other.ItemId;

    public override bool Equals(object? obj) => Equals(obj as InventoryItem);

    public override int GetHashCode() => ItemId.GetHashCode();
}
