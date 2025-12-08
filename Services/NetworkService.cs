using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EasyFlips.Services
{
    public class NetworkService
    {
        private static readonly Lazy<NetworkService> _instance =
       new Lazy<NetworkService>(() => new NetworkService());

        public static NetworkService Instance => _instance.Value;

        public bool IsConnected { get; private set; }

        public event Action<bool> ConnectivityChanged;

        private readonly DispatcherTimer _timer;
        private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        private NetworkService()
        {
            _ = UpdateStatus();

            NetworkChange.NetworkAvailabilityChanged += async (s, e) =>
            {
                // Khi có thay đổi về trạng thái mạng, kiểm tra lại kết nối Internet
                await UpdateStatus();
            };

            //Cứ 15 giây kiểm tra lại trạng thái kết nối
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _timer.Tick += async (s, e) => await UpdateStatus();
            _timer.Start();
        }

        private async Task UpdateStatus()
        {
            bool status = await CheckInternet();
            if (IsConnected != status)
            {
                IsConnected = status;
                ConnectivityChanged?.Invoke(IsConnected);
            }
        }

        // URL dùng để kiểm tra kết nối Internet (Google cung cấp trang này để kiểm tra nhanh)
        private static readonly Uri TestUri = new Uri("http://clients3.google.com/generate_204");

        private async Task<bool> CheckInternet()
        {
            try
            {
                var response = await _client.GetAsync(TestUri);
                return response.StatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch
            {
                return false;
            }
        }

    }
}
