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
                    services.AddHostedService<StatsWorker>();
                    services.AddHostedService<StatsRequestWorker>();
                })
                .Build();

            host.Run();
        }
    }
}