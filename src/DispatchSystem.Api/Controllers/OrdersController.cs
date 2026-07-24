using DispatchSystem.Api.Data;
using DispatchSystem.Api.Dtos;
using DispatchSystem.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DispatchSystem.Api.Controllers
{

    [ApiController]
    [Route("api/orders")]
    public class OrdersController(DispatchDbContext db) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult> CreateOrder(CreateOrderRequest request)
        {
            var order = new Order
            {
                CustomerName = request.CustomerName,
                PickupAddress = request.PickupAddress,
                DropoffAddress = request.DropoffAddress,
                Status=OrderStatus.Created,
                CreatedAt = DateTime.UtcNow,
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            return Created($"/api/orders/{order.Id}", order);
        }
    }
}
