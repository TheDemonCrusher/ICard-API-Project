using ICard_API_Project.Models;
using Microsoft.Extensions.Configuration;
using System.Configuration;
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

        private async Task<SessionDetails?> GetSessionDetailsAsync(string icid)
        {
            if (icid == String.Empty)
                return null;

            string endpoint = icid + _configuration["Endpoints:SessionDetails"];
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

        private async Task<DeviceUsage?> GetDeviceUsageAsync(string icid)
        {
            if (icid == String.Empty)
                return null;

            string endpoint = icid + _configuration["Endpoints:DeviceUsage"];
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

        private async Task<DeviceLocation?> GetDeviceLocationsAsync(string icid)
        {
            if (icid == String.Empty)
                return null;

            string endpoint = icid + _configuration["Endpoints:DeviceLocation"];

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
                    for (int i = 0; i < 10; i++) // loop through all icids in the icid table, this loop will be a while(reader) or sth probably
                    {
                        CombinedInfo info = new CombinedInfo();

                        string icid = "next id"; // we get the current icid and asign it to the combined model
                        info.icid = icid;
                        info.details = await GetSessionDetailsAsync(icid);
                        info.usage = await GetDeviceUsageAsync(icid);
                        info.location = await GetDeviceLocationsAsync(icid);

                        //write the information to the respective tables        
                    }
                }
                catch (Exception ex)
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
        
    }
}
