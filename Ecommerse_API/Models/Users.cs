using System.ComponentModel.DataAnnotations;

namespace Ecommerce_API.Models
{
    public class Users
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
        public byte isAdmin { get; set; } = 0;
        public string Address { get; set; } 
    }
}
