using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace BETTA.Views
{
    public partial class MainWindow
    {
#if DEBUG
        private static readonly HttpClient DevHttps =
            new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
#else
        private static readonly HttpClient DevHttps = new HttpClient();
#endif

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Loading markets…";
            BtnRefresh.IsEnabled = false;

            try
            {
                // Same host/port as Flask, HTTPS if you proxy through SSL
                var res = await DevHttps.GetAsync(
                    "http://127.0.0.1:5000/data/horse-markets");

                res.EnsureSuccessStatusCode();
                var json = await res.Content.ReadAsStringAsync();

                var obj = JObject.Parse(json);
                MarketGrid.ItemsSource = obj["markets"]?.ToObject<object[]>();
                StatusText.Text = $"Loaded {MarketGrid.Items.Count} markets.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Refresh failed: {ex.Message}";
            }
            finally
            {
                BtnRefresh.IsEnabled = true;
            }
        }
    }
}
