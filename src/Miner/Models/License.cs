using System.Text.Json.Serialization;

namespace Miner.Models
{
    public class License
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("digAllowed")]
        public int DigAllowed { get; set; }

        [JsonPropertyName("digUsed")]
        public int DigUsed { get; set; }
    }
}
