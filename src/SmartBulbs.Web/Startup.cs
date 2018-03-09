using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pivotal.Discovery.Client;
using SmartBulbs.Common;
using SmartBulbs.Web.Hubs;
using SmartBulbs.Web.Models;
using Steeltoe.CircuitBreaker.Hystrix;
using Steeltoe.Management.CloudFoundry;

namespace SmartBulbs.Web
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
            services.AddMvc();
            services.AddSignalR();

            /* Begin non-boilerplate code for demo */
            services.AddDiscoveryClient(Configuration);
            services.AddHystrixCommand<NewPasswordCommand>("NewColor", Configuration);
            services.Configure<TwitterCredentials>(Configuration.GetSection("Twitter"));
            services.AddCloudFoundryActuators(Configuration);
            /* End non-boilerplate code for demo */
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseDiscoveryClient();

            app.UseSignalR(routes =>
            {
                routes.MapHub<ObservationHub>("/observe");
            });
            app.UseCloudFoundryActuators();
        }
    }
}
