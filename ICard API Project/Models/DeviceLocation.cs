using System.Globalization;
using System.Text.Json.Serialization;

namespace ICard_API_Project.Models
{
    internal record simLocations(
        string iccid,
        DateTime? dateReceived,
        int cellId,
        int cellLac,
        int servingMcc,
        int servingMnc,
        float? latitude,
        float? longitude,
        float? accuracy,
        string? city,
        string? state,
        string? country
    )
    {
        [JsonConstructor]
        public simLocations(string iccid, string date, int cellId, int cellLac, int servingMcc, int servingMnc,
            float? latitude, float? longitude,float? accuracy, string? city, string? state, string? country)
            : this(iccid, convertToDateTime(date), cellId, cellLac, servingMcc, servingMnc, latitude, longitude, accuracy, city, state, country) { }
        public static DateTime? convertToDateTime(string date)
        {
            string format = "yyyy-MM-ddTHH:mm:sszzz";

            if (DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStartDate))
                return parsedStartDate;

            return null;
        }
    };
    internal class DeviceLocation
    {
        [JsonPropertyName("simLocations")]
        public simLocations[] all_locations { get; set; }
        public int pageNumber { get; set; }
        public bool lastPage { get; set; }

        public DeviceLocation()
        {
            all_locations = Array.Empty<simLocations>();
            pageNumber = 1;
            lastPage = false;
        }

        public void AddNextPage(DeviceLocation newPage)
        {
            pageNumber = newPage.pageNumber;
            lastPage = newPage.lastPage;
            all_locations = all_locations.Concat(newPage.all_locations).ToArray();
        }
    }
}
