using LogiTrack.Data.Repositories;
using LogiTrack.Domain.Models;
using LogiTrack.Web.Controllers;

using Microsoft.AspNetCore.Mvc;

using NSubstitute;

namespace LogiTrack.Web.Tests.Controllers;

public class OrderControllerTests
{
    private readonly IOrderRepository _repository;
    private readonly OrderController _controller;

    public OrderControllerTests()
    {
        _repository = Substitute.For<IOrderRepository>();
        _controller = new OrderController(_repository);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithOrders()
    {
        // Arrange
        var orders = new List<Order>
        {
            new() { OrderId = 1, CustomerName = "Customer1", DatePlaced = DateTime.UtcNow },
            new() { OrderId = 2, CustomerName = "Customer2", DatePlaced = DateTime.UtcNow }
        };
        _repository.GetAllAsync().Returns(orders);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedOrders = Assert.IsAssignableFrom<IEnumerable<Order>>(okResult.Value);
        Assert.Equal(2, returnedOrders.Count());
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoOrders()
    {
        // Arrange
        _repository.GetAllAsync().Returns(new List<Order>());

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedOrders = Assert.IsAssignableFrom<IEnumerable<Order>>(okResult.Value);
        Assert.Empty(returnedOrders);
    }

    [Fact]
    public async Task GetById_ReturnsOkWithOrder_WhenOrderExists()
    {
        // Arrange
        var order = new Order { OrderId = 1, CustomerName = "Customer1", DatePlaced = DateTime.UtcNow };
        _repository.GetByIdAsync(1).Returns(order);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedOrder = Assert.IsType<Order>(okResult.Value);
        Assert.Equal(1, returnedOrder.OrderId);
        Assert.Equal("Customer1", returnedOrder.CustomerName);
    }

    [Fact]
    public async Task GetById_ReturnsProblem_WhenOrderNotFound()
    {
        // Arrange
        _repository.GetByIdAsync(999).Returns((Order?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WithCreatedOrder()
    {
        // Arrange
        var datePlaced = DateTime.UtcNow;
        var newOrder = new Order { CustomerName = "NewCustomer", DatePlaced = datePlaced };
        var createdOrder = new Order { OrderId = 1, CustomerName = "NewCustomer", DatePlaced = datePlaced };
        _repository.CreateAsync(newOrder).Returns(createdOrder);

        // Act
        var result = await _controller.Create(newOrder);

        // Assert
        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(OrderController.GetById), createdAtResult.ActionName);
        Assert.Equal(1, createdAtResult.RouteValues?["id"]);
        var returnedOrder = Assert.IsType<Order>(createdAtResult.Value);
        Assert.Equal(1, returnedOrder.OrderId);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenOrderDeleted()
    {
        // Arrange
        _repository.DeleteAsync(1).Returns(true);

        // Act
        var result = await _controller.Delete(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsProblem_WhenOrderNotFound()
    {
        // Arrange
        _repository.DeleteAsync(999).Returns(false);

        // Act
        var result = await _controller.Delete(999);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsOrderWithItems_WhenOrderHasItems()
    {
        // Arrange
        var order = new Order { OrderId = 1, CustomerName = "Customer1", DatePlaced = DateTime.UtcNow };
        order.AddItem(new InventoryItem { ItemId = 1, Name = "Item1", Quantity = 5, Location = "A1" });
        order.AddItem(new InventoryItem { ItemId = 2, Name = "Item2", Quantity = 10, Location = "B2" });
        _repository.GetByIdAsync(1).Returns(order);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedOrder = Assert.IsType<Order>(okResult.Value);
        Assert.Equal(2, returnedOrder.Items.Count);
    }
}
