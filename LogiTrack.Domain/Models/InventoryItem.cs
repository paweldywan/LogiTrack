using System.Text.Json.Serialization;

namespace LogiTrack.Domain.Models;

public class InventoryItem : IEquatable<InventoryItem>, IAuditableEntity
{
    public int ItemId { get; set; }

    public required string Name { get; set; }

    public int Quantity { get; set; }

    public required string Location { get; set; }

    public int? OrderId { get; set; }

    [JsonIgnore]
    public Order? Order { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public override string ToString() => $"Item: {Name} | Quantity: {Quantity} | Location: {Location}";

    public bool Equals(InventoryItem? other) => other is not null && ItemId == other.ItemId;

    public override bool Equals(object? obj) => Equals(obj as InventoryItem);

    public override int GetHashCode() => ItemId.GetHashCode();
}
