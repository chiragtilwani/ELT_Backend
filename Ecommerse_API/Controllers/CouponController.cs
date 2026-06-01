using Dapper;
using Ecommerce_API.Models;
using Ecommerce_API.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Ecommerce_API.Controllers
{
    [ApiController]
    [Route("api/coupon")]
    public class CouponController : ControllerBase
    {
        private readonly IConfiguration _config;

        public CouponController(IConfiguration config)
        {   
            _config = config;   
        }


        [HttpGet(Name ="GetAll")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Coupons>>> GetAll()
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            IEnumerable<Coupons> Coupons = await GetAllCoupons(connection);
            return Ok(Coupons);
        }

        private static async Task<IEnumerable<Coupons>> GetAllCoupons(SqlConnection connection)
        {
            return await connection.QueryAsync<Coupons>("select * from COUPONS");
        }

        [HttpGet("userId")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CouponDTO>>> GetAllCouponsByUserId(int userId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            await connection.OpenAsync();
            var AllCoupons = await connection.QueryAsync<Coupons>("select * from COUPONS");

            var couponsAvailableForUser = await Task.WhenAll(
                AllCoupons.Select(async coupon =>
                {
                    var isCouponAlreadyUsed = await connection.QueryFirstOrDefaultAsync<CouponUsed>(
                    "SELECT * FROM COUPON_USED WHERE Coupon_id = @Coupon_id AND User_id = @User_id",
                    new { Coupon_id = coupon.Id, User_id = userId });

                    return new { Coupon = coupon, IsCouponAlreadyUsed = isCouponAlreadyUsed };
                })
            );

            var validCoupons = couponsAvailableForUser
                .Where(result => result.IsCouponAlreadyUsed == null)
                .Select(result => result.Coupon)
                .ToList();
            connection.Close();
            return Ok(validCoupons);
        }

        [HttpGet("CouponId")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<Coupons>> GetAll(int CouponId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            connection.OpenAsync();
            var Coupon = await connection.QueryAsync<Coupons>("select * from COUPONS where Id = @Id",new { Id=CouponId});
            return Ok(Coupon);
        }

        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCoupon([FromBody] CouponDTO couponDTO) {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Id=@Id", new { Id = couponDTO.CurrentUserId });

            var coupons = await GetAllCoupons(connection);

            if (coupons.FirstOrDefault(coupon => coupon.CouponCode == couponDTO.CouponCode) != null) {
                ModelState.AddModelError("CouponCode", "Coupon already exist !");
                return BadRequest(ModelState);
            }
            
            if (user.isAdmin == 1)
            {
                await connection.QueryAsync("insert into COUPONS (CouponCode,DiscountPercent,MaxDiscount,CouponExpireDate) values (@CouponCode,@DiscountPercent,@MaxDiscount,@CouponExpireDate)", couponDTO);
                connection.Close();
                return Ok();
            }
            else {
                ModelState.AddModelError("Not an admin", "Only admin can CREATE the coupon !");
                return BadRequest(ModelState);
            }
        }

        [HttpDelete]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteCoupon(int CouponId,[FromBody]int CurrentUserId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Id=@Id", new { Id = CurrentUserId });
            if (user.isAdmin == 1)
            {
                await connection.QueryAsync("delete from COUPONS where Id=@Id",new { Id=CouponId});
                return Ok();
            }
            else
            {
                ModelState.AddModelError("Not an admin", "Only admin can DELETE the coupon !");
                return BadRequest(ModelState);
            }
        }
    }
}
