using LogiTrack.Domain.Models;

using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data.Repositories;

public class OrderRepository(LogiTrackContext context) : IOrderRepository
{
    public Task<List<Order>> GetAllAsync() => context.Orders
        .Include(o => o.Items)
        .AsNoTracking()
        .ToListAsync();

    public Task<Order?> GetByIdAsync(int id) => context.Orders
        .Include(o => o.Items)
        .AsNoTracking()
        .FirstOrDefaultAsync(o => o.OrderId == id);

    public async Task<Order> CreateAsync(Order order)
    {
        order.DatePlaced = DateTime.UtcNow;

        context.Orders.Add(order);

        await context.SaveChangesAsync();

        return order;
    }

    public async Task<Order?> UpdateAsync(Order order)
    {
        var existing = await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == order.OrderId);

        if (existing is null)
            return null;

        existing.CustomerName = order.CustomerName;
        // DatePlaced is not updated - it's immutable
        // Items collection would need separate handling for complex scenarios

        await context.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var order = await context.Orders.FindAsync(id);

        if (order is null)
            return false;

        context.Orders.Remove(order);

        await context.SaveChangesAsync();

        return true;
    }
}
