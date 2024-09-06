using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using Voter.Abstractions;

namespace Voter.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VoteController : ControllerBase
    {
        private readonly ILogger<VoteController> _logger;
        private readonly ISender sender;

        public VoteController(ILogger<VoteController> logger, ISender sender)
        {
            _logger = logger;
            this.sender = sender;
        }

        [HttpPost("{routingKey}")]
        public bool Vote([FromRoute] string routingKey, [FromBody] VoteModel model)
            => sender.Send(routingKey, model);
    }
}