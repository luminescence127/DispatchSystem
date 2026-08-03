using DispatchSystem.Api.Data;
using DispatchSystem.Api.Dtos;
using DispatchSystem.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DispatchSystem.Api.Controllers
{

    [ApiController]
    [Route("api/orders")]
    public class OrdersController(DispatchDbContext db) : ControllerBase
    {
        [HttpGet("{id:int}")]
        [ProducesResponseType<Order>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            var order = await db.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(o => o.Id == id);

            if (order is null) return NotFound();

            return Ok(order);
        }

        [HttpPost]
        [ProducesResponseType<Order>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Order>> CreateOrder(CreateOrderRequest request)
        {
            var order = new Order
            {
                CustomerName = request.CustomerName,
                PickupAddress = request.PickupAddress,
                DropoffAddress = request.DropoffAddress,
                Status = OrderStatus.Created,
                CreatedAt = DateTime.UtcNow,
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            return Created($"/api/orders/{order.Id}", order);
        }

        [HttpPost("{id:int}/assign")]
        [ProducesResponseType<Order>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<Order>> AssignOrder(int id)
        {
            var order = await db.Orders.FindAsync(id);

            //訂單是否存在
            if (order is null) return NotFound();

            //訂單狀態是否為新建
            if (order.Status != OrderStatus.Created)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "訂單狀態無法更新",
                    detail: $"訂單目前狀態為 {order.Status}，只有狀態為 {OrderStatus.Created} 的訂單可以進行指派。"
                );
            }

            //找線上任一外送員
            var rider = await db.Riders
                .Where(r => r.IsAvailable)
                .FirstOrDefaultAsync();

            //是否有外送員
            if (rider is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "訂單狀態無法更新",
                    detail: "目前沒有可接單的外送員。"
                );
            }

            order.RiderId = rider.Id;
            order.Status = OrderStatus.Assigned;

            await db.SaveChangesAsync();

            return Ok(order);
        }

        [HttpPost("{id:int}/accept")]
        [ProducesResponseType<Order>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<Order>> AcceptOrder(int id)
        {
            var order = await db.Orders.FindAsync(id);

            if (order is null) return NotFound();

            if (order.Status != OrderStatus.Assigned)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "訂單狀態無法更新",
                    detail: $"訂單目前狀態為 {order.Status}，只有狀態為 {OrderStatus.Assigned} 的訂單可以進行接受。"
                );
            }

            order.Status = OrderStatus.Accepted;
            await db.SaveChangesAsync();

            return Ok(order);
        }

        [HttpPost("{id:int}/complete")]
        [ProducesResponseType<Order>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<Order>> CompleteOrder(int id)
        {
            var order = await db.Orders.FindAsync(id);

            if (order is null) return NotFound();

            if (order.Status != OrderStatus.Accepted)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "訂單狀態無法更新",
                    detail: $"訂單目前狀態為 {order.Status}，只有狀態為{OrderStatus.Accepted} 的訂單可以進行完成。"
                );
            }

            order.Status = OrderStatus.Completed;
            await db.SaveChangesAsync();

            return Ok(order);
        }
    }
}