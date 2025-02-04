using System.ComponentModel.DataAnnotations;

namespace RestAPI_INFO7255.Models
{
    public class LinkedService
    {
        [Required]
        public string _org { get; set; }

        [Required]
        public string ObjectId { get; set; }

        [Required]
        public string ObjectType { get; set; } = "service";

        [Required]
        public string Name { get; set; }
    }
}