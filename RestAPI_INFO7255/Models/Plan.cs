using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Primitives;

namespace RestAPI_INFO7255.Models
{
    public class Plan
    {
        public PlanCostShares? PlanCostShares { get; set; }

        public List<LinkedPlanService>? LinkedPlanServices { get; set; }

        [Required]
        public string _org { get; set; }

        [Required]
        public string ObjectId { get; set; }

        [Required]
        public string ObjectType { get; set; } = "plan";

        [Required]
        public string PlanType { get; set; }

        [Required]
        public DateTimeOffset? CreationDate { get; set; }
    }
}