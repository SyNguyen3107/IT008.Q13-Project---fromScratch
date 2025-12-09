using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Supabase;
using EasyFlips.Models;

namespace EasyFlips.Services
{
    /// <summary>
    /// Service ?? t??ng tác v?i Supabase database
    /// Qu?n lý Profiles, Classrooms, Members
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
            _client = new Supabase.Client(AppConfig.SupabaseUrl, AppConfig.SupabaseKey, options);
        }

        public async Task InitializeAsync()
        {
            await _client.InitializeAsync();
        }

        #region Profile Operations

        /// <summary>
        /// L?y profile c?a user hi?n t?i
        /// </summary>
        public async Task<Profile?> GetProfileAsync(Guid userId)
        {
            var result = await _client
                .From<Profile>()
                .Where(x => x.Id == userId)
                .Single();

            return result;
        }

        /// <summary>
        /// C?p nh?t profile (display name, avatar)
        /// </summary>
        public async Task<Profile?> UpdateProfileAsync(Guid userId, string? displayName, string? avatarUrl)
        {
            var profile = new Profile
            {
                Id = userId,
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
        /// Tìm ki?m user theo email
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
        /// T?o classroom m?i
        /// Room code s? t? ??ng ???c generate b?i database
        /// </summary>
        public async Task<Classroom?> CreateClassroomAsync(string name, string? description, Guid ownerId)
        {
            // Generate room code using database function
            var roomCode = await GenerateRoomCodeAsync();

            var classroom = new Classroom
            {
                Id = Guid.NewGuid(),
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
        /// L?y classroom theo ID
        /// </summary>
        public async Task<Classroom?> GetClassroomAsync(Guid classroomId)
        {
            var result = await _client
                .From<Classroom>()
                .Where(x => x.Id == classroomId)
                .Single();

            return result;
        }

        /// <summary>
        /// C?p nh?t thông tin classroom
        /// </summary>
        public async Task<Classroom?> UpdateClassroomAsync(Guid classroomId, string name, string? description)
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
        public async Task<bool> DeactivateClassroomAsync(Guid classroomId)
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
        /// L?y danh sách classroom c?a user
        /// </summary>
        public async Task<List<UserClassroom>> GetUserClassroomsAsync(Guid userId)
        {
            // Call database function
            var result = await _client.Rpc("get_user_classrooms", new Dictionary<string, object>
            {
                { "p_user_id", userId }
            });

            // Parse result to UserClassroom list
            // Note: You may need to adjust parsing based on actual return format
            return new List<UserClassroom>(); // TODO: Parse result
        }

        #endregion

        #region Member Operations

        /// <summary>
        /// Join classroom b?ng room code
        /// </summary>
        public async Task<JoinClassroomResult> JoinClassroomByCodeAsync(string roomCode, Guid userId)
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
        /// L?y danh sách members trong classroom
        /// </summary>
        public async Task<List<Member>> GetClassroomMembersAsync(Guid classroomId)
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
        public async Task<Member?> AddMemberAsync(Guid classroomId, Guid userId, string role = "member")
        {
            var member = new Member
            {
                Id = Guid.NewGuid(),
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
        /// Xóa member kh?i classroom
        /// </summary>
        public async Task<bool> RemoveMemberAsync(Guid classroomId, Guid userId)
        {
            await _client
                .From<Member>()
                .Where(x => x.ClassroomId == classroomId && x.UserId == userId)
                .Delete();

            return true;
        }

        /// <summary>
        /// R?i kh?i classroom
        /// </summary>
        public async Task<bool> LeaveClassroomAsync(Guid classroomId, Guid userId)
        {
            return await RemoveMemberAsync(classroomId, userId);
        }

        /// <summary>
        /// Update role c?a member (by owner)
        /// </summary>
        public async Task<Member?> UpdateMemberRoleAsync(Guid classroomId, Guid userId, string newRole)
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
        public async Task<string?> UploadAvatarAsync(Guid userId, byte[] imageData, string fileName)
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
        public async Task<bool> DeleteAvatarAsync(Guid userId, string fileName)
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
        /// L?y public URL c?a avatar
        /// </summary>
        public string GetAvatarUrl(Guid userId, string fileName)
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
            var result = await _client.Rpc("generate_room_code", null);
            // TODO: Parse result to string
            return "TEMP1234"; // Temporary, replace with actual parsing
        }

        #endregion

        #region Realtime Subscriptions (Optional)

        /// <summary>
        /// Subscribe to classroom changes
        /// TODO: Implement when realtime API is stable
        /// </summary>
        public void SubscribeToClassroom(Guid classroomId, Action<Classroom> onUpdate)
        {
            // Realtime API implementation depends on Supabase-csharp version
            // Will be implemented when needed
            Console.WriteLine($"[SupabaseService] Realtime subscription not yet implemented for classroom: {classroomId}");
        }

        /// <summary>
        /// Subscribe to member changes in classroom
        /// TODO: Implement when realtime API is stable
        /// </summary>
        public void SubscribeToClassroomMembers(Guid classroomId, Action<Member> onMemberChange)
        {
            // Realtime API implementation depends on Supabase-csharp version
            // Will be implemented when needed
            Console.WriteLine($"[SupabaseService] Realtime subscription not yet implemented for members in classroom: {classroomId}");
        }

        #endregion
    }
}
