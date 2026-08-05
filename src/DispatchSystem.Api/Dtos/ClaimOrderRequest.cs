using System.ComponentModel.DataAnnotations;

namespace DispatchSystem.Api.Dtos
{
    public class ClaimOrderRequest
    {
        [Range(1, int.MaxValue)]
        public int RiderId { get; set; }
    }
}