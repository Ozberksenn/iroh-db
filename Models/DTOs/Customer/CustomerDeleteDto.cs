
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.DTOs.Customer
{
    public class CustomerDeleteDto
    {

        [Required]
        public required Boolean isDeleted { get; set; }


    }
}