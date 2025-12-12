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

        private NetworkService()
        {
            // [FIX]: Lấy trạng thái sơ bộ ngay lập tức dựa trên phần cứng
            // Giúp UI hiển thị "Online" ngay lập tức, không bị flash "Offline" lúc mới mở
            IsConnected = NetworkInterface.GetIsNetworkAvailable();
        }

        // Hàm này BẮT BUỘC phải được gọi từ App.xaml.cs
        public void Initialize()
        {
            // 1. Chạy kiểm tra HTTP ngay lập tức để xác thực internet thực sự
            _ = UpdateStatus();

            // 2. Đăng ký sự kiện mạng của Windows
            NetworkChange.NetworkAvailabilityChanged += async (s, e) =>
            {
                await UpdateStatus();
            };

            // 3. Khởi tạo Timer kiểm tra định kỳ 15s
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _timer.Tick += async (s, e) => await UpdateStatus();
            _timer.Start();
        }

        private async Task UpdateStatus()
        {
            bool status = await CheckInternet();
            // Chỉ bắn sự kiện nếu trạng thái thực sự thay đổi
            if (IsConnected != status)
            {
                IsConnected = status;
                ConnectivityChanged?.Invoke(IsConnected);
            }
        }

        private static readonly Uri TestUri = new Uri("http://clients3.google.com/generate_204");

        private async Task<bool> CheckInternet()
        {
            // Nếu phần cứng báo không có mạng thì trả về false luôn cho nhanh
            if (!NetworkInterface.GetIsNetworkAvailable()) return false;

            try
            {
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