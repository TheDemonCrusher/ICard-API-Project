using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Text;
using Serilog;

namespace ICard_API_Project
{
    internal static class Program
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public static IConfiguration Configuration { get; private set; }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddXmlFile("App.config", optional: false, reloadOnChange: true)
                .AddXmlFile("App.Development.config", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            var services = new ServiceCollection();

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()            
            .WriteTo.File("logs/app_log_.txt", rollingInterval: RollingInterval.Day
            ,outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

            // Registering configuration so forms can use it if needed
            services.AddSingleton(Configuration);

            services.AddHttpClient("ICardApiClient", client =>
            {
                string baseAddress = Configuration["ApiSettings:BaseAddress"];
                if (!string.IsNullOrEmpty(baseAddress))
                {
                    client.BaseAddress = new Uri(baseAddress);
                }

                client.DefaultRequestHeaders.Add("Accept", "application/json");

                //Setting up the basic auth needed for the api requests
                string username = Configuration["ApiSettings:Username"];
                string apiKey = Configuration["ApiSettings:ApiKey"];

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(apiKey))
                {
                    string credentials = $"{username}:{apiKey}";
                    byte[] bytes = Encoding.UTF8.GetBytes(credentials);
                    string base64Credentials = Convert.ToBase64String(bytes);

                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", base64Credentials);
                }

                client.Timeout = TimeSpan.FromSeconds(15);
            });

            services.AddTransient<Form1>();

            ServiceProvider = services.BuildServiceProvider();

            var mainForm = ServiceProvider.GetRequiredService<Form1>();
            Application.Run(mainForm);
        }
    }
}