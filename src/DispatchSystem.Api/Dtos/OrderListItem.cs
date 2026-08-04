using DispatchSystem.Api.Models;

namespace DispatchSystem.Api.Dtos
{
    public class OrderListItem
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public int? RiderId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}