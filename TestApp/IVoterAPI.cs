using RestEase;
using Shared.Models;

namespace TestApp
{
    public interface IVoterAPI
    {
        [Post("vote/{routingKey}")]
        Task<bool> SendVote([Path] string routingKey, [Body] VoteModel vote);
    }
}
