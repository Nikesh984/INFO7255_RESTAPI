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
            services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect("localhost:6379"));

            services.AddScoped<IDatabase>(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

            services.AddScoped<IPlanService, PlanService>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddControllers();
        }
    }
}