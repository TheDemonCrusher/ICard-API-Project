using Dapper;
using ICard_API_Project.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

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

            while (locations.lastPage == false) //Multiple pages not tested yet since none are in the sample data
            {
                string jsonResult = await sendGetRequestsAsync(endpoint + $"?pageNumber={locations.pageNumber}");
                DeviceLocation? currentPage = null;

                if (jsonResult != null)
                    currentPage = JsonSerializer.Deserialize<DeviceLocation>(jsonResult);

                if (currentPage == null || currentPage.all_locations == null)
                    locations.lastPage = true;
                else
                    locations.AddNextPage(currentPage);
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
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                MessageBox.Show($"Hit API rate limit. Try lowering MaxDegreeOfParallelism.");
                return null;
            }
            catch (Exception ex)
            {
                //log error
                return null;
            }
        }
        private async void importBtn_Click(object sender, EventArgs e)
        {
            DialogResult choice = MessageBox.Show(
            "Would you like to fully overwrite all iccids with the ones in this file? (iccids will simply update if not)",
            "Choose an Option",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xls)|*.xls";
            dialog.Multiselect = false;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string path = dialog.FileName;
                List<string> firstColumn = new List<string>();

                using (StreamReader reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read), Encoding.UTF8))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Split the row by comma and take the first item (index 0)
                        string[] columns = line.Split(',');
                        if (columns.Length > 0)
                        {
                            firstColumn.Add(columns[0].Trim());
                        }
                    }
                }
                if (choice == DialogResult.Yes)
                    await UpdateDatabaseWithIccidsAsync(firstColumn);
                if (choice == DialogResult.No)
                    await OverwriteDatabaseWithIccidsAsync(firstColumn);
            }
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null)
                return;

            startBtn.Enabled = false;
            stopBtn.Enabled = true;

            _cancellationTokenSource = new CancellationTokenSource();
            _ = RunApiBackgroundLoopAsync(_cancellationTokenSource.Token);
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                startBtn.Enabled = true;
                stopBtn.Enabled = false;
            }
        }

        private async Task RunApiBackgroundLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string[] ids = await ReadIccidsToStringArrayAsync();

                    var allSessions = new ConcurrentBag<SessionDetails>();
                    var allUsages = new ConcurrentBag<DeviceUsage>();
                    var allLocations = new ConcurrentBag<simLocations>();

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 20,
                        CancellationToken = token
                    };

                    int processedCount = 0;

                    await Parallel.ForEachAsync(ids, parallelOptions, async (iccid, ct) =>
                    {
                        // (These run concurrently for multiple ICCIDs at once)
                        var sessionTask = GetSessionDetailsAsync(iccid);
                        var usageTask = GetDeviceUsageAsync(iccid);
                        var locationsTask = GetDeviceLocationsAsync(iccid);

                        await Task.WhenAll(sessionTask, usageTask, locationsTask);

                        if (sessionTask.Result != null)
                            allSessions.Add(sessionTask.Result);

                        if (usageTask.Result != null)
                            allUsages.Add(usageTask.Result);

                        if (locationsTask.Result?.all_locations != null)
                        {
                            foreach (var loc in locationsTask.Result.raw_locations)
                            {
                                allLocations.Add(loc);
                            }
                        }
                    });

                    if (!allUsages.IsEmpty)
                        await UpdateDatabaseWithDeviceUsagesAsync(allUsages.ToList());

                    if (!allSessions.IsEmpty)
                        await UpdateDatabaseWithSessionInfoAsync(allSessions.ToList());

                    if (!allLocations.IsEmpty)
                        await UpdateDatabaseWithDeviceLocationsAsync(allLocations.ToList());
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

        private async Task UpdateDatabaseWithDeviceUsagesAsync(List<DeviceUsage> dataList)
        {
            string? connString = _configuration["Database:ConnString"];

            if (string.IsNullOrEmpty(connString) || dataList.Count == 0) //TODO: Make the user aware of this error
                return;

            string query = @"
                UPDATE device_usages 
                SET 
                    imsi = @imsi, msisdn = @msisdn, imei = @imei, status = @status, 
                    ratePlan = @ratePlan, communicationPlan = @communicationPlan, 
                    ctdDataUsage = @dataUsage, ctdVoiceUsage = @voiceUsage, 
                    ctdSessionCount = @sessionCount, overageLimitReached = @overageLimitReached, 
                    overageLimitOverride = @overageLimitOverride 
                WHERE iccid = @iccid;

                IF @@ROWCOUNT = 0
                BEGIN
                    INSERT INTO device_usages (iccid, imsi, msisdn, imei, status, ratePlan, communicationPlan, ctdDataUsage, ctdVoiceUsage, ctdSessionCount, overageLimitReached, overageLimitOverride) 
                    VALUES (@iccid, @imsi, @msisdn, @imei, @status, @ratePlan, @communicationPlan, @dataUsage, @voiceUsage, @sessionCount, @overageLimitReached, @overageLimitOverride);
                END";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();

                await conn.ExecuteAsync(query, dataList);
            }
        }

        private async Task UpdateDatabaseWithSessionInfoAsync(List<SessionDetails> dataList)
        {
            string? connString = _configuration["Database:ConnString"];

            if (string.IsNullOrEmpty(connString) || dataList.Count == 0) //TODO: Make the user aware of this error
                return;

            string query = @"
                UPDATE session_details 
                SET 
                    dateSessionEnded = @endDate, 
                    ipAddress = @ipv4, 
                    ipv6Address = @ipv6, 
                    apn = @apn
                WHERE iccid = @iccid 
                AND (
                    dateSessionStarted = @startDate 
                    OR (dateSessionStarted IS NULL AND @startDate IS NULL)
                );

                IF @@ROWCOUNT = 0
                BEGIN
                    INSERT INTO session_details (iccid, dateSessionStarted, dateSessionEnded, ipAddress, ipv6Address, apn) 
                    VALUES (@iccid, @startDate, @endDate, @ipv4, @ipv6, @apn);
                END";

            var mappedData = dataList.Select(data =>
            {
                var parsedDates = data.convertToDateTime();
                DateTime? startDate = parsedDates[0];
                DateTime? endDate = parsedDates[1];

                return new
                {
                    iccid = data.iccid,
                    startDate = (startDate.HasValue && startDate.Value.Year >= 1753) ? startDate.Value : (DateTime?)null,
                    endDate = (endDate.HasValue && endDate.Value.Year >= 1753) ? endDate.Value : (DateTime?)null,
                    ipv4 = data.ipv4,
                    ipv6 = data.ipv6,
                    apn = data.apn
                };
            }).ToList();

            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();

                await conn.ExecuteAsync(query, mappedData);
            }
        }

        private async Task UpdateDatabaseWithDeviceLocationsAsync(List<simLocations> data)
        {
            string? connString = _configuration["Database:ConnString"];

            if (string.IsNullOrEmpty(connString)) //TODO: Make the user aware of this error
                return;

            // Make sure any locations which couldnt have their dates parsed are filtered out
            var validData = data.Where(record => record.dateReceived != null).ToList();
            
            string sql = @"
                INSERT INTO device_locations (
                    iccid, dateReceived, cellId, cellLac, servingMcc, servingMnc, 
                    latitude, longitude, accuracy, city, state, country
                )
                SELECT 
                    @iccid, @dateReceived, @cellId, @cellLac, @servingMcc, @servingMnc, 
                    @latitude, @longitude, @accuracy, @city, @state, @country
                WHERE NOT EXISTS (
                    SELECT 1 FROM device_locations 
                    WHERE iccid = @iccid 
                    AND dateReceived = @dateReceived
                );";

            DeviceLocation devices = new DeviceLocation();
            devices.raw_locations = validData;
            devices.ConvertLocations();
            var mappedData = devices.all_locations.Select(data => new
            {
                iccid = data.iccid,
                dateReceived = data.dateReceived,
                cellId = data.cellId,
                cellLac = data.cellLac,
                servingMcc = data.servingMcc,
                servingMnc = data.servingMnc,
                latitude = data.latitude,
                longitude = data.longitude,
                accuracy = data.accuracy,
                city = data.city,
                state = data.state,
                country = data.country
            }).ToList();

            using (var connection = new SqlConnection(connString))
            {
                await connection.OpenAsync();

                await connection.ExecuteAsync(sql, mappedData);
            }
        }

        private async Task UpdateDatabaseWithIccidsAsync(List<string> data)
        {
            string? connString = _configuration["Database:ConnString"];

            if (string.IsNullOrEmpty(connString))
                return;

            string sql = @"
                INSERT INTO Iccids (iccid)
                SELECT @Iccid
                WHERE NOT EXISTS (
                    SELECT 1 FROM Iccids WHERE iccid = @Iccid
                );";

            var mappedData = data
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => new { Iccid = x })
            .ToList();

            if (mappedData.Count == 0) return;

            using (var connection = new SqlConnection(connString))
            {
                await connection.OpenAsync();

                await connection.ExecuteAsync(sql, mappedData);
            }
        }
        private async Task OverwriteDatabaseWithIccidsAsync(List<string> data)
        {
            string? connString = _configuration["Database:ConnString"];

            if (string.IsNullOrEmpty(connString))
                return;

            var mappedData = data
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .Select(x => new { Iccid = x })
            .ToList();

            string deleteSql = "DELETE FROM Iccids;";
            string insertSql = "INSERT INTO Iccids (iccid) VALUES (@Iccid);";

            using (var connection = new SqlConnection(connString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        await connection.ExecuteAsync(deleteSql, transaction: transaction);

                        if (mappedData.Count > 0)
                        {
                            await connection.ExecuteAsync(insertSql, mappedData, transaction: transaction);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
        private async Task<string[]> ReadIccidsToStringArrayAsync()
        {
            string? connString = _configuration["Database:ConnString"];

            string query = "SELECT iccid FROM Iccids";

            List<string> items = new List<string>();

            if (string.IsNullOrEmpty(connString)) //TODO: Make the user aware of this error
                return items.ToArray();

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
            items.RemoveAt(0);
            return items.ToArray();
        }
    }
}
