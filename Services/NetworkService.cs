using System;
using System.Net.Http;
using System.Net.NetworkInformation;
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

        private DispatcherTimer _timer;
        private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        // Constructor: Chỉ khởi tạo biến, KHÔNG chạy logic nặng
        private NetworkService()
        {
        }

        // Hàm này sẽ được gọi thủ công từ App.xaml.cs
        public void Initialize()
        {
            // 1. Chạy kiểm tra ngay lập tức (Fire and forget an toàn ở đây)
            _ = UpdateStatus();

            // 2. Đăng ký sự kiện mạng
            NetworkChange.NetworkAvailabilityChanged += async (s, e) =>
            {
                await UpdateStatus();
            };

            // 3. Khởi tạo Timer (Cần chạy trên UI Thread, Initialize được gọi từ OnStartup nên an toàn)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _timer.Tick += async (s, e) => await UpdateStatus();
            _timer.Start();
        }

        private async Task UpdateStatus()
        {
            bool status = await CheckInternet();
            // Chỉ bắn sự kiện nếu trạng thái thay đổi
            if (IsConnected != status)
            {
                IsConnected = status;
                ConnectivityChanged?.Invoke(IsConnected);
            }
        }

        private static readonly Uri TestUri = new Uri("http://clients3.google.com/generate_204");

        private async Task<bool> CheckInternet()
        {
            try
            {
                // Thêm cấu hình Header để tránh bị một số server chặn bot
                using (var request = new HttpRequestMessage(HttpMethod.Get, TestUri))
                {
                    request.Headers.Add("User-Agent", "EasyFlips-App");
                    var response = await _client.SendAsync(request);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}