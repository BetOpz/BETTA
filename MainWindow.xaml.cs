using System;
using System.Threading.Tasks;
using System.Windows;

namespace BETTA
{
    public partial class MainWindow : Window
    {
        private readonly BetfairApiClient _apiClient;
        private readonly string _sessionToken;
        private readonly string _username;
        private readonly string _appKey;

        public MainWindow(BetfairApiClient apiClient, string sessionToken, string username, string appKey)
        {
            InitializeComponent();
            _apiClient = apiClient;
            _sessionToken = sessionToken;
            _username = username;
            _appKey = appKey;
            Title = $"BETTA - Logged in as {_username}";

            // Subscribe to Loaded event
            this.Loaded += MainWindow_Loaded;
        }

        // Await load in event handler after UI is ready
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAccountInfo();
        }

        private async Task LoadAccountInfo()
        {
            try
            {
                var acc = await _apiClient.GetAccountInfoAsync();
                if (acc.Success && acc.Account != null)
                {
                    Title = $"BETTA - {_username} (Balance: {acc.Account.AvailableBalance:C})";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading account info: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var ans = MessageBox.Show("Are you sure you want to logout?",
                                      "Logout Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Yes)
            {
                try
                {
                    await _apiClient.LogoutAsync();
                }
                catch
                {
                    // ignore logout errors
                }
                Application.Current.Shutdown();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _apiClient.Dispose();
            base.OnClosed(e);
        }
    }
}
