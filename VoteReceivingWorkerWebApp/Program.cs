using Shared.Helpers;
using VoteReceivingWorker.Database;

namespace VoteReceivingWorker
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
                    services.AddHostedService(WorkerFactory());
                })
                .Build();

            host.Run();
        }

        private static Func<IServiceProvider, VoteReceivingWorker> WorkerFactory()
        {
            return (sp) => new VoteReceivingWorker(
                sp.GetRequiredService<ILogger<VoteReceivingWorker>>(),
                sp.GetRequiredService<IDatabaseOperations>(),
                sp.GetRequiredService<IConfiguration>().GetRequiredSection("RabbitMq"));
        }
    }
}