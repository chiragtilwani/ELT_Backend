using Ecommerce_API.Models;

namespace Ecommerce_API.Models
{
    public class Orders
    {
        public int orderId { get; set; }
        public int customerId { get; set; }
        public int productId { get; set; }
        //public int quantity { get; set; }
        public DateTime orderDate{ get; set; }= DateTime.Now;
        public string Shipping_Address { get; set; }
        public string order_status { get; set; }
        public float original_price { get; set; }
        public int  couponId { get; set; }
        public float price_after_coupon { get; set; }
        //public int Quantity { get; set; }
    }
}
