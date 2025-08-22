using System;
using System.Windows;

namespace BETTA.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            // Check the bool property correctly
            if (Properties.Settings.Default.RememberCredentials)
            {
                // Assign strings to string properties
                UserBox.Text = Properties.Settings.Default.Username ?? "";
                PassBox.Password = Properties.Settings.Default.Password ?? "";
                AppKeyBox.Password = Properties.Settings.Default.AppKey ?? "";
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.ApiClient.SetAppKey(AppKeyBox.Password);

                var result = await App.ApiClient.LoginAsync(
                    UserBox.Text,
                    PassBox.Password);

                if (!result.Success)
                    throw new Exception("Login failed");

                // Check the bool property correctly
                if (Properties.Settings.Default.RememberCredentials)
                {
                    // Save strings to string properties
                    Properties.Settings.Default.Username = UserBox.Text;
                    Properties.Settings.Default.Password = PassBox.Password;
                    Properties.Settings.Default.AppKey = AppKeyBox.Password;
                }
                else
                {
                    // Clear string properties with empty strings
                    Properties.Settings.Default.Username = "";
                    Properties.Settings.Default.Password = "";
                    Properties.Settings.Default.AppKey = "";
                }

                Properties.Settings.Default.Save();

                new MainWindow().Show();
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
        }
    }
}
