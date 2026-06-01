using Dapper;
using Ecommerce_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Ecommerce_API.Controllers
{
    [ApiController]
    [Route("api/wishlist")]
    public class WishlistController : ControllerBase
    {
        private readonly IConfiguration _config;
        public WishlistController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> addToWishlist([FromBody] Wishlist item) {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            if (item == null) { return BadRequest(); }

            await connection.ExecuteAsync("insert into WISHLIST(userId,productId) values (@UserId,@ProductId)", new { UserId = item.UserId, ProductId = item.ProductId });

            return Ok();
        }


        [HttpGet("{userId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<Product>>> getWishListByUserId(int userId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Id=@Id", new { Id = userId });
            if (user == null) { return BadRequest(); }

           var wishlist= await connection.QueryAsync<Wishlist>("select * from WISHLIST where UserId=@UserId", new { UserId = userId});

            return Ok(wishlist);
        }


        [HttpDelete("{userId:int}/{productId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> removeItemFromWishlist(int userId,int productId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var product = await connection.QueryFirstOrDefaultAsync<Users>("select * from PRODUCTS where Id=@Id", new { Id = productId });
            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Id=@Id", new { Id = userId });
            if (product == null || user==null) { return BadRequest(); }

            if (user.isAdmin == 1)
            {
                var itemInWishlist=await connection.QueryAsync<Wishlist>("select * from WISHLIST where ProductId=@ProductId", new { ProductId  = productId });
                if (itemInWishlist != null)
                {
                    await connection.ExecuteAsync("delete from WISHLIST where ProductId=@productId", new { productId = productId });
                }
            }

            await connection.ExecuteAsync("delete from WISHLIST where ProductId=@ProductId and UserId=@userId", new { ProductId = productId , userId = userId });

            return Ok();
        }

    }
}
