using VoteReceivingWorker.Database;

namespace VoteReceivingWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddScoped<IDatabaseOperations, DatabaseOperations>();
                    services.AddHostedService(WorkerFactory(configuration));
                })
                .Build();

            host.Run();
        }

        private static Func<IServiceProvider, Worker> WorkerFactory(IConfiguration configuration)
        {
            return (sp) => new Worker(
                sp.GetRequiredService<ILogger<Worker>>(),
                configuration.GetRequiredSection("RabbitMq"),
                configuration.GetRequiredSection("MongoDb"));
        }
    }
}