using LogiTrack.Domain.Models;

namespace LogiTrack.Data.Repositories;

public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetAllAsync();

    Task<InventoryItem?> GetByIdAsync(int id);

    Task<InventoryItem> CreateAsync(InventoryItem item);

    Task<InventoryItem?> UpdateAsync(InventoryItem item);

    Task<bool> DeleteAsync(int id);
}
