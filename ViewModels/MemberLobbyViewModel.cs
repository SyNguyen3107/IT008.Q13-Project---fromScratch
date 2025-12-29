using CommunityToolkit.Mvvm.Input;
using EasyFlips.Models;
using EasyFlips.Services;
using EasyFlips.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class MemberLobbyViewModel : BaseLobbyViewModel
    {
        public MemberLobbyViewModel(
            IAuthService authService,
            INavigationService navigationService,
            UserSession userSession,
            IClassroomRepository classroomRepository,
            SupabaseService supabaseService,
            AudioService audioService)
            : base(authService, navigationService, userSession, classroomRepository, supabaseService, audioService)
        {
        }

        protected override async Task OnInitializeSpecificAsync(Classroom roomInfo)
        {
            var myId = _authService.CurrentUserId ?? _userSession.UserId;

            await _supabaseService.AddMemberAsync(_realClassroomIdUUID, myId);

            var updatedUtc = DateTime.SpecifyKind(roomInfo.UpdatedAt, DateTimeKind.Utc);
            var elapsed = (int)(DateTime.Now - updatedUtc).TotalSeconds;
            AutoStartSeconds = Math.Max(roomInfo.WaitTime - elapsed, 0);

            if (AutoStartSeconds > 0)
            {
                IsAutoStartActive = true;
                _autoStartTimer.Start();
            }
            else
            {

                MessageBox.Show("Waiting time is over. Please wait for the host to start.", "Notification");
                IsAutoStartActive = false;
            }
        }

        protected override async Task OnPollingSpecificAsync(List<MemberWithProfile> currentMembers)
        {
            var myId = _authService.CurrentUserId ?? _userSession.UserId;

            bool amIStillInRoom = false;
            foreach (var m in currentMembers)
            {
                if (m.UserId == myId)
                {
                    amIStillInRoom = true;
                    break;
                }
            }

            if (!amIStillInRoom)
            {
                StopPolling();
                MessageBox.Show("You have been removed from the room (or the connection was lost).", "Notification");
                CanCloseWindow = true;
                ForceCloseWindow();
                return;
            }

            await _supabaseService.SendHeartbeatAsync(_realClassroomIdUUID, myId);
        }

        [RelayCommand]
        private async Task LeaveRoom()
        {
            if (MessageBox.Show("Are you sure you want to leave the room?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    StopPolling();
                    var myId = _authService.CurrentUserId ?? _userSession.UserId;
                    await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, myId);

                    _navigationService.ShowMainWindow();

                    CanCloseWindow = true;
                    ForceCloseWindow();
                }
                catch (Exception)
                {
                    _navigationService.ShowMainWindow();
                    ForceCloseWindow();
                }
            }
        }
    }
}