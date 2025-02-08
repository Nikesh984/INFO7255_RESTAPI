using CodeChallenge.Config;
using RestAPI_INFO7255.Repositories;
using RestAPI_INFO7255.Services;
using StackExchange.Redis;

namespace RestAPI_INFO7255.Config
{
    public class App
    {
        public WebApplication Configure(string[] args)
        {
            args ??= Array.Empty<string>();

            var builder = WebApplication.CreateBuilder(args);

            builder.UseRedisDB();

            AddServices(builder.Services);

            var app = builder.Build();

            var env = builder.Environment;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllers();
            //app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            return app;
        }

        private void AddServices(IServiceCollection services)
        {

            services.AddScoped<IPlanService, PlanService>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddControllers();
        }
    }
}