using Shared.Helpers;
using StatsWorker.Database;

namespace StatsWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Startup.RegisterBsonClassMaps();
            IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddScoped<IDatabaseOperations, DatabaseOperations>();
                    //services.AddHostedService<StatsWorker>();
                    //services.AddHostedService<StatsRequestWorker>();
                    services.AddSingleton<IHostedService, StatsWorker>(provider =>
                    {
                        return new StatsWorker(
                            provider.GetRequiredService<ILogger<StatsWorker>>(),
                            provider.GetRequiredService<IDatabaseOperations>(),
                            provider.GetRequiredService<IConfiguration>());
                    });
                    services.AddSingleton<IHostedService, StatsRequestWorker>(provider =>
                    {
                        return new StatsRequestWorker(
                            provider.GetRequiredService<ILogger<StatsRequestWorker>>(),
                            provider.GetRequiredService<IDatabaseOperations>(),
                            provider.GetRequiredService<IConfiguration>());
                    });
                })
                .Build();

            host.Run();
        }
    }
}