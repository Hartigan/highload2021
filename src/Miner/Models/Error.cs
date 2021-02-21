using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Miner.Models
{
    public class Error
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
