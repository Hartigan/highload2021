using System.Text.Json.Serialization;

namespace Miner.Models
{
    public class Dig
    {
        [JsonPropertyName("posX")]
        public int PosX { get; set; }

        [JsonPropertyName("posY")]
        public int PosY { get; set; }

        [JsonPropertyName("licenseID")]
        public int LicenseId { get; set; }

        [JsonPropertyName("depth")]
        public int Depth { get; set; }
    }
}
