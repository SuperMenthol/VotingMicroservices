namespace TestApp
{
    public class ActiveVotingProcedures
    {
        public List<KeyValuePair<string, List<int>>> ProceduresWithOptions { get; set; } = new();
        public KeyValuePair<string, List<int>> RandomProcedure => ProceduresWithOptions[random.Next(0, ProceduresWithOptions.Count()-1)];
        public int RandomOption(KeyValuePair<string, List<int>> procedure) => procedure.Value[random.Next(0, procedure.Value.Count()-1)];

        private readonly Random random;
        public ActiveVotingProcedures()
        {
            random = new Random();
        }
    }
}
