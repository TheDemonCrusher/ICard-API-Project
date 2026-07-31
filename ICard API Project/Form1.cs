using Microsoft.Extensions.Configuration;
using System.Configuration;

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
            sendGetRequest(endpoint);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == String.Empty)
            {
                MessageBox.Show("You must enter a device id first!");
                return;
            }
            string endpoint = textBox1.Text + _configuration["Endpoints:DeviceUsage"];
            sendGetRequest(endpoint);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == String.Empty)
            {
                MessageBox.Show("You must enter a device id first!");
                return;
            }
            string endpoint = textBox1.Text + _configuration["Endpoints:DeviceLocation"];
            sendGetRequest(endpoint);
        }


        private async void sendGetRequest(string endpoint)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ICardApiClient");

                HttpResponseMessage response = await client.GetAsync(endpoint);

                response.EnsureSuccessStatusCode();

                string jsonResult = await response.Content.ReadAsStringAsync();
                MessageBox.Show(jsonResult, "API Response");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error calling API: " + ex.Message);
            }
        }
    }
}
