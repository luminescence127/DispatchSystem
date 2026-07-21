using System.Text.Json.Serialization;

namespace DispatchSystem.Api.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;
        public string DropoffAddress { get; set; } = string.Empty;
        public OrderStatus Status { get; set; } = OrderStatus.Created;
        public int? RiderId { get; set; }
        
        [JsonIgnore]
        public Rider? Rider { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
