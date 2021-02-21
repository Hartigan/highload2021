using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Miner.Models
{
    public class Wallet
    {
        [JsonPropertyName("balance")]
        public int Balance { get; set; }

        [JsonPropertyName("wallet")]
        public List<int> Data { get; set; }
    }
}
