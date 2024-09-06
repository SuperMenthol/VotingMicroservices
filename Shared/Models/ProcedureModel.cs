namespace Shared.Models
{
    public class ProcedureModel
    {
        public string Name { get; set; }
        public string RoutingKey { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<OptionModel> Options { get; set; }
        public VotingResultsModel LatestResults { get; set; }

        public ProcedureModel() { }

        public ProcedureModel(string name, string routingKey, string description, bool isActive, IEnumerable<OptionModel> options)
        {
            Name=name;
            RoutingKey=routingKey;
            Description=description;
            IsActive=isActive;
            Options=options;
            LatestResults = new VotingResultsModel(DateTime.Now, routingKey, options.Select(x => new OptionResultModel(x.OptionId, x.OptionName, 0, 0))); ;
        }
    }
}