using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System;

namespace EasyFlips.ViewModels
{
    public partial class TestRealtimeViewModel : ObservableObject
    {
        private readonly RealtimeService _realtimeService;

        [ObservableProperty]
        private string roomId = "TEST_ROOM_01";

        [ObservableProperty]
        private string messageInput = "Hello from WPF";

        // Danh sách Log hiển thị trên màn hình
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public TestRealtimeViewModel()
        {
            _realtimeService = new RealtimeService();

            // Đăng ký nhận tin nhắn
            _realtimeService.OnMessageReceived += (eventName, data) =>
            {
                // Bắt buộc dùng Dispatcher để cập nhật UI từ luồng khác
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 📩 NHẬN ({eventName}): {data}");
                });
            };
        }

        [RelayCommand]
        private async Task Connect()
        {
            try
            {
                Logs.Insert(0, "⏳ Đang kết nối...");
                await _realtimeService.ConnectAsync();

                Logs.Insert(0, $"⏳ Đang vào phòng {RoomId}...");
                await _realtimeService.JoinRoomAsync(RoomId);

                Logs.Insert(0, "✅ KẾT NỐI THÀNH CÔNG! (Đang lắng nghe...)");
            }
            catch (Exception ex)
            {
                Logs.Insert(0, $"❌ Lỗi kết nối: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Send()
        {
            try
            {
                // Giả lập gói tin game
                var payload = new
                {
                    user = "User_" + Guid.NewGuid().ToString().Substring(0, 4),
                    text = MessageInput,
                    time = DateTime.Now.ToString("HH:mm:ss")
                };

                await _realtimeService.SendMessageAsync("chat_msg", payload);

                Logs.Insert(0, $"📤 ĐÃ GỬI: {MessageInput}");
                MessageInput = "";
            }
            catch (Exception ex)
            {
                Logs.Insert(0, $"❌ Lỗi gửi: {ex.Message}");
            }
        }
    }
}