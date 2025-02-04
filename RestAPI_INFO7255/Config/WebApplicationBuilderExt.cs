using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CodeChallenge.Config
{
    public static class WebApplicationBuilderExt
    {
        private static readonly string DB_NAME = "INFO7225";
        public static void UseEmployeeDB(this WebApplicationBuilder builder)
        {
            // builder.Services.AddDbContext<PlanContext>(options =>
            // {
            //     options.UseInMemoryDatabase(DB_NAME);
            // });
        }
    }
}