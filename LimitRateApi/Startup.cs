using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;

namespace LimitRateApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        public void ConfigureServices(IServiceCollection services)
        {
            string _connectionString = Configuration["RedisConfig:Default:Connection"];//连接字符串
            string _instanceName = Configuration["RedisConfig:Default:InstanceName"];//实例名称
            int _defaultDB = int.Parse(Configuration["RedisConfig:Default:DefaultDB"] ?? "0"); //默认数据库           
            services.AddSingleton(new RedisHelper(_connectionString, _instanceName, _defaultDB));
            services.AddSingleton<RateLimitClient>();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }


        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
