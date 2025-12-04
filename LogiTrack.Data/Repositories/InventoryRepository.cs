using LogiTrack.Domain.Models;

using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data.Repositories;

public class InventoryRepository(LogiTrackContext context) : IInventoryRepository
{
    public Task<List<InventoryItem>> GetAllAsync() => context.InventoryItems
        .AsNoTracking()
        .ToListAsync();

    public Task<InventoryItem?> GetByIdAsync(int id) => context.InventoryItems
        .AsNoTracking()
        .FirstOrDefaultAsync(i => i.ItemId == id);

    public async Task<InventoryItem> CreateAsync(InventoryItem item)
    {
        context.InventoryItems.Add(item);

        await context.SaveChangesAsync();

        return item;
    }

    public async Task<InventoryItem?> UpdateAsync(InventoryItem item)
    {
        var existing = await context.InventoryItems.FindAsync(item.ItemId);

        if (existing is null)
            return null;

        existing.Name = item.Name;
        existing.Quantity = item.Quantity;
        existing.Location = item.Location;
        existing.OrderId = item.OrderId;

        await context.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var item = await context.InventoryItems.FindAsync(id);

        if (item is null)
            return false;

        context.InventoryItems.Remove(item);

        await context.SaveChangesAsync();

        return true;
    }
}
