using Bogus;
using RestEase;
using Shared.Helpers;
using Shared.Models;

namespace TestApp
{
    public class TestVoteSendingWorker : BackgroundService
    {
        private readonly Random random;
        private readonly int MaxIntervalMilliseconds;
        private readonly IVoterAPI restClient;
        private readonly ActiveVotingProcedures activeProcedures;
        private Randomizer randomizer;

        public TestVoteSendingWorker(IConfiguration configuration, ActiveVotingProcedures activeVotingProcedures)
        {
            random = new Random();
            MaxIntervalMilliseconds = int.Parse(configuration.GetRequiredSection("SenderConfiguration")[nameof(MaxIntervalMilliseconds)]);
            restClient = RestClient.For<IVoterAPI>(configuration["VoterUrl"]);
            activeProcedures = activeVotingProcedures;
            randomizer = new Randomizer();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TaskAwaiter.Wait(10);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (activeProcedures.ProceduresWithOptions.Any())
                {
                    Console.WriteLine($"{activeProcedures.ProceduresWithOptions.Count()} procedures found.");
                    var personIdPrefix = new string(randomizer.Chars('\u0041', '\u005a', 3));
                    var personIdSuffix = new string(randomizer.Chars('\u0030', '\u0039', 6));
                    var procedure = activeProcedures.RandomProcedure;
                    var request = new VoteModel(
                        personIdPrefix + personIdSuffix,
                        activeProcedures.RandomOption(procedure));

                    await restClient.SendVote(procedure.Key, request);
                    Console.WriteLine($"Sent vote to {procedure.Key}");
                }

                await Task.Delay(random.Next(0, MaxIntervalMilliseconds), stoppingToken);
            }
        }
    }
}
