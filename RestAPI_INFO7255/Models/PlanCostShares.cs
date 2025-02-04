using System.ComponentModel.DataAnnotations;

namespace RestAPI_INFO7255.Models
{
    public class PlanCostShares
    {
        [Required]
        public int Deductible { get; set; }

        [Required]
        public string _org { get; set; }

        [Required]
        public int Copay { get; set; }

        [Required]
        public string ObjectId { get; set; }

        [Required]
        public string ObjectType { get; set; } = "membercostshare";
    }

}