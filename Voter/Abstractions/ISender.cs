using Shared.Models;

namespace Voter.Abstractions
{
    public interface ISender
    {
        bool Send(string routingKey, VoteModel model);
    }
}
