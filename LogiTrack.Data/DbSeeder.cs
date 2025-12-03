using LogiTrack.Domain.Models;

using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data;

public class DbSeeder(LogiTrackContext context)
{
    public async Task SeedAsync()
    {
        if (await context.InventoryItems.AnyAsync())
            return;

        context.InventoryItems.Add(new InventoryItem
        {
            Name = "Pallet Jack",
            Quantity = 12,
            Location = "Warehouse A"
        });

        await context.SaveChangesAsync();
    }
}
