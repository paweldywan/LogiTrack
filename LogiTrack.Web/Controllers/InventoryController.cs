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
public class InventoryController(
    IInventoryRepository inventoryRepository,
    IMemoryCache cache,
    ILogger<InventoryController> logger) : ControllerBase
{
    private const string InventoryCacheKey = "inventory_all";
    private const string InventoryItemCacheKeyPrefix = "inventory_item_";

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAll()
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheHit = cache.TryGetValue(InventoryCacheKey, out _);

        var items = await cache.GetOrCreateAsync(InventoryCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);

            return inventoryRepository.GetAllAsync();
        });

        stopwatch.Stop();

        logger.LogInformation(
            "GetAll completed in {ElapsedMs}ms (Cache {CacheStatus})",
            stopwatch.ElapsedMilliseconds,
            cacheHit ? "HIT" : "MISS");

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItem>> GetById(int id)
    {
        var cacheKey = $"{InventoryItemCacheKeyPrefix}{id}";

        var item = await cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

            return inventoryRepository.GetByIdAsync(id);
        });

        if (item is null)
        {
            cache.Remove(cacheKey);

            return Problem(
                title: "Inventory item not found",
                detail: $"No inventory item exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<InventoryItem>> Create(InventoryItem item)
    {
        var created = await inventoryRepository.CreateAsync(item);

        cache.Remove(InventoryCacheKey);

        logger.LogInformation("Cache invalidated after creating item {ItemId}", created.ItemId);

        return CreatedAtAction(nameof(GetById), new { id = created.ItemId }, created);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await inventoryRepository.DeleteAsync(id);

        if (!deleted)
            return Problem(
                title: "Inventory item not found",
                detail: $"Cannot delete item. No inventory item exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);

        cache.Remove(InventoryCacheKey);
        cache.Remove($"{InventoryItemCacheKeyPrefix}{id}");

        logger.LogInformation("Cache invalidated after deleting item {ItemId}", id);

        return NoContent();
    }
}
