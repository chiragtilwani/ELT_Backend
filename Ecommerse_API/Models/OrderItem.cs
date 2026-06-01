namespace Ecommerce_API.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int orderId { get; set; }
        public int productId { get; set; }
        public int quantity { get; set; }
        public float price { get; set; }
    }
}
