using CodeChallenge.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
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

            app.Use(async (context, next) =>
           {
               await next();
               if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
               {
                   context.Response.ContentType = "application/json";
                   var problemDetails = new ProblemDetails
                   {
                       Status = 401,
                       Title = "Unauthorized",
                       Detail = "Invalid or missing Bearer token. Please provide a valid token signed by Google."
                   };
                   await context.Response.WriteAsJsonAsync(problemDetails);
               }
           });

            app.UseAuthentication();
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
            //.AddNewtonsoftJson();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "https://accounts.google.com";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "https://accounts.google.com",
                        ValidateAudience = true,
                        ValidAudience = "182172321365-o1jvpm9gdacqj1u4ai35tl9bjleajd8g.apps.googleusercontent.com",
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            var problemDetails = new ProblemDetails
                            {
                                Status = 401,
                                Title = "Unauthorized",
                                Detail = "Token validation failed. Ensure the Bearer token is valid and signed by Google."
                            };
                            return context.Response.WriteAsJsonAsync(problemDetails);
                        }
                    };
                });
        }
    }
}