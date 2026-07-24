using System.ComponentModel.DataAnnotations;

namespace DispatchSystem.Api.Dtos
{
    public class CreateOrderRequest
    {
        [Required]
        [StringLength(50)]
        public string CustomerName { get; set; } =string.Empty;

        [Required]
        [StringLength(200)]
        public string PickupAddress { get; set; }=string.Empty;

        [Required]
        [StringLength(200)]
        public string DropoffAddress { get; set; } = string.Empty;
    }
}