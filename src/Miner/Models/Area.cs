using System.Text.Json.Serialization;

namespace Miner.Models
{
    public class Area
    {
        [JsonPropertyName("posX")]
        public int PosX { get; set; }

        [JsonPropertyName("posY")]
        public int PosY { get; set; }

        [JsonPropertyName("sizeX")]
        public int SizeX { get; set; }

        [JsonPropertyName("sizeY")]
        public int SizeY { get; set; }
    }
}
