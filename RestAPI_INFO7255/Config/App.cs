using CodeChallenge.Config;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Nest;
using RestAPI_INFO7255.Consumers;
using RestAPI_INFO7255.Repositories;
using RestAPI_INFO7255.Services;

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

            // using (var scope = app.Services.CreateScope())
            // {
            //     var elasticClient = scope.ServiceProvider.GetRequiredService<IElasticClient>();
            //     var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();

            //     var indexExistsResponse = elasticClient.Indices.Exists("plans");
            //     if (!indexExistsResponse.)
            //     {
            //         var createIndexResponse = elasticClient.Indices.Create("plans", c => c
            //             .Map(m => m
            //                 .Properties(p => p
            //                     .Join(j => j
            //                         .Name("joinField")
            //                         .Relations(r => r
            //                             .Join("plan", "planCostShare")
            //                             .Join("plan", "linkedPlanServices")
            //                             .Join("linkedPlanServices", "linkedService")
            //                             .Join("linkedPlanServices", "planserviceCostShares")
            //                         )
            //                     )
            //                     .Keyword(k => k.Name("objectId"))
            //                     .Keyword(k => k.Name("_org"))
            //                     .Keyword(k => k.Name("objectType"))
            //                     .Keyword(k => k.Name("planType"))
            //                     .Date(d => d.Name("creationDate"))
            //                     .Number(n => n.Name("deductible").Type(NumberType.Integer))
            //                     .Number(n => n.Name("copay").Type(NumberType.Integer))
            //                     .Text(t => t.Name("name"))
            //                 )
            //             )
            //         );

            //         if (!createIndexResponse.IsValid)
            //         {
            //             logger.LogError($"Failed to create plans index: {createIndexResponse.ServerError?.Error?.Reason ?? createIndexResponse.DebugInformation}");
            //         }
            //         else
            //         {
            //             logger.LogInformation("Successfully created plans index with join field mapping.");
            //         }
            //     }
            // }

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


            //ElasticSearch
            var settings = new ConnectionSettings(new Uri("http://localhost:9200")).DefaultIndex("plans");
            services.AddSingleton<IElasticClient>(new ElasticClient(settings));

            // RabbitMQ
            services.AddMassTransit(x =>
            {
                x.AddConsumer<PlanUpdatedConsumer>();
                x.AddConsumer<PlanDeletedConsumer>();
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
                    cfg.ReceiveEndpoint("plan-updated", e => e.ConfigureConsumer<PlanUpdatedConsumer>(context));
                    cfg.ReceiveEndpoint("plan-deleted", e => e.ConfigureConsumer<PlanDeletedConsumer>(context));
                });
            });

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