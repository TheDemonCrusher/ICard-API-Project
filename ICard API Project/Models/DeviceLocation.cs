using System.Globalization;
using System.Text.Json.Serialization;

namespace ICard_API_Project.Models
{
    internal record simLocationsDated(
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
        public simLocationsDated(simLocations loc)
            : this(loc.iccid, convertToDateTime(loc.dateReceived), loc.cellId, loc.cellLac, loc.servingMcc, loc.servingMnc, loc.latitude, loc.longitude, loc.accuracy, loc.city, loc.state, loc.country) { }
        public static DateTime? convertToDateTime(string? date)
        {
            if (string.IsNullOrEmpty(date))
                return null;
            string format = "yyyy-MM-ddTHH:mm:sszzz";

            if (DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStartDate))
                return parsedStartDate;

            return null;
        }
    };
    internal record simLocations(
        string iccid,
        string? dateReceived,
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
    ) { };
    internal class DeviceLocation
    {
        [JsonPropertyName("simLocations")]
        public List<simLocations> raw_locations { get; set; }

        public List<simLocationsDated> all_locations { get; set; }
        public int pageNumber { get; set; }
        public bool lastPage { get; set; }

        public DeviceLocation()
        {
            raw_locations = new List<simLocations>();
            all_locations = new List<simLocationsDated>();
            pageNumber = 1;
            lastPage = false;
        }
        public void ConvertLocations()
        {
            foreach(simLocations raw in raw_locations)
                all_locations.Add(new simLocationsDated(raw));
        }
        public void AddNextPage(DeviceLocation newPage)
        {
            pageNumber = newPage.pageNumber;
            lastPage = newPage.lastPage;
            raw_locations = raw_locations.Concat(newPage.raw_locations).ToList();
        }
    }
}
