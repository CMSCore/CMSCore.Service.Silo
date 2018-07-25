namespace CMSCore.Service.Silo
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Library.Data.Configuration;
    using Library.Data.Context;
    using Library.Grains;
    using Library.Repository;
    using Library.Repository.Implementations;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Orleans;
    using Orleans.Configuration;
    using Orleans.Hosting;

    public class Program
    {
        private static ISiloHost silo;
        private static readonly ManualResetEvent siloStopped = new ManualResetEvent(false);

        static void Main(string [ ] args)
        {
            silo = new SiloHostBuilder()
                //.Configure<ClusterOptions>(options =>
                //{
                //    options.ClusterId = "orleans-docker";
                //    options.ServiceId = "AspNetSampleApp";
                //})
                .UseLocalhostClustering()
                //.ConfigureEndpoints(siloPort: 11111, gatewayPort: 30000)
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(ReadContentGrain).Assembly).WithReferences().WithCodeGeneration())
                .ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Warning).AddConsole())
                .ConfigureServices(services =>
                {
                    var conf = Configuration;
                    services.AddSingleton<IConfiguration>(conf);

 
                    services.AddSingleton<IDataConfiguration>(provider => new DataConfiguration(conf));
                    services.AddSingleton<IContentDbContext>(provider =>
                    {
                        var p = provider.GetService<IDataConfiguration>();
                        return new ContentDbContext(p);
                    });
                    services.AddSingleton<IReadContentRepository, ReadContentRepository>();
                })
                .Build();

            Task.Run(StartSilo);

            AssemblyLoadContext.Default.Unloading += context =>
            {
                Task.Run(StopSilo);
                siloStopped.WaitOne();
            };

            siloStopped.WaitOne();
        }

        private static async Task StartSilo()
        {
            await silo.StartAsync();
            Console.WriteLine("************ Silo started ************");
        }

        private static async Task StopSilo()
        {
            await silo.StopAsync();
            Console.WriteLine("************ Silo stopped ************");
            siloStopped.Set();
        }

        public static IConfiguration Configuration => new ConfigurationBuilder()
             .AddJsonFile("appsettings.json", false, true)
            .Build();
    }
}