namespace Ecommerce_API.Models
{
    public class Wishlist
    {
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public DateTime createdAt { get; set; } = DateTime.Now;
    }
}
