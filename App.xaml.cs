using System.Windows;
using BETTA.Services;

namespace BETTA
{
    public partial class App : Application
    {
        // Point to your local Flask service
        public static readonly ApiClient ApiClient =
            new ApiClient("http://127.0.0.1:5000");
    }
}
