using LogiTrack.Domain.Models;

namespace LogiTrack.Data.Repositories;

public interface IOrderRepository
{
    Task<List<Order>> GetAllAsync();

    Task<Order?> GetByIdAsync(int id);

    Task<Order> CreateAsync(Order order);

    Task<Order?> UpdateAsync(Order order);

    Task<bool> DeleteAsync(int id);
}
