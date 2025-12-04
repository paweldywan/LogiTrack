using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogiTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ApiAccess")]
public class OrderController(IOrderRepository orderRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetAll()
    {
        var orders = await orderRepository.GetAllAsync();

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        var order = await orderRepository.GetByIdAsync(id);

        if (order is null)
            return Problem(
                title: "Order not found",
                detail: $"No order exists with ID {id}.",
                statusCode: StatusCodes.Status404NotFound);

        return Ok(order);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Order>> Create(Order order)
    {
        var created = await orderRepository.CreateAsync(order);

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

        return NoContent();
    }
}
