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
                    for (int i = 0; i < 10; i++) // loop through all iccids in the iccid table, this loop will be a while(reader) or sth probably
                    {
                        CombinedInfo info = new CombinedInfo();

                        string iccid = "next id"; // we get the current iccid and asign it to the combined model
                        info.iccid = iccid;
                        info.details = await GetSessionDetailsAsync(iccid);
                        info.usage = await GetDeviceUsageAsync(iccid);
                        info.location = await GetDeviceLocationsAsync(iccid);

                        //write the information to the respective tables        
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
                    //cmd.Parameters.AddWithValue("@LastUpdated", DateTime.UtcNow);

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
