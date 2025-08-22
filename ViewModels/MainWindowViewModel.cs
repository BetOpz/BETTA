using BETTA.Models;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BETTA.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private MarketInfo _selectedMarket;
        private bool _isLoading;

        public ObservableCollection<MarketInfo> Markets { get; } = new ObservableCollection<MarketInfo>();

        public MarketInfo SelectedMarket
        {
            get => _selectedMarket;
            set
            {
                if (_selectedMarket != value)
                {
                    _selectedMarket = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand RefreshCommand { get; }

        public MainWindowViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadMarketsAsync(), () => !_isLoading);
            _ = LoadMarketsAsync();
        }

        private async Task LoadMarketsAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                var response = await _httpClient.GetStringAsync("https://api.yourdomain.com/data/horse-markets");
                var result = JsonConvert.DeserializeObject<MarketResponse>(response);
                Markets.Clear();
                if (result?.Markets != null)
                {
                    foreach (var m in result.Markets)
                        Markets.Add(m);
                }
                SelectedMarket = Markets.Count > 0 ? Markets[0] : null;
            }
            finally
            {
                _isLoading = false;
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private class MarketResponse
        {
            [JsonProperty("markets")]
            public MarketInfo[] Markets { get; set; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
