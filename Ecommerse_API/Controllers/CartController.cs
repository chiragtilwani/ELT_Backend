using Dapper;
using Ecommerce_API.Models;
using Ecommerce_API.Models.DTO;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Ecommerce_API.Controllers
{
    [ApiController]
    [Route("api/cart")]
    public class CartController : ControllerBase
    {
        private readonly IConfiguration _config;
        public CartController(IConfiguration config)
        {
            _config = config;
        }
        [HttpGet("{userId}")]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CartResponseDTO>> GetCartByUserId(int userId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var user = await connection.QueryFirstOrDefaultAsync<Users>("SELECT * FROM USERS WHERE Id = @Id", new { Id = userId });

            if (user == null)
            {
                ModelState.AddModelError("error", "User not found!");
                return BadRequest(ModelState);
            }

            var cart = await connection.QueryAsync<Cart>("SELECT * FROM cart WHERE UserId = @UserId", new { UserId = userId });
            var totalCartPrice = await connection.ExecuteScalarAsync<decimal?>("SELECT SUM(CartPrice) FROM Cart WHERE UserId = @UserId", new { UserId = userId });
            var totalQty = await connection.ExecuteScalarAsync<int?>("SELECT SUM(Quantity) FROM Cart WHERE UserId = @UserId", new { UserId = userId });

            var cartResponse = new CartResponseDTO
            {
                Cart = cart.ToList(), 
                TotalCartPrice = totalCartPrice ?? 0 ,
                cartQuantity=totalQty ?? 0
            };

            return Ok(cartResponse);
        }



        [HttpDelete("{cartId}/{userId}")]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemoveItemFromCart(int cartId, int userId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var cartItem = await connection.QueryFirstOrDefaultAsync<Cart>("SELECT * FROM cart WHERE CartId = @Id", new { Id = cartId });

            if (cartItem == null)
            {
                ModelState.AddModelError("error", "Cart item not found!");
                return BadRequest(ModelState);
            }



            var user = await connection.QueryFirstOrDefaultAsync<Users>("SELECT * FROM USERS WHERE Id = @Id", new { Id = userId });

            if (user.Id == cartItem.UserId)
            {
                var product = await connection.QueryFirstOrDefaultAsync<ProductDTO>("select * from PRODUCTS where Id=@Id", new { Id = cartItem.ProductId });
                var qty = product.Quantity;
                await connection.ExecuteAsync("UPDATE PRODUCTS SET Quantity = @Quantity WHERE Id =@Id", new { Quantity = (qty + cartItem.Quantity), Id = cartItem.ProductId });
                await connection.ExecuteAsync("delete from cart where CartId=@CartId", new { CartId = cartId });
            }

            return NoContent();
        }

        [HttpDelete("clear/{userId}")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> clearCart(int userId)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var user = await connection.QueryFirstOrDefaultAsync("select * from USERS where Id=@Id", new { Id = userId });
            if (user == null)
            {
                ModelState.AddModelError("Error", "User not found !");
                return BadRequest(ModelState);
            }
            await connection.ExecuteAsync("delete from cart where UserId=@Id", new { Id = userId });
            return NoContent(); 
        }


        [HttpPost("addItem")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddItemToCart([FromBody] CartDTO cartDTO)
        {

            using var connection = new SqlConnection(_config.GetConnectionString("default"));

            var user = await connection.QueryFirstOrDefaultAsync("SELECT * FROM USERS WHERE Id = @Id", new { Id = cartDTO.UserId });
            if (user == null)
            {
                return BadRequest("User not found!");
            }

            var product = await connection.QueryFirstOrDefaultAsync<ProductDTO>("select * from PRODUCTS where Id=@Id", new { Id = cartDTO.ProductId });
            var qty = product.Quantity;
            await connection.ExecuteAsync("UPDATE PRODUCTS SET Quantity = @Quantity WHERE Id =@Id", new { Quantity = (qty - cartDTO.Quantity), Id = cartDTO.ProductId });

            await connection.ExecuteAsync("INSERT INTO cart (UserId, ProductId, Quantity, CartPrice) VALUES (@UserId, @ProductId, @Quantity, @CartPrice)", new { UserId = cartDTO.UserId, ProductId = cartDTO.ProductId, Quantity = cartDTO.Quantity, CartPrice = (product.Price * cartDTO.Quantity) });

            return Ok("Item added to cart successfully!");
        }
        [HttpPatch("{cartId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdatePartialCart(int cartId, [FromBody] JsonPatchDocument<CartDTO> cartDTO)
        {
            if (cartDTO == null || cartId == 0)
            {
                return BadRequest();
            }

            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var cartItem = await connection.QueryFirstOrDefaultAsync<CartDTO>("select * from cart where CartId=@CartId", new { CartId = cartId });
            var product = await connection.QueryFirstOrDefaultAsync<ProductDTO>("select * from PRODUCTS where Id=@Id", new { Id = cartItem.ProductId });
            int oldQty = cartItem.Quantity;
            if (cartItem == null) { return BadRequest(); }
            cartDTO.ApplyTo(cartItem, ModelState);
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }


            await connection.ExecuteAsync(
            "UPDATE cart SET Quantity=@Quantity,CartPrice=@CartPrice WHERE CartId = @CartId", new { Quantity = cartItem.Quantity, CartPrice = (cartItem.Quantity * product.Price), CartId = cartId });

            var updatedCartItem = await connection.QueryFirstOrDefaultAsync<CartDTO>("select * from cart where CartId=@CartId", new { CartId = cartId });
            int productQty;
            if (updatedCartItem.Quantity > oldQty)
            {
                productQty = updatedCartItem.Quantity - oldQty;
                await connection.ExecuteAsync("UPDATE PRODUCTS SET Quantity = @Quantity WHERE Id =@Id", new { Quantity = product.Quantity - productQty, Id = product.Id });
            }
            else
            {
                productQty = oldQty - updatedCartItem.Quantity;
                await connection.ExecuteAsync("UPDATE PRODUCTS SET Quantity = @Quantity WHERE Id =@Id", new { Quantity = product.Quantity + productQty, Id = product.Id });

            }
            return NoContent();
        }
    }
}