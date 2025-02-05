using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace RestAPI_INFO7255.Models
{
    public class LinkedPlanService
    {
        [Required]
        public LinkedService LinkedService { get; set; } = new LinkedService();

        [Required]
        public PlanServiceCostShares PlanServiceCostShares { get; set; } = new PlanServiceCostShares();

        [Required]
        public string _org { get; set; }

        [Required]
        public string ObjectId { get; set; }

        [Required]
        public string ObjectType { get; set; } = "planservice";
    }
}