using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace BETTA.Views
{
    public class RaceData
    {
        public string race_info { get; set; }
        public string race_info_with_status { get; set; }
        public string venue { get; set; }
        public int color_index { get; set; }
        public string market_id { get; set; }
        public string market_name { get; set; }
        public string start_time { get; set; }
        public string event_name { get; set; }
        public double time_to_start_minutes { get; set; }
        public string race_status { get; set; }
        public string status_color { get; set; }
    }

    public partial class MainWindow : Window
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

        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _clockTimer;
        private List<RaceData> _currentRaces;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimers();
            _ = RefreshMarkets();
        }

        private void InitializeTimers()
        {
            // Clock timer - updates every second
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            // Refresh timer - variable interval based on race proximity
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            var ukTime = DateTime.Now; // System time should be UK time
            CurrentTimeDisplay.Text = ukTime.ToString("HH:mm:ss");
            CurrentTimeLarge.Text = ukTime.ToString("dddd, dd MMMM yyyy HH:mm:ss");

            // Update time-to-start displays
            UpdateTimeToStartDisplays();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await RefreshMarkets();
        }

        private void UpdateTimeToStartDisplays()
        {
            if (_currentRaces == null) return;

            var now = DateTime.Now;
            foreach (var race in _currentRaces)
            {
                // This would need to be calculated properly with the race start time
                // For now, we'll refresh the data to get updated times
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) =>
            await RefreshMarkets();

        private async Task RefreshMarkets()
        {
            StatusText.Text = "Loading markets…";
            BtnRefresh.IsEnabled = false;

            try
            {
                var res = await DevHttp.GetAsync("http://127.0.0.1:5000/data/horse-markets");
                res.EnsureSuccessStatusCode();

                var json = await res.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);

                var markets = data["markets"]?.ToObject<List<RaceData>>();
                _currentRaces = markets;
                RaceList.ItemsSource = markets;

                // Update current time from server
                var serverTime = data["current_time_uk"]?.Value<string>();
                if (!string.IsNullOrEmpty(serverTime))
                {
                    CurrentTimeLarge.Text = serverTime;
                }

                StatusText.Text = $"Loaded {markets?.Count ?? 0} WIN markets.";

                // Set dynamic refresh interval based on closest race
                SetDynamicRefreshInterval(markets);
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

        private void SetDynamicRefreshInterval(List<RaceData> races)
        {
            if (races == null || !races.Any())
            {
                _refreshTimer.Stop();
                NextRefreshInfo.Text = "";
                return;
            }

            // Find the closest race that hasn't started
            var closestRace = races.Where(r => r.time_to_start_minutes > 0)
                                  .OrderBy(r => r.time_to_start_minutes)
                                  .FirstOrDefault();

            if (closestRace == null)
            {
                // No upcoming races, use long interval
                _refreshTimer.Interval = TimeSpan.FromMinutes(5);
                NextRefreshInfo.Text = "Next refresh: 5 minutes";
                _refreshTimer.Start();
                return;
            }

            var timeToStart = closestRace.time_to_start_minutes;

            if (timeToStart < 2) // Less than 2 minutes - refresh every 10 seconds
            {
                _refreshTimer.Interval = TimeSpan.FromSeconds(10);
                NextRefreshInfo.Text = "Next refresh: 10 seconds (race imminent)";
            }
            else if (timeToStart < 5) // 2-5 minutes - refresh every 30 seconds
            {
                _refreshTimer.Interval = TimeSpan.FromSeconds(30);
                NextRefreshInfo.Text = "Next refresh: 30 seconds (race soon)";
            }
            else if (timeToStart < 10) // 5-10 minutes - refresh every 1 minute
            {
                _refreshTimer.Interval = TimeSpan.FromMinutes(1);
                NextRefreshInfo.Text = "Next refresh: 1 minute";
            }
            else // More than 10 minutes - refresh every 2 minutes
            {
                _refreshTimer.Interval = TimeSpan.FromMinutes(2);
                NextRefreshInfo.Text = "Next refresh: 2 minutes";
            }

            _refreshTimer.Start();
        }

        private void RaceList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RaceList.SelectedItem is RaceData selectedRace)
            {
                var raceForm = new RaceForm(selectedRace);
                raceForm.Show();
            }
        }
    }
}
