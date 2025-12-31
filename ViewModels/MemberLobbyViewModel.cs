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
        // Biến đếm số lần liên tiếp không thấy mình trong phòng
        private int _missingFromRoomRetryCount = 0;
        private const int MAX_RETRY_COUNT = 3; // Cho phép lỡ 3 nhịp (khoảng 5-10s tùy tốc độ polling)
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

            try
            {
                // Cố gắng thêm thành viên
                await _supabaseService.AddMemberAsync(_realClassroomIdUUID, myId);
            }
            catch (Supabase.Postgrest.Exceptions.PostgrestException ex) when (ex.StatusCode == 23505 || ex.Message.Contains("23505"))
            {
                // Nếu lỗi là 23505 (Duplicate Key): User đã có trong phòng.
                // Ta "nuốt" lỗi này để code không bị dừng lại, coi như thành công.
                System.Diagnostics.Debug.WriteLine("User already in room, skipping insert.");
            }

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

            // 1. Kiểm tra xem mình có trong danh sách không
            bool amIStillInRoom = currentMembers.Any(m => m.UserId == myId);

            if (amIStillInRoom)
            {
                // Nếu thấy mình -> Reset biến đếm về 0 ngay lập tức (Kết nối ổn định)
                _missingFromRoomRetryCount = 0;

                // Gửi Heartbeat để báo Host biết mình còn sống
                // (Lưu ý: Chỉ gửi khi mình còn trong phòng để tránh spam lỗi vào DB nếu đã bị xóa)
                await _supabaseService.SendHeartbeatAsync(_realClassroomIdUUID, myId);
            }
            else
            {
                // Nếu KHÔNG thấy mình -> Tăng biến đếm
                _missingFromRoomRetryCount++;

                System.Diagnostics.Debug.WriteLine($"[Warning] Không thấy mình trong phòng. Lần thử: {_missingFromRoomRetryCount}/{MAX_RETRY_COUNT}");

                // Nếu đã quá số lần cho phép (ví dụ 3 lần liên tiếp) -> Mới thực sự thoát
                if (_missingFromRoomRetryCount >= MAX_RETRY_COUNT)
                {
                    StopPolling();
                    MessageBox.Show("You have been removed from the room (or the connection was lost).", "Notification");
                    CanCloseWindow = true;
                    ForceCloseWindow();
                    return;
                }

                // Nếu chưa quá 3 lần -> Chấp nhận bỏ qua lần này, chờ lần poll tiếp theo
                // Có thể do mạng lag chưa tải hết list, hoặc DB Supabase chưa sync kịp.
            }
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