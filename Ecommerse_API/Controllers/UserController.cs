using Dapper;
using Ecommerce_API.Models;
using Ecommerce_API.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ecommerce_API.Controllers
{
    [Route("api/User")]
    [ApiController]
    public class UserController:ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string secretKey;

        public UserController(IConfiguration config)
        {
            _config = config;
            secretKey = _config.GetValue<string>("ApiSettings:Secret");
        }

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Users>> RegisterUser([FromBody]ResgisterationRequestDTO newUser) {
            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            var existingUser = await connection.QueryFirstOrDefaultAsync<Users>("Select * from USERS where Email = @Email", new { Email = newUser.Email });
            if (existingUser == null)
            {
                await connection.ExecuteAsync("insert into USERS (Name,Email,Password,Address,isAdmin) values (@Name,@Email,@Password,@Address,@isAdmin)", newUser);
                newUser.Password = "";
                var NewUser = await connection.QueryFirstOrDefaultAsync<Users>("select * from USERS where Email=@Email", new { Email = newUser.Email });

                var key = Encoding.ASCII.GetBytes(secretKey);
                var tokenHandler = new JwtSecurityTokenHandler();

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new Claim[]
                    {
                new Claim(ClaimTypes.PrimarySid, NewUser.Id.ToString()),
                new Claim(ClaimTypes.Email, NewUser.Email)
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var Token = tokenHandler.CreateToken(tokenDescriptor);

                LoginResponseDTO loginResponseDTO = new LoginResponseDTO()
                {
                    token = tokenHandler.WriteToken(Token),
                    user = NewUser
                };

                return Ok(loginResponseDTO);
            }
            else {
                ModelState.AddModelError("Email", "User with this Email already exist");
                return BadRequest(ModelState);
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LoginResponseDTO>> LoginUser([FromBody]LoginRequestDTO user) {

            using var connection = new SqlConnection(_config.GetConnectionString("default"));
            
            var existingUser = await connection.QueryFirstOrDefaultAsync<Users>("Select * from USERS where Email = @Email and Password=@Password", new {Email=user.Email,Password=user.Password } );

            if (existingUser == null)
            {
                ModelState.AddModelError("login", "Invalid Email or Password !");
                return NotFound(ModelState);
            }
                
            var key = Encoding.ASCII.GetBytes(secretKey);
            var tokenHandler = new JwtSecurityTokenHandler();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                new Claim(ClaimTypes.PrimarySid, existingUser.Id.ToString()),
                new Claim(ClaimTypes.Email, existingUser.Email)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var Token = tokenHandler.CreateToken(tokenDescriptor);

            LoginResponseDTO loginResponseDTO=new LoginResponseDTO() { 
                token=tokenHandler.WriteToken(Token),
                user=existingUser
            };

            return Ok(loginResponseDTO);
        }
    }
}
