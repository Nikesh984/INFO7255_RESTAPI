using System.ComponentModel.DataAnnotations;

namespace RestAPI_INFO7255.Models
{
    public class Plan
    {
        [Required]
        public PlanCostShares PlanCostShares { get; set; } = new PlanCostShares();

        [Required]
        public List<LinkedPlanService> LinkedPlanServices { get; set; } = new List<LinkedPlanService>();

        [Required]
        public string _org { get; set; }

        [Required]
        public string ObjectId { get; set; }

        [Required]
        public string ObjectType { get; set; } = "plan";

        [Required]
        public string PlanType { get; set; }

        [Required]
        public DateTime CreationDate { get; set; }
    }
}