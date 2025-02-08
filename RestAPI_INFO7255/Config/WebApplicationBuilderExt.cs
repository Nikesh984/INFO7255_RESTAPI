using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CodeChallenge.Config
{
    public static class WebApplicationBuilderExt
    {
        //private static readonly string DB_NAME = "INFO7225";
        public static void UseRedisDB(this WebApplicationBuilder builder)
        {
            var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

            builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));

            builder.Services.AddScoped<IDatabase>(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
        }
    }
}