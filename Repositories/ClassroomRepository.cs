using EasyFlips.Interfaces;
using EasyFlips.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;
using System.Collections.Generic;

namespace EasyFlips.Repositories
{
    /// <summary>
    /// Triển khai repository để quản lý dữ liệu phòng học (Classroom) và thành viên (Member) trong Supabase.
    /// </summary>
    public class ClassroomRepository : IClassroomRepository
    {
        private readonly Supabase.Client _client;

        /// <summary>
        /// Khởi tạo ClassroomRepository với Supabase client được inject.
        /// </summary>
        public ClassroomRepository(Supabase.Client client)
        {
            _client = client;
        }

        /// <summary>
        /// Tạo mới một phòng học trong cơ sở dữ liệu.
        /// </summary>
        /// <param name="room">Đối tượng Classroom cần tạo.</param>
        /// <returns>Đối tượng Classroom đã được tạo thành công, hoặc null nếu thất bại.</returns>
        public async Task<Classroom> CreateClassroomAsync(Classroom room)
        {
            var response = await _client.From<Classroom>().Insert(room);
            return response.Models.FirstOrDefault();
        }

        //===LẤY CLASSROOM THEO ROOM CODE (room_code LÀ MÃ 6 KÝ TỰ, roomId là Id trong DB, là một chuỗi dài hơn rất nhiều)===
        /// <summary>
        /// Tìm kiếm phòng học dựa trên mã code (6 ký tự) và trạng thái hoạt động.
        /// </summary>
        /// <param name="code">Mã phòng học (room_code).</param>
        /// <returns>Đối tượng Classroom nếu tìm thấy và đang active, ngược lại trả về null.</returns>
        public async Task<Classroom> GetClassroomByCodeAsync(string code)
        {
            try
            {
                // Duyệt qua DB va lấy các classroom với room_code và is_active = true
                var response = await _client.From<Classroom>()
                                            .Select("*")
                                            .Match(new Dictionary<string, string>
                                            {
                                                { "room_code", code },
                                                { "is_active", "true" }
                                            })
                                            .Get();
                return response.Models.FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        //=== CẬP NHẬT CỘT STATUS CỦA CLASSROOM THEO ROOM CODE ===
        /// <summary>
        /// Cập nhật trạng thái (Status) của phòng học dựa trên mã code.
        /// </summary>
        /// <param name="code">Mã phòng học.</param>
        /// <param name="status">Trạng thái mới (VD: "WAITING", "PLAYING").</param>
        public async Task UpdateStatusAsync(string code, string status)
        {
            await _client.From<Classroom>()
                         .Filter(x => x.RoomCode, Operator.Equals, code)
                         .Set(x => x.Status, status)
                         .Update();
        }

        //=== XÓA CLASSROOM THEO ROOM CODE ===
        /// <summary>
        /// Xóa phòng học dựa trên mã code.
        /// </summary>
        /// <param name="code">Mã phòng học cần xóa.</param>
        public async Task DeleteClassroomAsync(string code)
        {
            await _client.From<Classroom>()
                         .Filter(x => x.RoomCode, Operator.Equals, code)
                         .Delete();
        }

        //=== LẤY DANH SÁCH THÀNH VIÊN THEO ROOM ID (VÌ TRONG BẢNG MEMBER LƯU THEO ROOMID) ===
        /// <summary>
        /// Lấy danh sách thành viên tham gia phòng học dựa trên ID phòng (UUID).
        /// </summary>
        /// <param name="roomId">ID phòng học (UUID).</param>
        /// <returns>Danh sách các đối tượng Member.</returns>
        public async Task<List<Member>> GetMembersAsync(string roomId)
        {
            try
            {
                var response = await _client.From<Member>()
                                            .Select("*")
                                            .Filter("classroom_id", Operator.Equals, roomId)
                                            .Get();

                if (response.Models == null) return new List<Member>();
                return response.Models;
            }
            catch (Exception ex)
            {
                throw new Exception($"GetMembers Failed: {ex.Message}");
            }
        }

        //=== THÊM THÀNH VIÊN VÀO CLASSROOM (THÊM MỘT THỂ HIỆN TRONG BẢNG MEMBER) ===
        /// <summary>
        /// Thêm một người dùng vào phòng học (tạo bản ghi Member mới).
        /// </summary>
        /// <param name="classId">ID phòng học (UUID).</param>
        /// <param name="userId">ID người dùng (UUID).</param>
        public async Task AddMemberAsync(string classId, string userId)
        {
            try
            {
                var member = new Member
                {
                    ClassroomId = classId,
                    UserId = userId,
                    Role = "member",
                    JoinedAt = DateTime.UtcNow
                };
                await _client.From<Member>().Insert(member);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
                {
                    return;
                }
                throw new Exception($"AddMember Failed: {ex.Message}");
            }
        }

        //=== XÓA THÀNH VIÊN KHỎI CLASSROOM ===
        /// <summary>
        /// Xóa một thành viên khỏi phòng học.
        /// </summary>
        /// <param name="classId">ID phòng học.</param>
        /// <param name="userId">ID người dùng cần xóa.</param>
        public async Task RemoveMemberAsync(string classId, string userId)
        {
            try
            {
                await _client.From<Member>()
                             .Match(new Dictionary<string, string>
                             {
                                 { "classroom_id", classId },
                                 { "user_id", userId }
                             })
                             .Delete();
            }
            catch { }
        }

        //=== CẬP NHẬT CÀI ĐẶT CLASSROOM ===
        /// <summary>
        /// Cập nhật các cài đặt cấu hình cho phòng học (Deck, MaxPlayers, Time, WaitTime).
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="deckId">ID bộ thẻ mới (có thể null).</param>
        /// <param name="maxPlayers">Số người chơi tối đa mới.</param>
        /// <param name="timePerRound">Thời gian mỗi vòng chơi mới.</param>
        /// <param name="waitTimeSeconds">Thời gian chờ tự động bắt đầu mới.</param>
        /// <returns>Đối tượng Classroom sau khi cập nhật.</returns>
        public async Task<Classroom> UpdateClassroomSettingsAsync(string classroomId, string? deckId, int maxPlayers, int timePerRound, int waitTimeSeconds)
        {
            try
            {
                var response = await _client.From<Classroom>()
                                            .Filter(x => x.Id, Operator.Equals, classroomId)
                                            .Set(x => x.DeckId, deckId)
                                            .Set(x => x.MaxPlayers, maxPlayers)
                                            .Set(x => x.TimePerRound, timePerRound)
                                            .Set(x => x.WaitTime, waitTimeSeconds)
                                            .Set(x => x.UpdatedAt, DateTime.UtcNow)
                                            .Update();

                return response.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception($"UpdateClassroomSettings Failed: {ex.Message}");
            }
        }
    }
}