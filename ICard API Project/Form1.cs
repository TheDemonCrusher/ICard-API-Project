using ICard_API_Project.Models;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace ICard_API_Project
{
    public partial class Form1 : Form
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private CancellationTokenSource _cancellationTokenSource;

        public Form1(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            InitializeComponent();
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private async Task<SessionDetails?> GetSessionDetailsAsync(string iccid)
        {
            if (iccid == String.Empty)
                return null;

            string endpoint = iccid + _configuration["Endpoints:SessionDetails"];
            string jsonResult = await sendGetRequestsAsync(endpoint);
            if (jsonResult == null)
                return null;

            try
            {
                SessionDetails? details = JsonSerializer.Deserialize<SessionDetails>(jsonResult);
                return details;
            }
            catch (Exception ex)
            {
                return null;
            } 
        }

        private async Task<DeviceUsage?> GetDeviceUsageAsync(string iccid)
        {
            if (iccid == String.Empty)
                return null;

            string endpoint = iccid + _configuration["Endpoints:DeviceUsage"];
            string jsonResult = await sendGetRequestsAsync(endpoint);
            if (jsonResult == null)
                return null;

            try
            {
                DeviceUsage? usage = JsonSerializer.Deserialize<DeviceUsage>(jsonResult);
                return usage;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task<DeviceLocation?> GetDeviceLocationsAsync(string iccid)
        {
            if (iccid == String.Empty)
                return null;

            string endpoint = iccid + _configuration["Endpoints:DeviceLocation"];

            DeviceLocation locations = new DeviceLocation();

            while (locations.lastPage == false) //Multiple pages not tested yet
            {
                string jsonResult = await sendGetRequestsAsync(endpoint + $"?pageNumber={locations.pageNumber}");
                DeviceLocation? currentPage = null;

                if (jsonResult != null)
                    currentPage = JsonSerializer.Deserialize<DeviceLocation>(jsonResult);

                if (currentPage != null)
                    locations.AddNextPage(currentPage);
                else
                    locations.lastPage = true;
            }
            return locations;
        }

        private async Task<string> sendGetRequestsAsync(string endpoint)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ICardApiClient");

                HttpResponseMessage response = await client.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                //log error
                return null;
            }
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _ = RunApiBackgroundLoopAsync(_cancellationTokenSource.Token);
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
        }

        private async Task RunApiBackgroundLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (string iccid in await ReadIccidsToStringArrayAsync()) // loop through all iccids in the iccid table
                    {
                        CombinedInfo info = new CombinedInfo();

                        info.iccid = iccid;
                        info.session = await GetSessionDetailsAsync(iccid);
                        info.usage = await GetDeviceUsageAsync(iccid);
                        info.locations = await GetDeviceLocationsAsync(iccid);

                        //write the information to the respective tables
                        if (info.usage is not null)
                            await UpdateDatabaseWithDeviceUsagesAsync(info.usage);
                        if (info.session is not null)
                            await UpdateDatabaseWithSessionInfoAsync(info.session);
                        if (info.locations is not null)
                            await UpdateDatabaseWithDeviceLocationsAsync(info.locations);
                    }
                }
                catch (Exception ex) //TODO: Create a catch method specifically for database related errors to be handled differently
                {
                    break;//log error
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), token);
                }
                catch (Exception ex)
                {
                    break;
                }
            }
        }

        private async Task UpdateDatabaseWithDeviceUsagesAsync(DeviceUsage data)
        {
            string? connString = _configuration["Database:ConnString"];

            if (connString == String.Empty) //TODO: Make the user aware of this error
                return;

            string query = @"
                IF EXISTS (SELECT 1 FROM DeviceUsages WHERE iccid = @iccid)
                BEGIN
                    -- If it exists, update it
                    UPDATE DeviceUsages 
                    SET imsi = @imsi, msisdn = @msisdn,  imei = @imei, status = @status, ratePlan = @ratePlan, communicationPlan = @communicationPlan, ctdDataUsage = @dataUsage, ctdVoiceUsage = @voiceUsage, ctdSessionCount = @sessionCount overageLimitReached = @overageLimitReached, overageLimitOverride = @overageLimitOverride 
                    WHERE iccid = @iccid
                END
                ELSE
                BEGIN
                    -- If it does not exist, insert it
                    INSERT INTO DeviceUsages (iccid, imsi, msisdn, imei, status, ratePlan, communicationPlan, ctdDataUsage, ctdVoiceUsage, ctdSessionCount, overageLimitReached, overageLimitOverride) 
                    VALUES (@iccid, @imsi, @msisdn, @imei, @status, @ratePlan, @communicationPlan, @dataUsage, @voiceUsage, @sessionCount @overageLimitReached, @overageLimitOverride)
                END";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@iccid", data.iccid);
                    cmd.Parameters.AddWithValue("@imsi", data.imsi ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@msisdn", data.msisdn ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@imei", data.imei ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", data.status ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ratePlan", data.ratePlan ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@communicationPlan", data.communicationPlan);
                    cmd.Parameters.AddWithValue("@dataUsage", data.dataUsage);
                    cmd.Parameters.AddWithValue("@voiceUsage", data.voiceUsage);
                    cmd.Parameters.AddWithValue("@sessionCount", data.sessionCount);
                    cmd.Parameters.AddWithValue("@overageLimitReached", data.overageLimitReached);
                    cmd.Parameters.AddWithValue("@overageLimitOverride", data.overageLimitOverride ?? (object)DBNull.Value);

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateDatabaseWithSessionInfoAsync(SessionDetails data)
        {
            string? connString = _configuration["Database:ConnString"];

            if (connString == String.Empty) //TODO: Make the user aware of this error
                return;

            string query = @"
                IF EXISTS (SELECT 1 FROM SessionDetails WHERE iccid = @iccid)
                BEGIN
                    -- If it exists, update it
                    UPDATE SessionDetails 
                    SET dateSessionStarted = @startDate, dateSessionEnded = @endDate,  ipAddress = @ipv4, ipv6Address = @ipv6, apn = @apn
                    WHERE iccid = @iccid
                END
                ELSE
                BEGIN
                    -- If it does not exist, insert it
                    INSERT INTO SessionDetails (iccid, dateSessionStarted, dateSessionEnded, ipAddress, ipv6Address, apn) 
                    VALUES (@iccid, @startDate, @endDate, @ipv4, @ipv6, @apn)
                END";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@iccid", data.iccid);
                    cmd.Parameters.AddWithValue("@startDate", data.convertToDateTime()[0]);
                    cmd.Parameters.AddWithValue("@endDate", data.convertToDateTime()[1]);
                    cmd.Parameters.AddWithValue("@ipv4", data.ipv4 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ipv6", data.ipv6 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@apn", data.apn ?? (object)DBNull.Value);

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateDatabaseWithDeviceLocationsAsync(DeviceLocation data)
        {
            return;
            //TODO: figure out the table/s needed for this one since its more complicated
            //loop through all locations and insert/update them
        }

        private async Task<string[]> ReadIccidsToStringArrayAsync()
        {
            string connString = _configuration["Database:ConnString"];

            string query = "SELECT iccid FROM Iccids";

            List<string> items = new List<string>();

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                string rowValue = reader.GetString(0);
                                items.Add(rowValue);
                            }
                        }
                    }
                }
            }

            return items.ToArray();
        }
    }
}
