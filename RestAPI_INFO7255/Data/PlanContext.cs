using Microsoft.EntityFrameworkCore;
using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Data
{
    public class PlanContext : DbContext
    {
        public PlanContext(DbContextOptions<PlanContext> options) : base(options)
        {

        }

        public DbSet<Plan> Plans { get; set; }
    }
}