using Azure;
using Dapper;
using Ecommerce_API.Models;
using Ecommerce_API.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Ecommerce_API.Controllers
{
    [ApiController]
    [Route("api/order")]
    public class OrderController : ControllerBase
    {
        private readonly IConfiguration _config;

        public OrderController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateOder([FromBody] OrderDTO order)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var product = await connection.QueryFirstOrDefaultAsync<ProductDTO>("select * from PRODUCTS where Id=@Id", new { Id = order.productId });
            var customer = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Id=@Id", new { Id = order.customerId });


            if (order.quantity <= product.Quantity)
            {
                if (order.couponId > 0)
                {
                    var isCouponAlreadyUsed = await connection.QueryFirstOrDefaultAsync<Users>("select * from COUPON_USED where Coupon_id=@Coupon_id and User_id=@User_id", new { Coupon_id = order.couponId, User_id = order.customerId });
                    if (isCouponAlreadyUsed != null)
                    {
                        ModelState.AddModelError("Coupon", "This coupon code can only be applied once per user.");
                        return BadRequest(ModelState);
                    }

                    var couponUsing = await connection.QueryFirstOrDefaultAsync<Coupons>("select * from COUPONS where Id =@Id", new { Id = order.couponId });
                    var order_original_price = product.Price * order.quantity;
                    if (couponUsing != null && (order.original_price >= couponUsing.minAmountToApply))
                    {
                        var discountedAmount = ((order.original_price * couponUsing.DiscountPercent) / 100);

                        if (discountedAmount > couponUsing.MaxDiscount)
                        {
                            discountedAmount = couponUsing.MaxDiscount;
                        }

                        var priceAfterDiscount = order.original_price - discountedAmount;

                        await connection.ExecuteAsync("insert into ORDERS (customerId,productId,quantity,Shipping_Address,order_status,original_price,couponId,price_after_coupon) values (@customerId,@productId,@quantity,@Shipping_Address,@order_status,@original_price,@couponId,@price_after_coupon)", new { order.customerId, order.productId, order.quantity, Shipping_Address = customer.Address, order.order_status, order.original_price, order.couponId, price_after_coupon = priceAfterDiscount });
                        await connection.ExecuteAsync("insert into COUPON_USED (Coupon_id,User_id) values (@Coupon_id,@User_id)", new { Coupon_id = order.couponId, User_id = order.customerId });
                    }
                    else {
                        ModelState.AddModelError("minAmountToApply", "The selected coupon cannot be applied because the order total is below the minimum amount required for this coupon. Please add more items to meet the minimum purchase requirement.");
                        return BadRequest(ModelState);
                    }



                }
                else
                {
                    await connection.ExecuteAsync("insert into ORDERS (customerId,productId,quantity,Shipping_Address,order_status,original_price,couponId,price_after_coupon) values (@customerId,@productId,@quantity,@Shipping_Address,@order_status,@original_price,@couponId,@price_after_coupon)", new { order.customerId, order.productId, order.quantity, Shipping_Address = customer.Address, order.order_status, original_price = order.original_price, order.couponId, price_after_coupon = order.original_price });
                }

            }
            else
            {
                ModelState.AddModelError("Quantity", "Sorry, there is not enough stock available for the quantity you requested.");
                return BadRequest(ModelState);
            }

            return Ok();
        }

        [HttpGet("adminId")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Orders>>> GetAllOrders(int adminId) {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));

            var user = await connection.QueryFirstOrDefaultAsync("select * from USERS where Id=@Id", new { Id = adminId });

            if (user == null) {
                ModelState.AddModelError("user", "User not Found !");
                return BadRequest(ModelState);
            }

            if (user.isAdmin == 0) {
                ModelState.AddModelError("Not an admin", "Only admin can access this data");
                return BadRequest(ModelState);
            }

            var orders = await connection.QueryAsync<Orders>("select * from ORDERS");
            return Ok(orders);
        }


        [HttpGet("userId")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Orders>>> GetAllOrdersByUserId(int userId) {

            if (userId == 0)
            {
                return BadRequest();
            }

            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Id=@Id", new { Id = userId });

            if (user == null) {
                return NotFound();
            }

            var orders = await connection.QueryAsync<Orders>("select * from ORDERS where customerId=@customerId", new { customerId = userId });

            return Ok(orders);
        }

        //order update ---for order status

        [HttpPatch("{orderId:int}")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId,[FromBody] JsonPatchDocument<OrderDTO> orderDTO) {
            if (orderDTO == null || orderId == 0)
            {
                return BadRequest();
            }
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var order = await connection.QueryFirstOrDefaultAsync<OrderDTO>("select * from ORDERS where orderId=@orderId", new { orderId});
            if (order == null)
            {
                return BadRequest();
            }



            orderDTO.ApplyTo(order, ModelState);

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            try
            {
                await connection.ExecuteAsync(
                    "UPDATE ORDERS SET order_status = @order_status WHERE orderId = @Id",
                    new {order_status=order.order_status,Id=orderId}
                );
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error Updating the order status," + ex);
            }
        }
    }
}
