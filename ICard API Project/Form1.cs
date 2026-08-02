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

        public Form1(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            InitializeComponent();
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            
            if (textBox1.Text == String.Empty)
            {
                MessageBox.Show("You must enter a device id first!");
                return;
            }
            string endpoint = textBox1.Text + _configuration["Endpoints:SessionDetails"];
            string jsonResult = await sendGetRequest(endpoint);

            SessionDetails? details = JsonSerializer.Deserialize<SessionDetails>(jsonResult);
            int a = 0;
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == String.Empty)
            {
                MessageBox.Show("You must enter a device id first!");
                return;
            }
            string endpoint = textBox1.Text + _configuration["Endpoints:DeviceUsage"];
            string jsonResult = await sendGetRequest(endpoint);
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == String.Empty)
            {
                MessageBox.Show("You must enter a device id first!");
                return;
            }
            string endpoint = textBox1.Text + _configuration["Endpoints:DeviceLocation"];
            string jsonResult = await sendGetRequest(endpoint);

            DeviceLocation? locations = JsonSerializer.Deserialize<DeviceLocation>(jsonResult);
            int a = 0;
        }


        private async Task<string> sendGetRequest(string endpoint)
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
                MessageBox.Show("Error calling API: " + ex.Message);
                return null;
            }
        }
    }
}
