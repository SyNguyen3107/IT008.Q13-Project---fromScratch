using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Supabase;
using EasyFlips.Models;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System.Diagnostics; // Dùng Debug.WriteLine thay cho MessageBox

namespace EasyFlips.Services
{
    public class SupabaseService
    {
        private readonly Supabase.Client _client;
        private readonly CustomFileSessionHandler _sessionHandler;

        public Supabase.Client Client => _client;

        public SupabaseService()
        {
            // Thiết lập đường dẫn lưu cache tại %AppData%/EasyFlips
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyFlips");
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

            _sessionHandler = new CustomFileSessionHandler(cacheDir);

            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true,
                SessionHandler = _sessionHandler
            };

            _client = new Supabase.Client(AppConfig.SupabaseUrl, AppConfig.SupabaseKey, options);
        }

        public async Task InitializeAsync()
        {
            try
            {
                // [FIX]: Tự động load session thủ công để đảm bảo tính ổn định
                var loadedSession = _sessionHandler.LoadSession();

                await _client.InitializeAsync();

                // Nếu thư viện không tự nhận session, ta thực hiện thủ công
                if (_client.Auth.CurrentSession == null && loadedSession != null)
                {
                    if (!string.IsNullOrEmpty(loadedSession.AccessToken) && !string.IsNullOrEmpty(loadedSession.RefreshToken))
                    {
                        try
                        {
                            await _client.Auth.SetSession(loadedSession.AccessToken, loadedSession.RefreshToken);
                            Debug.WriteLine("[SupabaseService] Session restored manually.");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[SupabaseService] Failed to set session: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Initialization error: {ex.Message}");
            }
        }

        #region Profile Operations
        public async Task<Profile?> GetProfileAsync(string userId)
        {
            var result = await _client.From<Profile>().Where(x => x.Id == userId).Single();
            return result;
        }
        public async Task<Profile?> UpdateProfileAsync(string userId, string? displayName, string? avatarUrl)
        {
            var profile = new Profile { Id = userId, DisplayName = displayName, AvatarUrl = avatarUrl, UpdatedAt = DateTime.UtcNow };
            var result = await _client.From<Profile>().Update(profile);
            return result.Models.FirstOrDefault();
        }
        public async Task<List<Profile>> SearchProfilesByEmailAsync(string emailQuery)
        {
            var result = await _client.From<Profile>().Where(x => x.Email.Contains(emailQuery)).Get();
            return result.Models;
        }
        #endregion

        #region Classroom Operations
        public async Task<Classroom?> CreateClassroomAsync(string name, string? description, string ownerId)
        {
            var roomCode = await GenerateRoomCodeAsync();
            var classroom = new Classroom
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                RoomCode = roomCode,
                OwnerId = ownerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var result = await _client.From<Classroom>().Insert(classroom);
            return result.Models.FirstOrDefault();
        }
        public async Task<Classroom?> GetClassroomAsync(string classroomId)
        {
            var result = await _client.From<Classroom>().Where(x => x.Id == classroomId).Single();
            return result;
        }
        public async Task<Classroom?> UpdateClassroomAsync(string classroomId, string name, string? description)
        {
            var classroom = new Classroom { Id = classroomId, Name = name, Description = description, UpdatedAt = DateTime.UtcNow };
            var result = await _client.From<Classroom>().Update(classroom);
            return result.Models.FirstOrDefault();
        }
        public async Task<bool> DeactivateClassroomAsync(string classroomId)
        {
            var classroom = new Classroom { Id = classroomId, IsActive = false, UpdatedAt = DateTime.UtcNow };
            var result = await _client.From<Classroom>().Update(classroom);
            return result.Models.Any();
        }
        public async Task<List<UserClassroom>> GetUserClassroomsAsync(string userId)
        {
            var result = await _client.Rpc("get_user_classrooms", new Dictionary<string, object> { { "p_user_id", userId } });
            return new List<UserClassroom>();
        }
        #endregion

        #region Member Operations
        public async Task<JoinClassroomResult> JoinClassroomByCodeAsync(string roomCode, string userId)
        {
            var result = await _client.Rpc("join_classroom_by_code", new Dictionary<string, object> { { "p_room_code", roomCode }, { "p_user_id", userId } });
            return new JoinClassroomResult { Success = true, Message = "Joined successfully" };
        }
        public async Task<List<Member>> GetClassroomMembersAsync(string classroomId)
        {
            var result = await _client.From<Member>().Where(x => x.ClassroomId == classroomId).Get();
            return result.Models;
        }
        public async Task<Member?> AddMemberAsync(string classroomId, string userId, string role = "member")
        {
            var member = new Member { Id = Guid.NewGuid().ToString(), ClassroomId = classroomId, UserId = userId, Role = role, JoinedAt = DateTime.UtcNow };
            var result = await _client.From<Member>().Insert(member);
            return result.Models.FirstOrDefault();
        }
        public async Task<bool> RemoveMemberAsync(string classroomId, string userId)
        {
            await _client.From<Member>().Where(x => x.ClassroomId == classroomId && x.UserId == userId).Delete();
            return true;
        }
        public async Task<bool> LeaveClassroomAsync(string classroomId, string userId)
        {
            return await RemoveMemberAsync(classroomId, userId);
        }
        public async Task<Member?> UpdateMemberRoleAsync(string classroomId, string userId, string newRole)
        {
            var member = new Member { ClassroomId = classroomId, UserId = userId, Role = newRole };
            var result = await _client.From<Member>().Where(x => x.ClassroomId == classroomId && x.UserId == userId).Update(member);
            return result.Models.FirstOrDefault();
        }
        #endregion

        #region Storage Operations
        public async Task<string?> UploadAvatarAsync(string userId, byte[] imageData, string fileName)
        {
            var path = $"{userId}/{fileName}";
            try
            {
                await _client.Storage.From("avatars").Upload(imageData, path);
                return _client.Storage.From("avatars").GetPublicUrl(path);
            }
            catch (Exception ex) { Debug.WriteLine($"[SupabaseService] Upload avatar error: {ex.Message}"); return null; }
        }
        public async Task<bool> DeleteAvatarAsync(string userId, string fileName)
        {
            var path = $"{userId}/{fileName}";
            try
            {
                await _client.Storage.From("avatars").Remove(path);
                return true;
            }
            catch (Exception ex) { Debug.WriteLine($"[SupabaseService] Delete avatar error: {ex.Message}"); return false; }
        }
        public string GetAvatarUrl(string userId, string fileName)
        {
            var path = $"{userId}/{fileName}";
            return _client.Storage.From("avatars").GetPublicUrl(path);
        }
        #endregion

        #region Helper Methods
        private async Task<string> GenerateRoomCodeAsync()
        {
            try { var result = await _client.Rpc("generate_room_code", null); return result.Content ?? "TEMP1234"; }
            catch { return "TEMP1234"; }
        }
        #endregion

        #region Realtime Subscriptions (Optional)
        public void SubscribeToClassroom(string classroomId, Action<Classroom> onUpdate) { }
        public void SubscribeToClassroomMembers(string classroomId, Action<Member> onMemberChange) { }
        #endregion
    }

    public class CustomFileSessionHandler : IGotrueSessionPersistence<Session>
    {
        private readonly string _cachePath;
        private readonly string _fileName = ".gotrue.cache";

        // Cấu hình JSON bỏ qua lỗi null và format ngày tháng chuẩn
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        public CustomFileSessionHandler(string cachePath)
        {
            _cachePath = cachePath;
        }

        public void SaveSession(Session session)
        {
            try
            {
                if (session == null)
                {
                    DestroySession();
                    return;
                }

                var fullPath = Path.Combine(_cachePath, _fileName);
                var json = JsonConvert.SerializeObject(session, Formatting.Indented, _jsonSettings);
                File.WriteAllText(fullPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHandler] Save failed: {ex.Message}");
            }
        }

        public Session? LoadSession()
        {
            try
            {
                var fullPath = Path.Combine(_cachePath, _fileName);
                if (!File.Exists(fullPath)) return null;

                var json = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(json)) return null;

                // Deserialize với settings chuẩn
                var session = JsonConvert.DeserializeObject<Session>(json, _jsonSettings);
                return session;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHandler] Load failed: {ex.Message}");
                return null;
            }
        }

        public void DestroySession()
        {
            try
            {
                var path = Path.Combine(_cachePath, _fileName);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}