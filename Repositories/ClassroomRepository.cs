using EasyFlips.Interfaces;
using EasyFlips.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;
using System.Collections.Generic;

namespace EasyFlips.Repositories
{
    public class ClassroomRepository : IClassroomRepository
    {
        private readonly Supabase.Client _client;

        public ClassroomRepository(Supabase.Client client)
        {
            _client = client;
        }

        public async Task<Classroom> CreateClassroomAsync(Classroom room)
        {
            var response = await _client.From<Classroom>().Insert(room);
            return response.Models.FirstOrDefault();
        }

        public async Task<Classroom> GetClassroomByCodeAsync(string code)
        {
            try
            {
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

        public async Task UpdateStatusAsync(string code, string status)
        {
            await _client.From<Classroom>()
                         .Filter(x => x.RoomCode, Operator.Equals, code)
                         .Set(x => x.Status, status)
                         .Update();
        }

        public async Task DeleteClassroomAsync(string code)
        {
            await _client.From<Classroom>()
                         .Filter(x => x.RoomCode, Operator.Equals, code)
                         .Delete();
        }

        // --- [ĐOẠN CẦN SỬA Ở ĐÂY] ---
        public async Task<List<Member>> GetMembersAsync(string roomId)
        {
            try
            {
                // [FIX TẠM THỜI]: Bỏ "profiles(*)" đi để tránh lỗi Foreign Key
                // Chỉ lấy dữ liệu thô từ bảng Member để test số lượng (Count)
                var response = await _client.From<Member>()
                                            .Select("*")
                                            .Filter("classroom_id", Supabase.Postgrest.Constants.Operator.Equals, roomId)
                                            .Get();

                // Kiểm tra null an toàn trước khi trả về
                if (response.Models == null)
                {
                    return new List<Member>();
                }

                return response.Models;
            }
            catch (Exception ex)
            {
                // In chi tiết lỗi ra để debug nếu vẫn bị crash
                throw new Exception($"GetMembers Failed: {ex.Message}");
            }
        }

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
                // Bỏ qua lỗi trùng lặp (đã join rồi thì thôi)
                if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
                {
                    return;
                }
                throw new Exception($"AddMember Failed: {ex.Message}");
            }
        }

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
    }
}