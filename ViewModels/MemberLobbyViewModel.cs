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

            // 1. Thêm Member vào phòng (Join)
            await _supabaseService.AddMemberAsync(_realClassroomIdUUID, myId);

            // 2. Tính toán thời gian còn lại để hiển thị đồng bộ với Host
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
                // Nếu vào khi đã hết giờ chờ (nhưng Host chưa Start hoặc đang Start)
                // Thông báo hoặc cho phép ngồi chờ Host bấm nút
                MessageBox.Show("Thời gian chờ đã kết thúc, vui lòng đợi chủ phòng bắt đầu.", "Thông báo");
                IsAutoStartActive = false;
            }
        }

        protected override async Task OnPollingSpecificAsync(List<MemberWithProfile> currentMembers)
        {
            // Logic Member: Gửi Heartbeat để báo mình còn sống
            var myId = _authService.CurrentUserId ?? _userSession.UserId;

            // Kiểm tra xem mình có còn trong danh sách thành viên không (có bị Kick không?)
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
                MessageBox.Show("Bạn đã bị mời ra khỏi phòng (hoặc kết nối bị ngắt).", "Thông báo");
                CanCloseWindow = true;
                ForceCloseWindow();
                return;
            }

            // Gửi Heartbeat update LastActive
            await _supabaseService.SendHeartbeatAsync(_realClassroomIdUUID, myId);
        }

        [RelayCommand]
        private async Task LeaveRoom()
        {
            if (MessageBox.Show("Bạn muốn rời phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    StopPolling();
                    var myId = _authService.CurrentUserId ?? _userSession.UserId;
                    await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, myId);

                    // MỞ LẠI MAIN WINDOW
                    _navigationService.ShowMainWindow();

                    CanCloseWindow = true;
                    ForceCloseWindow();
                }
                catch (Exception)
                {
                    // Dù lỗi API (ví dụ mất mạng) vẫn cho về trang chủ
                    _navigationService.ShowMainWindow();
                    ForceCloseWindow();
                }
            }
        }
    }
}