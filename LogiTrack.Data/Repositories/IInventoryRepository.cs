using LogiTrack.Domain.Models;

namespace LogiTrack.Data.Repositories;

public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetAllAsync();

    ValueTask<InventoryItem?> GetByIdAsync(int id);

    Task<InventoryItem> CreateAsync(InventoryItem item);

    Task<bool> DeleteAsync(int id);
}
