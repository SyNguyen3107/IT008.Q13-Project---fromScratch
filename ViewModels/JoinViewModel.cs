using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
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

        public JoinViewModel(INavigationService navigationService, IClassroomRepository classroomRepository)
        {
            _navigationService = navigationService;
            _classroomRepository = classroomRepository;
        }

        [RelayCommand]
        private void OpenCreateRoomWindow()
        {
            // Chuyển sang màn hình tạo phòng
            _navigationService.ShowCreateRoomWindow();
            CloseWindow();
        }

        [RelayCommand]
        private async Task JoinRoom()
        {
            try
            {
                string code = RoomIdInput?.ToUpper().Trim();

                if (string.IsNullOrWhiteSpace(code) || code.Length < 4)
                {
                    MessageBox.Show("Please enter a valid Room Code (e.g., ABC123).", "Input Error");
                    return;
                }

                // 1. Kiểm tra phòng trên Database
                var room = await _classroomRepository.GetClassroomByCodeAsync(code);

                if (room == null)
                {
                    MessageBox.Show("Room not found or invalid code!", "Error");
                    return;
                }

                if (room.Status == "CLOSED")
                {
                    MessageBox.Show("This room has been closed.", "Notification");
                    return;
                }

                if (room.Status == "PLAYING")
                {
                    MessageBox.Show("The game is already in progress. You cannot join at this time.", "Oops!");
                    return;
                }

                // 2. Hợp lệ -> Vào Lobby
                // Đóng cửa sổ nhập mã trước khi mở Lobby để tránh chồng chéo
                CloseWindow();

                await _navigationService.ShowMemberLobbyWindowAsync(code);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking room status: {ex.Message}", "Connection Error");
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
                    // Tìm cửa sổ đang sử dụng ViewModel này và đóng nó
                    var window = Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.DataContext == this);

                    window?.Close();
                }
                catch (Exception)
                {
                    // Bỏ qua lỗi đóng cửa sổ (nếu có)
                }
            });
        }
    }
}