using CollectionsCleanupWorker.Database;
using Shared.Helpers;

namespace CollectionsCleanupWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Startup.RegisterBsonClassMaps();
            var builder = WebApplication.CreateBuilder(args);

            IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddScoped<IDatabaseOperations, DatabaseOperations>();
                    services.AddHostedService<CollectionsCleanupWorker>();
                })
                .Build();

            host.Run();
        }
    }
}