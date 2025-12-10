using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Supabase;
using EasyFlips.Models;

namespace EasyFlips.Services
{
    /// <summary>
    /// Service tương tác với Supabase database
    /// Quản lý Profiles, Classrooms, Members
    /// </summary>
    public class SupabaseService
    {
        private readonly Supabase.Client _client;

        public SupabaseService()
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };
            // Đảm bảo cấu hình URL và Key chính xác trong AppConfig
            _client = new Supabase.Client(AppConfig.SupabaseUrl, AppConfig.SupabaseKey, options);
        }

        public async Task InitializeAsync()
        {
            await _client.InitializeAsync();
        }

        #region Profile Operations

        /// <summary>
        /// Lấy profile của user hiện tại
        /// </summary>
        // [ĐÃ SỬA]: Tham số userId đổi từ Guid -> string
        public async Task<Profile?> GetProfileAsync(string userId)
        {
            var result = await _client
                .From<Profile>()
                .Where(x => x.Id == userId)
                .Single();

            return result;
        }

        /// <summary>
        /// Cập nhật profile (display name, avatar)
        /// </summary>
        // [ĐÃ SỬA]: Tham số userId đổi từ Guid -> string
        public async Task<Profile?> UpdateProfileAsync(string userId, string? displayName, string? avatarUrl)
        {
            var profile = new Profile
            {
                Id = userId, // Giờ cả 2 đều là string, gán OK
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _client
                .From<Profile>()
                .Update(profile);

            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Tìm kiếm user theo email
        /// </summary>
        public async Task<List<Profile>> SearchProfilesByEmailAsync(string emailQuery)
        {
            var result = await _client
                .From<Profile>()
                .Where(x => x.Email.Contains(emailQuery))
                .Get();

            return result.Models;
        }

        #endregion

        #region Classroom Operations

        /// <summary>
        /// Tạo classroom mới
        /// Room code sẽ tự động được generate bởi database hoặc code
        /// </summary>
        // [ĐÃ SỬA]: ownerId đổi từ Guid -> string
        public async Task<Classroom?> CreateClassroomAsync(string name, string? description, string ownerId)
        {
            // Generate room code using database function
            var roomCode = await GenerateRoomCodeAsync();

            var classroom = new Classroom
            {
                // [ĐÃ SỬA]: Tạo UUID dạng string
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                RoomCode = roomCode,
                OwnerId = ownerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _client
                .From<Classroom>()
                .Insert(classroom);

            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Lấy classroom theo ID
        /// </summary>
        // [ĐÃ SỬA]: Tham số classroomId đổi từ Guid -> string
        public async Task<Classroom?> GetClassroomAsync(string classroomId)
        {
            var result = await _client
                .From<Classroom>()
                .Where(x => x.Id == classroomId)
                .Single();

            return result;
        }

        /// <summary>
        /// Cập nhật thông tin classroom
        /// </summary>
        // [ĐÃ SỬA]: Tham số classroomId đổi từ Guid -> string
        public async Task<Classroom?> UpdateClassroomAsync(string classroomId, string name, string? description)
        {
            var classroom = new Classroom
            {
                Id = classroomId,
                Name = name,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _client
                .From<Classroom>()
                .Update(classroom);

            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Xóa classroom (soft delete - set IsActive = false)
        /// </summary>
        // [ĐÃ SỬA]: Tham số classroomId đổi từ Guid -> string
        public async Task<bool> DeactivateClassroomAsync(string classroomId)
        {
            var classroom = new Classroom
            {
                Id = classroomId,
                IsActive = false,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _client
                .From<Classroom>()
                .Update(classroom);

            return result.Models.Any();
        }

        /// <summary>
        /// Lấy danh sách classroom của user
        /// </summary>
        // [ĐÃ SỬA]: Tham số userId đổi từ Guid -> string
        public async Task<List<UserClassroom>> GetUserClassroomsAsync(string userId)
        {
            // Call database function
            var result = await _client.Rpc("get_user_classrooms", new Dictionary<string, object>
            {
                { "p_user_id", userId } // Supabase sẽ tự handle string -> uuid nếu parameter của hàm SQL là uuid
            });

            // TODO: Parse result trả về thành List<UserClassroom>
            // Lưu ý: Cần đảm bảo RPC trả về đúng format JSON map được với class UserClassroom
            return new List<UserClassroom>();
        }

        #endregion

        #region Member Operations

        /// <summary>
        /// Join classroom bằng room code
        /// </summary>
        // [ĐÃ SỬA]: Tham số userId đổi từ Guid -> string
        public async Task<JoinClassroomResult> JoinClassroomByCodeAsync(string roomCode, string userId)
        {
            // Call database function
            var result = await _client.Rpc("join_classroom_by_code", new Dictionary<string, object>
            {
                { "p_room_code", roomCode },
                { "p_user_id", userId }
            });

            // Parse result
            // TODO: Parse JSON result to JoinClassroomResult
            return new JoinClassroomResult
            {
                Success = true,
                Message = "Joined successfully"
            };
        }

        /// <summary>
        /// Lấy danh sách members trong classroom
        /// </summary>
        // [ĐÃ SỬA]: Tham số classroomId đổi từ Guid -> string
        public async Task<List<Member>> GetClassroomMembersAsync(string classroomId)
        {
            var result = await _client
                .From<Member>()
                .Where(x => x.ClassroomId == classroomId)
                .Get();

            return result.Models;
        }

        /// <summary>
        /// Thêm member vào classroom (by owner)
        /// </summary>
        // [ĐÃ SỬA]: Các tham số Guid -> string
        public async Task<Member?> AddMemberAsync(string classroomId, string userId, string role = "member")
        {
            var member = new Member
            {
                // [ĐÃ SỬA]: Tạo UUID dạng string
                Id = Guid.NewGuid().ToString(),
                ClassroomId = classroomId,
                UserId = userId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            };

            var result = await _client
                .From<Member>()
                .Insert(member);

            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Xóa member khỏi classroom
        /// </summary>
        // [ĐÃ SỬA]: Các tham số Guid -> string
        public async Task<bool> RemoveMemberAsync(string classroomId, string userId)
        {
            await _client
                .From<Member>()
                .Where(x => x.ClassroomId == classroomId && x.UserId == userId)
                .Delete();

            return true;
        }

        /// <summary>
        /// Rời khỏi classroom
        /// </summary>
        public async Task<bool> LeaveClassroomAsync(string classroomId, string userId)
        {
            return await RemoveMemberAsync(classroomId, userId);
        }

        /// <summary>
        /// Update role của member (by owner)
        /// </summary>
        public async Task<Member?> UpdateMemberRoleAsync(string classroomId, string userId, string newRole)
        {
            var member = new Member
            {
                ClassroomId = classroomId,
                UserId = userId,
                Role = newRole
            };

            var result = await _client
                .From<Member>()
                .Where(x => x.ClassroomId == classroomId && x.UserId == userId)
                .Update(member);

            return result.Models.FirstOrDefault();
        }

        #endregion

        #region Storage Operations

        /// <summary>
        /// Upload avatar lên storage bucket
        /// </summary>
        // [ĐÃ SỬA]: userId -> string
        public async Task<string?> UploadAvatarAsync(string userId, byte[] imageData, string fileName)
        {
            // Path format: {userId}/avatar.{extension}
            var path = $"{userId}/{fileName}";

            try
            {
                await _client.Storage
                    .From("avatars")
                    .Upload(imageData, path);

                // Return public URL
                var url = _client.Storage
                    .From("avatars")
                    .GetPublicUrl(path);

                return url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SupabaseService] Upload avatar error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Xóa avatar
        /// </summary>
        public async Task<bool> DeleteAvatarAsync(string userId, string fileName)
        {
            var path = $"{userId}/{fileName}";

            try
            {
                await _client.Storage
                    .From("avatars")
                    .Remove(path);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SupabaseService] Delete avatar error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy public URL của avatar
        /// </summary>
        public string GetAvatarUrl(string userId, string fileName)
        {
            var path = $"{userId}/{fileName}";
            return _client.Storage
                .From("avatars")
                .GetPublicUrl(path);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generate room code unique
        /// </summary>
        private async Task<string> GenerateRoomCodeAsync()
        {
            try
            {
                var result = await _client.Rpc("generate_room_code", null);
                // Giả sử RPC trả về trực tiếp string content hoặc JSON object
                // Cần kiểm tra kỹ response format thực tế khi chạy
                return result.Content ?? "TEMP1234";
            }
            catch
            {
                return "TEMP1234";
            }
        }

        #endregion

        #region Realtime Subscriptions (Optional)

        /// <summary>
        /// Subscribe to classroom changes
        /// </summary>
        public void SubscribeToClassroom(string classroomId, Action<Classroom> onUpdate)
        {
            Console.WriteLine($"[SupabaseService] Realtime subscription not yet implemented for classroom: {classroomId}");
        }

        /// <summary>
        /// Subscribe to member changes in classroom
        /// </summary>
        public void SubscribeToClassroomMembers(string classroomId, Action<Member> onMemberChange)
        {
            Console.WriteLine($"[SupabaseService] Realtime subscription not yet implemented for members in classroom: {classroomId}");
        }

        #endregion
    }
}