namespace DispatchSystem.Api.Dtos
{
    public class OrderListResponse
    {
        public List<OrderListItem> Items { get; set; } = new List<OrderListItem>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}