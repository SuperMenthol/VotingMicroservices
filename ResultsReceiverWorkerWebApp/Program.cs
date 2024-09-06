namespace ResultsReceiverWorkerWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddHostedService(WorkerFactory());
                })
                .Build();

            host.Run();
        }

        private static Func<IServiceProvider, BackgroundService> WorkerFactory()
        {
            return (sp) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return new ResultsReceivingWorker(config);
            };
        }
    }
}