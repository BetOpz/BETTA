using System;
using System.Threading.Tasks;
using System.Windows;
using BETTA.Services;

namespace BETTA.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;
            txtStatus.Text = "Logging in…";

            try
            {
                var username = txtUsername.Text;
                var password = txtPassword.Password;
                var appKey = txtAppKey.Text;

                App.ApiClient.SetAppKey(appKey);
                var result = await App.ApiClient.LoginAsync(username, password);

                // Optionally store the token for future use
                // App.ApiClient.SetBearerToken(result.Token);

                // Open main window on success
                var main = new MainWindow();
                main.Show();
                Close();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Login failed: {ex.Message}";
                btnLogin.IsEnabled = true;
            }
        }
    }
}
