using System.Text.Json.Serialization;

namespace ICard_API_Project.Models
{
    internal record simLocations(
        string iccid,
        string dateReceived,
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
    ){};
    internal class DeviceLocation
    {
        [JsonPropertyName("simLocations")]
        public simLocations[] locations { get; set; }
        public int pageNumber { get; set; }
        public bool lastPage { get; set; }

        public DeviceLocation() 
        {
            locations = Array.Empty<simLocations>();
            pageNumber = 1;
            lastPage = false;
        }

        public void AddNextPage(DeviceLocation newPage)
        {
            pageNumber = newPage.pageNumber;
            lastPage = newPage.lastPage;
            locations = locations.Concat(newPage.locations).ToArray();
        }
    }
}
