using Shared.Helpers;
using VoteDatabaseManager.Database;

namespace VoteDatabaseManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Startup.RegisterBsonClassMaps();
            // Add services to the container.
            IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            builder.Services.AddSingleton(configuration);
            builder.Services.AddScoped<IDatabaseOperations, DatabaseOperations>();
            builder.Services.AddControllers();
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddScoped<IDatabaseOperations, DatabaseOperations>();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddHostedService<RedundantCollectionsCleanupWorker>(); // TODO: nie dzia³a, uruchamia siê tylko raz
                })
                .Build();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            host.Run();
            app.Run();
        }
    }
}