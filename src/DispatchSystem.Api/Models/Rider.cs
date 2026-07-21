using System.Text.Json.Serialization;

namespace DispatchSystem.Api.Models
{
    public class Rider
    {
        public int Id { get; set; }
        public string Name { get; set; }=string.Empty;
        public bool IsAvailable { get; set; }
        
        [JsonIgnore]
        public List<Order>? Orders { get; set; }
    }
}
