using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace BETTA
{
    public partial class LoginWindow : Window
    {
        private const string REGISTRY_KEY = @"SOFTWARE\BETTA";
        private const string USERNAME_KEY = "Username";
        private const string PASSWORD_KEY = "Password";
        private const string APPKEY_KEY = "AppKey";

        private BetfairApiClient _apiClient;
        public string SessionToken { get; private set; }
        public string Username { get; private set; }
        public string AppKey { get; private set; }
        public bool LoginSuccessful { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            _apiClient = new BetfairApiClient();
            LoadSavedCredentials();
            KeyDown += LoginWindow_KeyDown;
            CheckServiceHealth();
        }

        private async void CheckServiceHealth()
        {
            bool healthy = await _apiClient.CheckHealthAsync();
            if (!healthy)
                UpdateStatus("Warning: Python service not running.", true);
        }

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                LoginButton_Click(sender, e);
        }

        private void LoadSavedCredentials()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
                if (key != null)
                {
                    UsernameTextBox.Text = key.GetValue(USERNAME_KEY) as string ?? string.Empty;
                    var encPwd = key.GetValue(PASSWORD_KEY) as string ?? string.Empty;
                    PasswordBox.Password = DecryptPassword(encPwd);
                    AppKeyTextBox.Text = key.GetValue(APPKEY_KEY) as string ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading saved credentials: {ex.Message}", true);
            }
        }

        private void SaveCredentials()
        {
            if (RememberCredentialsCheckBox.IsChecked != true) return;

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY);
                key.SetValue(USERNAME_KEY, UsernameTextBox.Text);
                key.SetValue(PASSWORD_KEY, EncryptPassword(PasswordBox.Password));
                key.SetValue(APPKEY_KEY, AppKeyTextBox.Text);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error saving credentials: {ex.Message}", true);
            }
        }

        private string EncryptPassword(string password)
        {
            byte[] data = Encoding.UTF8.GetBytes(password);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private string DecryptPassword(string encryptedPassword)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(encryptedPassword);
                byte[] data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return string.Empty;
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password) ||
                string.IsNullOrWhiteSpace(AppKeyTextBox.Text))
            {
                UpdateStatus("Please fill in all fields", true);
                return;
            }

            SetLoginState(isLoggingIn: true);
            UpdateStatus("Connecting to Betfair via Python service...", false);

            try
            {
                var loginResult = await _apiClient.LoginAsync(
                    UsernameTextBox.Text,
                    PasswordBox.Password,
                    AppKeyTextBox.Text
                );

                if (loginResult.Success && !string.IsNullOrEmpty(loginResult.SessionToken))
                {
                    SessionToken = loginResult.SessionToken;
                    Username = UsernameTextBox.Text;
                    AppKey = AppKeyTextBox.Text;
                    LoginSuccessful = true;

                    UpdateStatus("Login successful!", false);
                    SaveCredentials();

                    await Task.Delay(500);

                    var mainWindow = new MainWindow(_apiClient, SessionToken, Username, AppKey);
                    mainWindow.Show();
                    Close();
                }
                else
                {
                    UpdateStatus($"Login failed: {loginResult.Error}", true);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during login: {ex.Message}", true);
            }
            finally
            {
                SetLoginState(isLoggingIn: false);
            }
        }

        private void SetLoginState(bool isLoggingIn)
        {
            LoginButton.IsEnabled = !isLoggingIn;
            UsernameTextBox.IsEnabled = !isLoggingIn;
            PasswordBox.IsEnabled = !isLoggingIn;
            AppKeyTextBox.IsEnabled = !isLoggingIn;
            RememberCredentialsCheckBox.IsEnabled = !isLoggingIn;

            LoginProgressBar.Visibility = isLoggingIn ? Visibility.Visible : Visibility.Collapsed;
            LoginProgressBar.IsIndeterminate = isLoggingIn;
            LoginButton.Content = isLoggingIn ? "Logging in..." : "Login";
        }

        private void UpdateStatus(string message, bool isError)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = new SolidColorBrush(isError ? Colors.Red : Colors.LightGreen);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Do not set DialogResult here, as this window is not shown as a dialog.
            _apiClient.Dispose();
            base.OnClosed(e);
        }
    }
}
