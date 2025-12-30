using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class JoinViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IClassroomRepository _classroomRepository;

        [ObservableProperty]
        private string _roomIdInput;

        [ObservableProperty]
        private string _roomCode;

        public JoinViewModel(INavigationService navigationService, IClassroomRepository classroomRepository)
        {
            _navigationService = navigationService;
            _classroomRepository = classroomRepository;
        }

        [RelayCommand]
        private void OpenCreateRoomWindow()
        {
            _navigationService.ShowCreateRoomWindow();
            CloseWindow();
        }

        [RelayCommand]
        private async Task JoinRoom()
        {
            string code = RoomIdInput?.ToUpper().Trim();

            if (string.IsNullOrWhiteSpace(code) || code.Length < 4)
            {
                MessageBox.Show("Vui lòng nhập mã phòng hợp lệ (ví dụ: ABC123)", "Lỗi nhập liệu");
                return;
            }

            try
            {
                // 1. Kiểm tra phòng trên Database
                var room = await _classroomRepository.GetClassroomByCodeAsync(code);

                // 2. Validate các trường hợp
                if (room == null)
                {
                    MessageBox.Show("Phòng không tồn tại hoặc sai mã!", "Lỗi");
                    return;
                }

                if (room.Status == "CLOSED")
                {
                    MessageBox.Show("Phòng này đã bị đóng.", "Thông báo");
                    return;
                }

                if (room.Status == "PLAYING")
                {
                    MessageBox.Show("Trò chơi đã bắt đầu, bạn không thể vào lúc này.", "Tiếc quá");
                    return;
                }

                // 3. Hợp lệ -> Vào Lobby
                // [CẬP NHẬT] Truyền thêm room.Id (UUID) vào tham số thứ 2
                await _navigationService.ShowMemberLobbyWindowAsync(code);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi kiểm tra phòng: {ex.Message}", "Lỗi kết nối");
            }
        }

        [RelayCommand]
        private void CloseWindow()
        {
            if (Application.Current == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var window = Application.Current.Windows
                        .OfType<Window>()
                        .ToList()
                        .FirstOrDefault(w => w.DataContext == this);

                    window?.Close();
                }
                catch (Exception)
                {
                }
            });
        }
    }
}