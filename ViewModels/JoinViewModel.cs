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
                MessageBox.Show("Please enter a valid Room Code (e.g., ABC123).", "Input Error");
                return;
            }

            try
            {
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