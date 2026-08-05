using ICard_API_Project.Models;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.IO;
using System.Text;
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
            //int a = 0;
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

            DeviceUsage? usage = JsonSerializer.Deserialize<DeviceUsage>(jsonResult);
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == String.Empty)
            {
                MessageBox.Show("You must enter a device id first!");
                return;
            }
            string endpoint = textBox1.Text + _configuration["Endpoints:DeviceLocation"];

            DeviceLocation locations = new DeviceLocation();

            while (locations.lastPage == false) //Multiple pages not tested yet
            {
                string jsonResult = await sendGetRequest(endpoint + $"?pageNumber={locations.pageNumber}");
                DeviceLocation? currentPage = null;

                if (jsonResult != null)
                    currentPage = JsonSerializer.Deserialize<DeviceLocation>(jsonResult);

                if (currentPage != null)
                    locations.AddNextPage(currentPage);
                else
                    locations.lastPage = true;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
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

                // Example output: verify how many items were read
                MessageBox.Show($"Successfully read {firstColumn.Count} items from column 1.");
            }
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
