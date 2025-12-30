using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Models;
using EasyFlips.Services;
using EasyFlips.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System;

namespace EasyFlips.ViewModels
{
    public partial class CreateRoomViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IDeckRepository _deckRepository;
        private readonly IClassroomRepository _classroomRepository;
        private readonly IAuthService _authService;
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _userSession;

        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();

        [ObservableProperty] private Deck _selectedDeck;
        [ObservableProperty] private int _maxPlayers = 30;
        [ObservableProperty] private int _timePerRound = 15;       
        [ObservableProperty] private int _waitTimeMinutes = 5;


        public CreateRoomViewModel(
            INavigationService navigationService,
            IDeckRepository deckRepository,
            IClassroomRepository classroomRepository,
            IAuthService authService,
            SupabaseService supabaseService,
            UserSession userSession)
        {
            _navigationService = navigationService;
            _deckRepository = deckRepository;
            _classroomRepository = classroomRepository;
            _authService = authService;
            _userSession = userSession;
            _supabaseService = supabaseService; 

            LoadDecks();
        }

        private async void LoadDecks()
        {
            try
            {
                var decks = await _deckRepository.GetAllAsync();
                AvailableDecks.Clear();
                foreach (var deck in decks) AvailableDecks.Add(deck);

               
                SelectedDeck = AvailableDecks.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load deck list: {ex.Message}", "Error");
            }
        }
       
        [RelayCommand]
        private async Task ConfirmCreateRoom()
        {
            if (SelectedDeck == null)
            {
                MessageBox.Show("Please select a deck to teach!", "Notification");
                return;
            }
            var hostId = _userSession.UserId; 
          
            var random = new Random();
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

            string roomId = new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());

            try
            {
                
                if (_supabaseService.Client.Auth.CurrentSession != null)
                {
                    await _supabaseService.Client.Auth.RefreshSession();
                }

                var newRoom = new Classroom
                {
                    RoomCode = roomId,
                    HostId = _authService.CurrentUserId,
                    DeckId = SelectedDeck.Id,
                    MaxPlayers = MaxPlayers,
                    Status = "WAITING",
                    TimePerRound = TimePerRound,
                    IsActive = true,
                    Name = $"Classroom {roomId}",
                    WaitTime = WaitTimeMinutes * 60,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                if (newRoom != null)
                {
                    // Thêm Host vào danh sách thành viên với role 'owner'
                    //await _supabaseService.AddMemberAsync(newRoom.Id, hostId, "owner");

                  
                    await _classroomRepository.CreateClassroomAsync(newRoom);

                    await _navigationService.ShowHostLobbyWindowAsync(newRoom.RoomCode);
                }
                else
                {
                    MessageBox.Show("Error creating room, please try again.", "Error");
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}\nInner Exception: {ex.InnerException?.Message}", "Supabase Error");
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseWindow();
        }


        private void CloseWindow()
        {
            Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
        }
    }
}