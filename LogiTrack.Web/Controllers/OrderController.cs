using System.Diagnostics;

using Asp.Versioning;

using FluentValidation;

using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;
using LogiTrack.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Web.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize(Policy = "ApiAccess")]
public class OrderController(
    IOrderRepository orderRepository,
    IMemoryCache cache,
    ILogger<OrderController> logger,
    IValidator<Order> validator) : ControllerBase
{
    private const string OrdersCacheKey = "orders_all";
    private const string OrderCacheKeyPrefix = "order_";
    private const string CacheVersionKey = "orders_cache_version";
    private static readonly TimeSpan CollectionCacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ItemCacheExpiration = TimeSpan.FromMinutes(10);

    [HttpGet]
    public async Task<ActionResult<PagedResult<Order>>> GetAll([FromQuery] PaginationQuery pagination)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheVersion = cache.GetOrCreate(CacheVersionKey, _ => 0);
        var cacheKey = $"{OrdersCacheKey}_v{cacheVersion}_page{pagination.Page}_size{pagination.PageSize}";
        var cacheHit = cache.TryGetValue(cacheKey, out _);

        var result = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetAbsoluteExpiration(CollectionCacheExpiration);
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(2));

            var allOrders = await orderRepository.GetAllAsync();
            var totalItems = allOrders.Count;
            var pagedOrders = allOrders
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            return new PagedResult<Order>
            {
                Items = pagedOrders,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalItems = totalItems
            };
        });

        stopwatch.Stop();

        logger.LogInformation(
            "GetAll orders completed in {ElapsedMs}ms (Cache {CacheStatus})",
            stopwatch.ElapsedMilliseconds,
            cacheHit ? "HIT" : "MISS");

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        var cacheKey = $"{OrderCacheKeyPrefix}{id}";

        var order = await cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.SetAbsoluteExpiration(ItemCacheExpiration);
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(3));
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
        var validationResult = await validator.ValidateAsync(order);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var created = await orderRepository.CreateAsync(order);

        InvalidateCollectionCache();

        logger.LogInformation("Orders cache invalidated after creating order {OrderId}", created.OrderId);

        return CreatedAtAction(nameof(GetById), new { id = created.OrderId }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Order>> Update(int id, Order order)
    {
        if (id != order.OrderId)
        {
            return Problem(
                title: "ID mismatch",
                detail: "The ID in the URL does not match the ID in the request body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var validationResult = await validator.ValidateAsync(order);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var updated = await orderRepository.UpdateAsync(order);

        if (updated is null)
        {
            return Problem(
                title: "Order not found",
                detail: $"Cannot update order. No order exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        InvalidateCollectionCache();
        cache.Remove($"{OrderCacheKeyPrefix}{id}");

        logger.LogInformation("Cache invalidated after updating order {OrderId}", id);

        return Ok(updated);
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

        InvalidateCollectionCache();
        cache.Remove($"{OrderCacheKeyPrefix}{id}");

        logger.LogInformation("Cache invalidated after deleting order {OrderId}", id);

        return NoContent();
    }

    private void InvalidateCollectionCache()
    {
        // Increment cache version to invalidate all paginated cache entries
        var currentVersion = cache.GetOrCreate(CacheVersionKey, _ => 0);
        cache.Set(CacheVersionKey, currentVersion + 1);
    }
}
