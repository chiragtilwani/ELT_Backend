using Dapper;
using Ecommerce_API.Models;
using Ecommerce_API.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Ecommerce_API.Controllers
{
    [Route("api/Product")]
    [ApiController]
    public class ProductController : ControllerBase
    {

        private readonly IConfiguration _config;

        public ProductController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("new")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Product>>> GetNewProducts()
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            //var products = await connection.QueryAsync<ProductDTO>("select * from PRODUCTS");
            IEnumerable<Product> products = await connection.QueryAsync<Product>("SELECT * FROM PRODUCTS WHERE CreatedAt >= DATEADD(DAY, -5, CAST(GETDATE() AS DATE))  AND CreatedAt < CAST(GETDATE() AS DATE)");
            return Ok(products);
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> GetProducts()
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            //var products = await connection.QueryAsync<ProductDTO>("select * from PRODUCTS");
            IEnumerable<Product> products = await SelectAllProducts(connection);
            return Ok(products);
        }



        [HttpGet("{id:int}", Name = "GetProduct")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductDTO>> GetProduct(int id)
        {
            if (id == 0)
            {
                return BadRequest();
            }

            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            ProductDTO product = await SelectSingleProduct(id, connection);

            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }



        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductDTO>> CreateProduct([FromBody] ProductDTO prod)
        {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var products = await SelectAllProducts(connection);

            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from Users where Id=@Id",new { Id=prod.CurrentUserId});
            if(user == null)
            {
                return BadRequest();
            }

            if (products.FirstOrDefault(p => p.Title.ToLower() == prod.Title.ToLower()) != null)
            {
                ModelState.AddModelError("Title", "Product Already Exist");
                return BadRequest(ModelState);
            }

            if (prod == null)
            {
                return BadRequest();
            }

            if (prod.Id > 0)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            prod.Id = products.OrderByDescending(p => p.Id).FirstOrDefault().Id + 1;
            
            if (user.isAdmin == 1)
            {
                await connection.ExecuteAsync("insert into PRODUCTS (Title,Price,Description,Image,Category,CreatedBy,Quantity) values (@Title,@Price,@Description,@Image,@Category,@CreatedBy,@Quantity)", prod);
                return CreatedAtRoute("GetProduct", new { id = prod.Id }, prod);
            }
            else {
                ModelState.AddModelError("Not an admin", "Only admin can CREATE a product !");
                return BadRequest(ModelState);
            }
        }

        [HttpDelete("{id:int}/{currentUserId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProduct(int id,int currentUserId)
        {
            if (id == 0)
            {
                return BadRequest();
            }
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var product = await SelectSingleProduct(id, connection);
            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from Users where Id=@Id", new { Id = currentUserId });
            //var product = ProductStore.productList.FirstOrDefault(p => p.Id == id);
            if (product == null) { return NotFound(); }
            if (user.isAdmin==1)
            {
                await connection.ExecuteAsync("Delete from PRODUCTS where Id=@Id", new { Id = product.Id });
                return NoContent();
            }
            else {
                ModelState.AddModelError("Not an admin", "Only admin can DELETE a product !");
                return BadRequest(ModelState);
            }
        }

        [HttpPut("{id:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductDTO productDTO)
        {
            if (productDTO == null || id != productDTO.Id)
            {
                return BadRequest();
            }

            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var product = await SelectSingleProduct(id, connection);
            var user = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Id=@Id", new { Id = productDTO.CurrentUserId });
            if (product == null) { return NotFound(); }

            if (user!=null && user.isAdmin==1)
            {
                await connection.ExecuteAsync("update PRODUCTS set Title=@Title,Price=@Price,Description=@Description,Image=@Image,Rating=@Rating,Category=@Category,Quantity=@Quantity where Id=@Id", productDTO);
                return NoContent();
            }
            else {
                ModelState.AddModelError("Not an admin", "Only admin can UPDATE a product !");
                return BadRequest(ModelState);
            }
        }

        [HttpPatch("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdatePartialProduct(int id, [FromBody] JsonPatchDocument<ProductDTO> productDTO)
        {
            if (productDTO == null || id == 0)
            {
                return BadRequest();
            }
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var product = await SelectSingleProduct(id, connection);
            if (product == null)
            {
                return BadRequest();
            }


            productDTO.ApplyTo(product, ModelState); 

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            try
            {
                await connection.ExecuteAsync(
                    "UPDATE PRODUCTS SET Title = @Title, Price = @Price, Description = @Description, Image = @Image,Rating=@Rating,Category=@Category,Quantity=@Quantity WHERE Id = @Id",
                    product
                );
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error Updating the product," + ex);
            }
        }

        private static async Task<IEnumerable<Product>> SelectAllProducts(SqlConnection connection)
        {
            return await connection.QueryAsync<Product>("select * from PRODUCTS");
        }

        private static async Task<ProductDTO> SelectSingleProduct(int id, SqlConnection connection)
        {
            return await connection.QueryFirstAsync<ProductDTO>("select * from PRODUCTS where Id=@Id", new { Id = id });
        }
    }
}