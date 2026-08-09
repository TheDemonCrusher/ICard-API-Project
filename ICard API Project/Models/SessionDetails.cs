using System.Globalization;
using System.Text.Json.Serialization;

namespace ICard_API_Project.Models
{
    internal class SessionDetails
    {
        [JsonPropertyName("iccid")]
        public string iccid { get; set; }

        [JsonPropertyName("dateSessionStarted")]
        public string? startDate { get; set; }

        [JsonPropertyName("dateSessionEnded")]
        public string? endDate { get; set; }

        [JsonPropertyName("ipAddress")]
        public string? ipv4 { get; set; }

        [JsonPropertyName("ipv6Address")]
        public string? ipv6 { get; set; }

        [JsonPropertyName("apn")]
        public string? apn { get; set; }

        //Will be needed when the datetime needs to be written to the database as an actual date and not just a string
        public DateTime[] convertToDateTime()
        {
            string format = "yyyy-MM-ddTHH:mm:sszzz";
            DateTime[] result = new DateTime[2];

            if (DateTime.TryParseExact(startDate, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStartDate))
            {
                result[0] = parsedStartDate;
            }

            if (DateTime.TryParseExact(endDate, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEndDate))
            {
                result[1] = parsedEndDate;
            }

            return result;
        }
    }
}
