using MongoDB.Bson.Serialization;
using Shared.Models;

namespace Shared.Helpers
{
    public static class Startup
    {
        public static void RegisterBsonClassMaps()
        {
            BsonClassMap.RegisterClassMap<ProcedureModel>(classMap =>
            {
                classMap.AutoMap();
                classMap.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<OptionModel>(classMap =>
            {
                classMap.AutoMap();
                classMap.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<VoteModel>(classMap =>
            {
                classMap.AutoMap();
                classMap.SetIgnoreExtraElements(true);
            });
        }
    }
}
