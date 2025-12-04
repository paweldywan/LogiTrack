using System.Diagnostics;

using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ApiAccess")]
public class OrderController(
    IOrderRepository orderRepository,
    IMemoryCache cache,
    ILogger<OrderController> logger) : ControllerBase
{
    private const string OrdersCacheKey = "orders_all";
    private const string OrderCacheKeyPrefix = "order_";

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetAll()
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheHit = cache.TryGetValue(OrdersCacheKey, out _);

        var orders = await cache.GetOrCreateAsync(OrdersCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);

            return orderRepository.GetAllAsync();
        });

        stopwatch.Stop();

        logger.LogInformation(
            "GetAll orders completed in {ElapsedMs}ms (Cache {CacheStatus})",
            stopwatch.ElapsedMilliseconds,
            cacheHit ? "HIT" : "MISS");

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        var cacheKey = $"{OrderCacheKeyPrefix}{id}";

        var order = await cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

            return orderRepository.GetByIdAsync(id);
        });

        if (order is null)
        {
            cache.Remove(cacheKey);

            return Problem(
                title: "Order not found",
                detail: $"No order exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(order);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Order>> Create(Order order)
    {
        var created = await orderRepository.CreateAsync(order);

        cache.Remove(OrdersCacheKey);

        logger.LogInformation("Orders cache invalidated after creating order {OrderId}", created.OrderId);

        return CreatedAtAction(nameof(GetById), new { id = created.OrderId }, created);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await orderRepository.DeleteAsync(id);

        if (!deleted)
            return Problem(
                title: "Order not found",
                detail: $"Cannot delete order. No order exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);

        cache.Remove(OrdersCacheKey);
        cache.Remove($"{OrderCacheKeyPrefix}{id}");

        logger.LogInformation("Cache invalidated after deleting order {OrderId}", id);

        return NoContent();
    }
}
