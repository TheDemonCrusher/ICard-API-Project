using Dapper;
using ICard_API_Project.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Serilog;
using ICard_API_Project.Exceptions;

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
            catch (JsonException jsonEx)
            {
                Log.Error(jsonEx, "Failed to deserialize Session Details for ICCID: {Iccid}. Raw JSON: {Json}", iccid, jsonResult);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unexpected error occurred while parsing Session Details for ICCID: {Iccid}", iccid);
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
            catch (JsonException jsonEx)
            {
                Log.Error(jsonEx, "Failed to deserialize Device Usage for ICCID: {Iccid}. Raw JSON: {Json}", iccid, jsonResult);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unexpected error occurred while parsing Device Usage for ICCID: {Iccid}", iccid);
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

                try
                {
                    if (jsonResult != null)
                        currentPage = JsonSerializer.Deserialize<DeviceLocation>(jsonResult);
                }
                catch (JsonException jsonEx)
                {
                    Log.Error(jsonEx, "Failed to deserialize Device Location on page {PageNum} for ICCID: {Iccid}. Raw JSON: {Json}", locations.pageNumber, iccid, jsonResult);
                    return null;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An unexpected error occurred while parsing Device Location on page {PageNum} for ICCID: {Iccid}", locations.pageNumber, iccid);
                    return null;
                }
                

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
                Log.Warning(ex, "Hit API rate limit. Try lowering MaxDegreeOfParallelism.");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Log.Warning(ex, "API request failed with status code: {StatusCode}", ex.StatusCode);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Log.Warning(ex, "API request timed out.");
                return null;
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "API returned invalid JSON data.");
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
                try
                {
                    if (choice == DialogResult.Yes)
                        await UpdateDatabaseWithIccidsAsync(firstColumn);
                    if (choice == DialogResult.No)
                        await OverwriteDatabaseWithIccidsAsync(firstColumn);

                    Log.Information("ICCID database sync completed successfully.");
                    MessageBox.Show("Sync complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (DatabaseSyncException dbEx)
                {
                    Log.Error(dbEx, "Database sync failed during user-initiated update.");
                    MessageBox.Show("Failed to save to the database. Check the logs.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "An unexpected application error occurred during ICCID sync.");
                    MessageBox.Show("A critical error occurred.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null)
                return;

            if (!ValidateDatabaseConnection())
                return;

            Log.Information("Auto updates started.");
            startBtn.Enabled = false;
            stopBtn.Enabled = true;

            _cancellationTokenSource = new CancellationTokenSource();
            _ = RunApiBackgroundLoopAsync(_cancellationTokenSource.Token);
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                Log.Information("Auto updates stopped.");
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
                    {
                        try
                        {
                            await UpdateDatabaseWithDeviceUsagesAsync(allUsages.ToList());
                            Log.Information("Successfully updated {Count} Device Usages.", allUsages.Count);
                        }
                        catch (DatabaseSyncException dbEx)
                        {
                            Log.Error(dbEx, "Bulk update failed for Device Usages.");
                        }
                    }

                    if (!allSessions.IsEmpty)
                    {
                        try
                        {
                            await UpdateDatabaseWithSessionInfoAsync(allSessions.ToList());
                            Log.Information("Successfully updated {Count} Session Details.", allSessions.Count);
                        }
                        catch (DatabaseSyncException dbEx)
                        {
                            Log.Error(dbEx, "Bulk update failed for Session Details.");
                        }
                    }

                    if (!allLocations.IsEmpty)
                    {
                        try
                        {
                            await UpdateDatabaseWithDeviceLocationsAsync(allLocations.ToList());
                            Log.Information("Successfully updated {Count} Device Locations.", allLocations.Count);
                        }
                        catch (DatabaseSyncException dbEx)
                        {
                            Log.Error(dbEx, "Bulk update failed for Device Locations.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An unexpected error occurred while parsing Device Location on page {PageNum} for ICCID: {Iccid}", locations.pageNumber, iccid);
                    break;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), token);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An unexpected error occurred while parsing Device Location on page {PageNum} for ICCID: {Iccid}", locations.pageNumber, iccid);
                    break;
                }
            }
        }

        private async Task UpdateDatabaseWithDeviceUsagesAsync(List<DeviceUsage> dataList)
        {
            string? connString = _configuration["Database:ConnString"];

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

            Log.Information("Iccid table update started...");
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

            Log.Information("Iccid table overwrite started...");
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

        private bool ValidateDatabaseConnection()
        {
            string? connString = _configuration["Database:ConnString"];

            if (string.IsNullOrWhiteSpace(connString))
            {
                Log.Error("Database connection string is missing from the configuration file.");
                MessageBox.Show("The database connection string is missing. Please configure it before starting.",
                                "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                using (var conn = new SqlConnection(connString))
                {
                    Log.Information("Testing database connection...");
                    conn.Open();
                    return true;
                }
            }
            catch (ArgumentException argEx)
            {
                Log.Error(argEx, "The database connection string format is invalid.");
                MessageBox.Show("The connection string format is invalid. Please check for typos.",
                                "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (SqlException sqlEx)
            {
                Log.Error(sqlEx, "Failed to connect to the database. Server may be offline or credentials are bad.");
                MessageBox.Show($"Could not connect to the database. Please check your network and credentials.\n\nDetails: {sqlEx.Message}",
                                "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unexpected error occurred while validating the database connection.");
                MessageBox.Show("An unexpected error occurred while checking the database.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
