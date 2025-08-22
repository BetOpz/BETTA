using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace BETTA.Views
{
    public class RunnerData
    {
        public int selection_id { get; set; }
        public string profit_loss { get; set; } = "";
        public string name { get; set; }
        public double? back_price { get; set; }
        public double? lay_price { get; set; }
        public string status { get; set; }
        public double? last_price_traded { get; set; }
        public double total_matched { get; set; }
        public string optimum { get; set; } = "";
        public string bets { get; set; } = "";
    }

    public partial class RaceForm : Window
    {
#if DEBUG
        private static readonly HttpClient DevHttp =
            new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
#else
        private static readonly HttpClient DevHttp = new HttpClient();
#endif

        private readonly RaceData _raceData;

        public RaceForm(RaceData raceData)
        {
            InitializeComponent();
            _raceData = raceData;

            // Set race header information
            RaceTitle.Text = $"{_raceData.venue} - {_raceData.market_name ?? "Race Details"}";
            RaceSubtitle.Text = $"{_raceData.start_time ?? "Loading..."} • Market ID: {_raceData.market_id ?? "N/A"}";

            // Load market details
            _ = LoadMarketDetails();
        }

        private double CalculateBookPercentage(List<RunnerData> runners, Func<RunnerData, double?> priceSelector)
        {
            double totalPercentage = 0.0;
            int validPrices = 0;

            foreach (var runner in runners)
            {
                var price = priceSelector(runner);
                if (price.HasValue && price.Value > 1.0) // Valid betting price
                {
                    // Book percentage = (1 / decimal odds) * 100
                    totalPercentage += (1.0 / price.Value) * 100.0;
                    validPrices++;
                }
            }

            return validPrices > 0 ? totalPercentage : 0.0;
        }

        private void UpdateBookPercentages(List<RunnerData> runners)
        {
            // Calculate Back Book %
            var backBook = CalculateBookPercentage(runners, r => r.back_price);
            BackBookPercent.Text = $"{backBook:F1}%";

            // Color code based on book percentage
            if (backBook > 120)
                BackBookPercent.Foreground = System.Windows.Media.Brushes.Red;
            else if (backBook > 105)
                BackBookPercent.Foreground = System.Windows.Media.Brushes.Orange;
            else if (backBook > 95)
                BackBookPercent.Foreground = System.Windows.Media.Brushes.Green;
            else
                BackBookPercent.Foreground = System.Windows.Media.Brushes.DarkBlue;

            // Calculate Lay Book %
            var layBook = CalculateBookPercentage(runners, r => r.lay_price);
            LayBookPercent.Text = $"{layBook:F1}%";

            // Color code lay book
            if (layBook > 120)
                LayBookPercent.Foreground = System.Windows.Media.Brushes.Red;
            else if (layBook > 105)
                LayBookPercent.Foreground = System.Windows.Media.Brushes.Orange;
            else if (layBook > 95)
                LayBookPercent.Foreground = System.Windows.Media.Brushes.Green;
            else
                LayBookPercent.Foreground = System.Windows.Media.Brushes.DarkRed;

            // Calculate Last Traded Book %
            var lastBook = CalculateBookPercentage(runners, r => r.last_price_traded);
            LastBookPercent.Text = $"{lastBook:F1}%";

            // Color code last traded book
            if (lastBook > 120)
                LastBookPercent.Foreground = System.Windows.Media.Brushes.Red;
            else if (lastBook > 105)
                LastBookPercent.Foreground = System.Windows.Media.Brushes.Orange;
            else if (lastBook > 95)
                LastBookPercent.Foreground = System.Windows.Media.Brushes.Green;
            else
                LastBookPercent.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }

        private async Task LoadMarketDetails()
        {
            StatusText.Text = "Loading runners and odds...";

            try
            {
                if (string.IsNullOrEmpty(_raceData.market_id))
                {
                    StatusText.Text = "Error: No market ID available";
                    return;
                }

                var url = $"http://127.0.0.1:5000/data/market-details/{_raceData.market_id}";
                var res = await DevHttp.GetAsync(url);

                var json = await res.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);

                if (data["success"]?.Value<bool>() == true)
                {
                    var runners = data["runners"]?.ToObject<List<RunnerData>>();
                    var nonRunnerCount = data["non_runner_count"]?.Value<int>() ?? 0;

                    // Initialize columns with placeholder values
                    if (runners != null)
                    {
                        foreach (var runner in runners)
                        {
                            runner.profit_loss = "£0.00";
                            runner.optimum = "TBC";
                            runner.bets = "-";
                        }
                    }

                    RunnersGrid.ItemsSource = runners;

                    var totalMatched = data["total_matched"]?.Value<double>() ?? 0;
                    var statusMessage = data["status_message"]?.Value<string>() ?? "";
                    var inPlay = data["in_play"]?.Value<bool>() ?? false;

                    // Update title with race status
                    RaceTitle.Text = $"{_raceData.venue} - {_raceData.market_name}{statusMessage}";

                    // Update status with non-runner info
                    var runnerCount = runners?.Count ?? 0;
                    var statusText = $"Loaded {runnerCount} runners • Total matched: £{totalMatched:N0}";

                    if (nonRunnerCount > 0)
                    {
                        statusText += $" • {nonRunnerCount} Non-Runner{(nonRunnerCount > 1 ? "s" : "")}";
                    }

                    StatusText.Text = statusText;

                    // Show in-play warning if race has started
                    if (inPlay)
                    {
                        StatusText.Text += " • Race is IN-PLAY";
                    }

                    // Calculate and display book percentages (only for active runners)
                    if (runners != null && runners.Any())
                    {
                        UpdateBookPercentages(runners);
                    }
                }
                else
                {
                    var errorMsg = data["error"]?.Value<string>();
                    var userMessage = data["user_message"]?.Value<string>();
                    var marketStatus = data["market_status"]?.Value<string>();

                    // Handle specific error cases
                    if (marketStatus == "CLOSED")
                    {
                        StatusText.Text = "This race has finished";
                        MessageBox.Show($"Race Finished\n\n{userMessage ?? "This race has ended and betting is closed."}",
                                       "Race Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (marketStatus == "SUSPENDED")
                    {
                        StatusText.Text = "Market is suspended";
                        MessageBox.Show($"Market Suspended\n\n{userMessage ?? "This market is temporarily unavailable."}",
                                       "Market Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else if (errorMsg?.Contains("Not logged in") == true)
                    {
                        StatusText.Text = "Please log in first";
                        MessageBox.Show("Please log in to Betfair first using the main window login.", "Login Required");
                    }
                    else
                    {
                        StatusText.Text = $"Error: {errorMsg}";
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                StatusText.Text = "Flask service not available";
                MessageBox.Show($"Cannot connect to Flask service:\n{httpEx.Message}\n\n" +
                               "Make sure the service is running on http://127.0.0.1:5000",
                               "Connection Error");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading data: {ex.Message}";
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadMarketDetails();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
