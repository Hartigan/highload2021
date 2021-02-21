using System.Text.Json.Serialization;

namespace Miner.Models
{
    public class Explore
    {
        [JsonPropertyName("area")]
        public Area Area { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; }
    }
}
