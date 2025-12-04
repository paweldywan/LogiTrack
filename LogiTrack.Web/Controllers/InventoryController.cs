using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogiTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ApiAccess")]
public class InventoryController(IInventoryRepository inventoryRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAll()
    {
        var items = await inventoryRepository.GetAllAsync();

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItem>> GetById(int id)
    {
        var item = await inventoryRepository.GetByIdAsync(id);

        if (item is null)
            return Problem(
                title: "Inventory item not found",
                detail: $"No inventory item exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<InventoryItem>> Create(InventoryItem item)
    {
        var created = await inventoryRepository.CreateAsync(item);

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

        return NoContent();
    }
}
