using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Shared.Models
{
    public class VoteModel
    {
        public string PersonId { get; set; }
        public int OptionId { get; set; }
        [JsonIgnore]
        public DateTime CreatedAt { get; set; }

        public VoteModel(string personId, int optionId)
        {
            PersonId=personId;
            OptionId=optionId;
            CreatedAt = DateTime.Now;

            if (!Validate())
            {
                throw new InvalidDataException();
            }
        }

        private bool Validate()
            => Regex.IsMatch(PersonId, "[a-zA-Z]{3}[0-9]{6}") && OptionId >= 1 && OptionId <= 255;

    }
}