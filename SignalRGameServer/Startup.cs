using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignalRGameServer.Models;
using SignalRGameServer.Services;
using SignalRGameServer.Services.Interfaces;
using SignalRUtils;

namespace SignalRGameServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.Configure<AuthOptions>(Configuration.GetSection("Auth"));
            services.Configure<SignalROptions>(Configuration.GetSection("Azure:SignalR"));
            services.AddSingleton<IAuthService, AuthService>();
            //services.AddSingleton<OrleansService>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(option =>
                {
                    option.TokenValidationParameters = services.BuildServiceProvider().GetService<IAuthService>().GetTokenValidationParameters();
                });

            services.AddSignalR()
                .AddMessagePackProtocol()
                .AddAzureSignalR(options =>
                {
                    options.ClaimsProvider = context =>
                    {
                        return context.User.Claims;
                    };
                    options.ConnectionCount = 20;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();

            app.UseFileServer();
            app.UseAzureSignalR(routes =>
            {
                routes.MapHub<GameHub>("/chatjwt");
            });

            app.UseAuthentication();
        }
    }
}
