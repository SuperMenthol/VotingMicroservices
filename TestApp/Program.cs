namespace TestApp
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
                    services.AddSingleton<ActiveVotingProcedures>();
                    services.AddHostedService<TestManagingWorker>();
                    services.AddHostedService<TestVoteSendingWorker>();
                })
                .Build();

            host.Run();
        }
    }
}