using System;

namespace Ecommerce_API.Models
{
    public class Coupons
    {
        public int Id { get; set; }
        public string CouponCode { get; set; }
        public int DiscountPercent { get; set; }
        public int MaxDiscount { get; set; }
        public int minAmountToApply { get; set; }
        public DateTime CouponExpireDate { get; set; }
    }
}
