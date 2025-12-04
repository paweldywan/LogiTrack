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
public class InventoryController(
    IInventoryRepository inventoryRepository,
    IMemoryCache cache,
    ILogger<InventoryController> logger,
    IValidator<InventoryItem> validator) : ControllerBase
{
    private const string InventoryCacheKey = "inventory_all";
    private const string InventoryItemCacheKeyPrefix = "inventory_item_";
    private const string CacheVersionKey = "inventory_cache_version";
    private static readonly TimeSpan CollectionCacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ItemCacheExpiration = TimeSpan.FromMinutes(10);

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryItem>>> GetAll([FromQuery] PaginationQuery pagination)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheVersion = cache.GetOrCreate(CacheVersionKey, _ => 0);
        var cacheKey = $"{InventoryCacheKey}_v{cacheVersion}_page{pagination.Page}_size{pagination.PageSize}";
        var cacheHit = cache.TryGetValue(cacheKey, out _);

        var result = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetAbsoluteExpiration(CollectionCacheExpiration);
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(2));

            var allItems = await inventoryRepository.GetAllAsync();
            var totalItems = allItems.Count;
            var pagedItems = allItems
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            return new PagedResult<InventoryItem>
            {
                Items = pagedItems,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalItems = totalItems
            };
        });

        stopwatch.Stop();

        logger.LogInformation(
            "GetAll completed in {ElapsedMs}ms (Cache {CacheStatus})",
            stopwatch.ElapsedMilliseconds,
            cacheHit ? "HIT" : "MISS");

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItem>> GetById(int id)
    {
        var cacheKey = $"{InventoryItemCacheKeyPrefix}{id}";

        var item = await cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.SetAbsoluteExpiration(ItemCacheExpiration);
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(3));
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
        var validationResult = await validator.ValidateAsync(item);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var created = await inventoryRepository.CreateAsync(item);

        InvalidateCollectionCache();

        logger.LogInformation("Cache invalidated after creating item {ItemId}", created.ItemId);

        return CreatedAtAction(nameof(GetById), new { id = created.ItemId }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<InventoryItem>> Update(int id, InventoryItem item)
    {
        if (id != item.ItemId)
        {
            return Problem(
                title: "ID mismatch",
                detail: "The ID in the URL does not match the ID in the request body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var validationResult = await validator.ValidateAsync(item);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var updated = await inventoryRepository.UpdateAsync(item);

        if (updated is null)
        {
            return Problem(
                title: "Inventory item not found",
                detail: $"Cannot update item. No inventory item exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        InvalidateCollectionCache();
        cache.Remove($"{InventoryItemCacheKeyPrefix}{id}");

        logger.LogInformation("Cache invalidated after updating item {ItemId}", id);

        return Ok(updated);
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

        InvalidateCollectionCache();
        cache.Remove($"{InventoryItemCacheKeyPrefix}{id}");

        logger.LogInformation("Cache invalidated after deleting item {ItemId}", id);

        return NoContent();
    }

    private void InvalidateCollectionCache()
    {
        // Increment cache version to invalidate all paginated cache entries
        var currentVersion = cache.GetOrCreate(CacheVersionKey, _ => 0);
        cache.Set(CacheVersionKey, currentVersion + 1);
    }
}
