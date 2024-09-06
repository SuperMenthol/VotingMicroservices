using Bogus;
using RestEase;
using Shared.Helpers;
using Shared.Models;
using Shared.Request;

namespace TestApp
{
    public class TestManagingWorker : BackgroundService
    {
        private readonly Random random;
        private readonly int MinIntervalMilliseconds;
        private readonly int MaxIntervalMilliseconds;
        private readonly IManagerAPI restClient;
        private readonly ActiveVotingProcedures activeProcedures;
        private Randomizer randomizer;

        public TestManagingWorker(IConfiguration configuration, ActiveVotingProcedures activeVotingProcedures)
        {
            this.random = new Random();
            MinIntervalMilliseconds = int.Parse(configuration.GetRequiredSection("ManagerConfiguration")[nameof(MinIntervalMilliseconds)]);
            MaxIntervalMilliseconds = int.Parse(configuration.GetRequiredSection("ManagerConfiguration")[nameof(MaxIntervalMilliseconds)]);
            restClient = RestClient.For<IManagerAPI>(configuration["ManagerUrl"]);
            activeProcedures = activeVotingProcedures;
            randomizer = new Randomizer();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TaskAwaiter.Wait(10);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (random.Next(0, 10) > 7 && activeProcedures.ProceduresWithOptions.Count() > 3)
                {
                    SendRemoveRequest();
                }
                else if (activeProcedures.ProceduresWithOptions.Count() <= 5)
                {
                    SendCreateRequest();
                }

                await Task.Delay(random.Next(MinIntervalMilliseconds, MaxIntervalMilliseconds), stoppingToken);
            }
        }

        private async void SendCreateRequest()
        {
            try
            {
                Console.WriteLine("Adding procedure");
                var suggestedName = randomizer.Words(1);
                while (activeProcedures.ProceduresWithOptions.Any(x => x.Key == suggestedName))
                {
                    suggestedName = randomizer.Words(1);
                }
                var description = randomizer.Words(5);
                var options = new List<OptionModel>();

                for (int i = 0; i < random.Next(2, 8); i++)
                {
                    options.Add(new(i + 1, randomizer.Word()));
                }

                var request = new AddVotingProcedureRequest(new ProcedureModel(
                    suggestedName,
                    suggestedName.ToLower(),
                    description,
                    true,
                    options));

                await restClient.AddProcedure(request);
                Console.WriteLine($"Added procedure {suggestedName.ToLower()}.");

                activeProcedures.ProceduresWithOptions.Add(
                    new(suggestedName.ToLower(), options.Select(x => x.OptionId).ToList()));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async void SendRemoveRequest()
        {
            var procedureToRemove = activeProcedures.RandomProcedure;
            Console.WriteLine($"Removing procedure {procedureToRemove}");
            await restClient.RemoveProcedure(procedureToRemove.Key);
            activeProcedures.ProceduresWithOptions.Remove(procedureToRemove);

            Console.WriteLine("Procedure removal scheduled.");
        }
    }
}
