using System.Text.Json.Serialization;

namespace ICard_API_Project.Models
{
    internal class DeviceUsage
    {
        [JsonPropertyName("iccid")]
        public string iccid { get; set; }

        [JsonPropertyName("imsi")]
        public string? imsi { get; set; }

        [JsonPropertyName("msisdn")]
        public string? msisdn { get; set; }

        [JsonPropertyName("imei")]
        public string? imei { get; set; }

        [JsonPropertyName("status")]
        public string? status { get; set; }

        [JsonPropertyName("ratePlan")]
        public string? ratePlan { get; set; }

        [JsonPropertyName("communicationPlan")]
        public int communicationPlan { get; set; }

        [JsonPropertyName("ctdDataUsage")]
        public int dataUsage { get; set; }

        [JsonPropertyName("ctdVoiceUsage")]
        public int voiceUsage { get; set; }

        [JsonPropertyName("ctdSessionCount")]
        public int sessionCount { get; set; }

        [JsonPropertyName("overageLimitReached")]
        public bool overageLimitReached { get; set; }
        [JsonPropertyName("overageLimitOverride")]
        public string? overageLimitOverride { get; set; }
    }
}
