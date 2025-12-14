using EasyFlips.Models;
using Newtonsoft.Json;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Realtime; // Đảm bảo namespace này có mặt
using Supabase.Realtime.Interfaces; // Đảm bảo namespace này có mặt
using Supabase.Realtime.PostgresChanges;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using RealtimeConstants = Supabase.Realtime.Constants;
using Supabase.Realtime.Presence;

namespace EasyFlips.Services
{
    public class SupabaseService
    {
        private readonly Supabase.Client _client;
        private readonly CustomFileSessionHandler _sessionHandler;
        // [NEW]: Quản lý các kênh presence đang active
        private readonly Dictionary<string, RealtimeChannel> _activeChannels = new Dictionary<string, RealtimeChannel>();
        public Supabase.Client Client => _client;

        public SupabaseService()
        {
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
                HostId = ownerId,
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

        /// <summary>
        /// Giải tán phòng hoàn toàn (Hard Delete) - Chỉ Host mới được gọi
        /// Xóa tất cả members trước, sau đó xóa classroom
        /// </summary>
        /// <param name="classroomId">ID phòng cần giải tán</param>
        /// <param name="hostId">ID của Host (để kiểm tra quyền)</param>
        /// <returns>True nếu thành công, False nếu thất bại hoặc không có quyền</returns>
        public async Task<(bool Success, string Message)> DissolveClassroomAsync(string classroomId, string hostId)
        {
            try
            {
                // 1. Kiểm tra phòng có tồn tại không
                var classroom = await GetClassroomAsync(classroomId);
                if (classroom == null)
                {
                    return (false, "Phòng không tồn tại.");
                }

                // 2. Kiểm tra quyền Host
                if (classroom.HostId != hostId)
                {
                    return (false, "Bạn không có quyền giải tán phòng này.");
                }

                // 3. Xóa tất cả members trong phòng trước
                await _client.From<Member>()
                    .Where(x => x.ClassroomId == classroomId)
                    .Delete();
                Debug.WriteLine($"[SupabaseService] Deleted all members from room {classroomId}");

                // 4. Xóa classroom
                await _client.From<Classroom>()
                    .Where(x => x.Id == classroomId)
                    .Delete();
                Debug.WriteLine($"[SupabaseService] Deleted classroom {classroomId}");

                return (true, "Đã giải tán phòng thành công.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Dissolve classroom error: {ex.Message}");
                return (false, $"Lỗi khi giải tán phòng: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy danh sách members kèm thông tin profile (tên, avatar)
        /// </summary>
        public async Task<List<MemberWithProfile>> GetClassroomMembersWithProfileAsync(string classroomId)
        {
            try
            {
                // Lấy danh sách members
                var members = await GetClassroomMembersAsync(classroomId);
                var result = new List<MemberWithProfile>();

                // Lấy profile cho từng member
                foreach (var member in members)
                {
                    var profile = await GetProfileAsync(member.UserId);
                    result.Add(new MemberWithProfile
                    {
                        MemberId = member.Id,
                        UserId = member.UserId,
                        ClassroomId = member.ClassroomId,
                        Role = member.Role,
                        JoinedAt = member.JoinedAt,
                        DisplayName = profile?.DisplayName ?? "Unknown",
                        Email = profile?.Email ?? "",
                        AvatarUrl = profile?.AvatarUrl
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Get members with profile error: {ex.Message}");
                return new List<MemberWithProfile>();
            }
        }

        /// <summary>
        /// Kiểm tra user có phải là Host của phòng không
        /// </summary>
        public async Task<bool> IsHostAsync(string classroomId, string userId)
        {
            var classroom = await GetClassroomAsync(classroomId);
            return classroom?.HostId == userId;
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

        /// <summary>
        /// Upload ảnh avatar từ byte array
        /// </summary>
        /// <param name="userId">ID của user</param>
        /// <param name="imageData">Dữ liệu ảnh dạng byte[]</param>
        /// <param name="fileName">Tên file (vd: avatar.png)</param>
        /// <returns>Public URL của ảnh hoặc null nếu thất bại</returns>
        public async Task<string?> UploadAvatarAsync(string userId, byte[] imageData, string fileName)
        {
            var path = $"{userId}/{fileName}";
            try
            {
                // Upload với option upsert để ghi đè nếu đã tồn tại
                await _client.Storage.From("avatars").Upload(imageData, path, new Supabase.Storage.FileOptions
                {
                    Upsert = true
                });
                return _client.Storage.From("avatars").GetPublicUrl(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Upload avatar error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Upload ảnh avatar từ file path và tự động cập nhật vào bảng profiles
        /// </summary>
        /// <param name="userId">ID của user</param>
        /// <param name="filePath">Đường dẫn file ảnh trên máy</param>
        /// <returns>Public URL của ảnh hoặc null nếu thất bại</returns>
        public async Task<string?> UploadAvatarFromFileAsync(string userId, string filePath)
        {
            try
            {
                // 1. Validate file
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[SupabaseService] File not found: {filePath}");
                    return null;
                }

                // 2. Đọc file thành byte array
                var imageData = await File.ReadAllBytesAsync(filePath);

                // 3. Tạo tên file unique (tránh cache browser)
                var extension = Path.GetExtension(filePath).ToLower();
                var uniqueFileName = $"avatar_{DateTime.UtcNow.Ticks}{extension}";

                // 4. Upload lên Storage
                var avatarUrl = await UploadAvatarAsync(userId, imageData, uniqueFileName);

                if (avatarUrl != null)
                {
                    // 5. Cập nhật URL vào bảng profiles
                    await UpdateProfileAvatarAsync(userId, avatarUrl);
                    Debug.WriteLine($"[SupabaseService] Avatar uploaded and profile updated: {avatarUrl}");
                }

                return avatarUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Upload avatar from file error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Upload ảnh avatar từ Stream (dùng cho clipboard, camera...)
        /// </summary>
        public async Task<string?> UploadAvatarFromStreamAsync(string userId, Stream imageStream, string extension = ".png")
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                var uniqueFileName = $"avatar_{DateTime.UtcNow.Ticks}{extension}";
                var avatarUrl = await UploadAvatarAsync(userId, imageData, uniqueFileName);

                if (avatarUrl != null)
                {
                    await UpdateProfileAvatarAsync(userId, avatarUrl);
                }

                return avatarUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Upload avatar from stream error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cập nhật avatar_url vào bảng profiles
        /// </summary>
        public async Task<bool> UpdateProfileAvatarAsync(string userId, string avatarUrl)
        {
            try
            {
                var result = await _client.From<Profile>()
                    .Where(x => x.Id == userId)
                    .Set(x => x.AvatarUrl, avatarUrl)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .Update();

                return result.Models.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Update profile avatar error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Xóa avatar cũ khỏi Storage
        /// </summary>
        public async Task<bool> DeleteAvatarAsync(string userId, string fileName)
        {
            var path = $"{userId}/{fileName}";
            try
            {
                await _client.Storage.From("avatars").Remove(new List<string> { path });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Delete avatar error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Xóa tất cả avatar cũ của user và upload avatar mới
        /// </summary>
        public async Task<string?> ReplaceAvatarAsync(string userId, string newFilePath)
        {
            try
            {
                // 1. Lấy danh sách file cũ trong folder của user
                var existingFiles = await _client.Storage.From("avatars").List(userId);

                // 2. Xóa tất cả file cũ
                if (existingFiles != null && existingFiles.Any())
                {
                    var pathsToDelete = existingFiles.Select(f => $"{userId}/{f.Name}").ToList();
                    await _client.Storage.From("avatars").Remove(pathsToDelete);
                    Debug.WriteLine($"[SupabaseService] Deleted {pathsToDelete.Count} old avatar(s)");
                }

                // 3. Upload file mới
                return await UploadAvatarFromFileAsync(userId, newFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Replace avatar error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Upload avatar mới và giữ lại 1 avatar gần nhất làm backup (để rollback trong 7-30 ngày)
        /// Chỉ giữ tối đa 2 ảnh: ảnh hiện tại + 1 ảnh backup
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        /// <param name="newFilePath">Đường dẫn file ảnh mới</param>
        /// <returns>Public URL của ảnh mới hoặc null nếu thất bại</returns>
        public async Task<string?> ReplaceAvatarWithBackupAsync(string userId, string newFilePath)
        {
            try
            {
                // 1. Validate file
                if (!File.Exists(newFilePath))
                {
                    Debug.WriteLine($"[SupabaseService] File not found: {newFilePath}");
                    return null;
                }

                // 2. Lấy danh sách file cũ trong folder của user
                var existingFiles = await _client.Storage.From("avatars").List(userId);
                Debug.WriteLine($"[SupabaseService] Found {existingFiles?.Count ?? 0} existing avatar(s)");

                // 3. Sắp xếp theo tên file (chứa timestamp) để xác định ảnh cũ nhất
                // Giữ lại 1 ảnh gần nhất (avatar hiện tại sẽ thành backup), xóa các ảnh cũ hơn
                if (existingFiles != null && existingFiles.Count >= 2)
                {
                    // Sắp xếp theo tên file giảm dần (file mới nhất lên đầu)
                    var sortedFiles = existingFiles
                        .OrderByDescending(f => f.Name)
                        .ToList();

                    // Giữ lại 1 file mới nhất (sẽ thành backup), xóa các file còn lại
                    var filesToDelete = sortedFiles.Skip(1).Select(f => $"{userId}/{f.Name}").ToList();
                    
                    if (filesToDelete.Any())
                    {
                        await _client.Storage.From("avatars").Remove(filesToDelete);
                        Debug.WriteLine($"[SupabaseService] Deleted {filesToDelete.Count} old avatar(s), kept 1 as backup");
                    }
                }

                // 4. Đọc file mới thành byte array
                var imageData = await File.ReadAllBytesAsync(newFilePath);

                // 5. Tạo tên file unique với timestamp
                var extension = Path.GetExtension(newFilePath).ToLower();
                var uniqueFileName = $"avatar_{DateTime.UtcNow.Ticks}{extension}";

                // 6. Upload lên Storage
                var avatarUrl = await UploadAvatarAsync(userId, imageData, uniqueFileName);

                if (avatarUrl != null)
                {
                    // 7. Cập nhật URL vào bảng profiles
                    await UpdateProfileAvatarAsync(userId, avatarUrl);
                    Debug.WriteLine($"[SupabaseService] New avatar uploaded with backup: {avatarUrl}");
                }

                return avatarUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Replace avatar with backup error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy public URL của avatar
        /// </summary>
        public string GetAvatarUrl(string userId, string fileName)
        {
            var path = $"{userId}/{fileName}";
            return _client.Storage.From("avatars").GetPublicUrl(path);
        }

        /// <summary>
        /// Upload ảnh flashcard lên Storage
        /// </summary>
        public async Task<string?> UploadFlashcardImageAsync(string classroomId, string setId, byte[] imageData, string fileName)
        {
            var path = $"{classroomId}/{setId}/{fileName}";
            try
            {
                await _client.Storage.From("flashcard-images").Upload(imageData, path, new Supabase.Storage.FileOptions
                {
                    Upsert = true
                });
                return _client.Storage.From("flashcard-images").GetPublicUrl(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Upload flashcard image error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Upload audio flashcard lên Storage
        /// </summary>
        public async Task<string?> UploadFlashcardAudioAsync(string classroomId, string setId, byte[] audioData, string fileName)
        {
            var path = $"{classroomId}/{setId}/{fileName}";
            try
            {
                await _client.Storage.From("flashcard-audios").Upload(audioData, path, new Supabase.Storage.FileOptions
                {
                    Upsert = true
                });
                return _client.Storage.From("flashcard-audios").GetPublicUrl(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Upload flashcard audio error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper Methods
        private async Task<string> GenerateRoomCodeAsync()
        {
            try { var result = await _client.Rpc("generate_room_code", null); return result.Content ?? "TEMP1234"; }
            catch { return "TEMP1234"; }
        }
        #endregion

        #region Realtime Subscriptions

        /// <summary>
        /// Lắng nghe sự kiện có thành viên mới tham gia vào phòng
        /// </summary>
        /// <param name="classroomId">ID phòng học</param>
        /// <param name="onMemberJoined">Hàm callback xử lý khi có user mới</param>
        public async Task SubscribeToClassroomMembersAsync(string classroomId, Action<Member> onMemberJoined)
        {
            try
            {
                // 1. Đảm bảo kết nối Socket
                await _client.Realtime.ConnectAsync();

                // 2. Tạo kênh (Channel) riêng cho phòng này
                var channel = _client.Realtime.Channel($"room:{classroomId}");

                // 3. Đăng ký lắng nghe sự kiện INSERT trên bảng 'members'
                var options = new PostgresChangesOptions("public", "members")
                {
                    Filter = $"classroom_id=eq.{classroomId}"
                };

                channel.Register(options);

                channel.AddPostgresChangeHandler(ListenType.Inserts, (sender, change) =>
                {
                    try
                    {
                        // change.Model sẽ tự động deserialize thành Member
                        var member = change.Model<Member>();

                        if (member != null)
                        {
                            // Gọi callback để UI cập nhật
                            onMemberJoined?.Invoke(member);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Realtime Error] Parse member failed: {ex.Message}");
                    }
                });

                // 4. Bắt đầu lắng nghe
                await channel.Subscribe();
                Debug.WriteLine($"[SupabaseService] Subscribed to members of room {classroomId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Realtime subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Hủy đăng ký lắng nghe (khi thoát phòng)
        /// </summary>
        public async Task UnsubscribeFromClassroomAsync(string classroomId)
        {
            try
            {
                var channel = _client.Realtime.Channel($"room:{classroomId}");
                if (channel != null)
                {
                    channel.Unsubscribe();
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Unsubscribe failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Lắng nghe cập nhật thông tin phòng học
        /// </summary>
        public async Task SubscribeToClassroomAsync(string classroomId, Action<Classroom> onUpdate)
        {
            try
            {
                await _client.Realtime.ConnectAsync();

                var channel = _client.Realtime.Channel($"classroom:{classroomId}");

                var options = new PostgresChangesOptions("public", "classrooms")
                {
                    Filter = $"id=eq.{classroomId}"
                };

                channel.Register(options);

                channel.AddPostgresChangeHandler(ListenType.Updates, (sender, change) =>
                {
                    try
                    {
                        var classroom = change.Model<Classroom>();
                        if (classroom != null)
                        {
                            onUpdate?.Invoke(classroom);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Realtime Error] Parse classroom failed: {ex.Message}");
                    }
                });

                await channel.Subscribe();
                Debug.WriteLine($"[SupabaseService] Subscribed to classroom {classroomId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Classroom subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Lắng nghe nhiều loại sự kiện cho bảng members (Insert, Update, Delete)
        /// </summary>
        /// <param name="classroomId">ID phòng học</param>
        /// <param name="onInsert">Callback khi có thành viên mới</param>
        /// <param name="onUpdate">Callback khi thành viên được cập nhật</param>
        /// <param name="onDelete">Callback khi thành viên rời phòng</param>
        public async Task SubscribeToClassroomMembersAllEventsAsync(
            string classroomId,
            Action<Member>? onInsert = null,
            Action<Member>? onUpdate = null,
            Action<Member>? onDelete = null)
        {
            try
            {
                await _client.Realtime.ConnectAsync();

                var channel = _client.Realtime.Channel($"room-all:{classroomId}");

                var options = new PostgresChangesOptions("public", "members")
                {
                    Filter = $"classroom_id=eq.{classroomId}"
                };

                channel.Register(options);

                // Lắng nghe tất cả các sự kiện
                channel.AddPostgresChangeHandler(ListenType.All, (sender, change) =>
                {
                    try
                    {
                        var member = change.Model<Member>();
                        if (member == null) return;

                        switch (change.Event)
                        {
                            case RealtimeConstants.EventType.Insert:
                                onInsert?.Invoke(member);
                                break;
                            case RealtimeConstants.EventType.Update:
                                onUpdate?.Invoke(member);
                                break;
                            case RealtimeConstants.EventType.Delete:
                                onDelete?.Invoke(member);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Realtime Error] Lỗi parse member: {ex.Message}");
                    }
                });

                await channel.Subscribe();
                Debug.WriteLine($"[SupabaseService] Đã đăng ký lắng nghe tất cả sự kiện members của phòng {classroomId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Lỗi đăng ký lắng nghe: {ex.Message}");
            }
        }
        /// <summary>
        /// Tham gia Presence của phòng (theo dõi ai đang online)
        /// </summary>
        /// <param name="classroomId">ID phòng học</param>
        /// <param name="userId">ID người dùng</param>
        /// <param name="displayName">Tên hiển thị</param>
        /// <param name="onPresenceSync">Callback khi danh sách online thay đổi</param>
        public async Task JoinRoomPresenceAsync(string classroomId, string userId, string? displayName, Action<List<string>> onPresenceSync)
        {
            try
            {
                await _client.Realtime.ConnectAsync();

                string channelName = $"presence:{classroomId}";

                // Dọn dẹp kênh cũ nếu tồn tại
                if (_activeChannels.TryGetValue(channelName, out var oldChannel))
                {
                    oldChannel.Unsubscribe();
                    _activeChannels.Remove(channelName);
                }

                var channel = _client.Realtime.Channel(channelName);
                _activeChannels[channelName] = channel;

                // Đăng ký presence với key duy nhất (userId để định danh mỗi user)
                string presenceKey = userId;
                var presence = channel.Register<UserPresence>(presenceKey);

                // Thêm handler cho sự kiện Sync
                presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (sender, args) =>
                {
                    try
                    {
                        var onlineUserIds = new List<string>();

                        foreach (var presences in presence.CurrentState.Values)
                        {
                            foreach (var p in presences)
                            {
                                if (!string.IsNullOrEmpty(p.UserId))
                                {
                                    onlineUserIds.Add(p.UserId);
                                }
                            }
                        }

                        onPresenceSync?.Invoke(onlineUserIds.Distinct().ToList());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Presence Error] Lỗi xử lý Sync: {ex.Message}");
                    }
                });

                // Subscribe và kiểm tra trạng thái
                await channel.Subscribe();
                if (channel.State != ChannelState.Joined)
                {
                    Debug.WriteLine("[Presence] Subscribe thất bại");
                    return;
                }

                // Track payload
                var payload = new UserPresence
                {
                    UserId = userId,
                    DisplayName = displayName ?? "Unknown"
                };
                await presence.Track(payload);

                Debug.WriteLine($"[Presence] Đã tham gia phòng {classroomId} với userId {userId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Presence] Lỗi tham gia: {ex.Message}");
            }
        }

        /// <summary>
        /// Rời khỏi Presence của phòng (ngừng theo dõi online)
        /// </summary>
        public async Task LeaveRoomPresenceAsync(string classroomId, string userId)
        {
            string channelName = $"presence:{classroomId}";
            if (_activeChannels.TryGetValue(channelName, out var channel))
            {
                try
                {
                    // Đăng ký lại với cùng key để untrack
                    var presence = channel.Register<UserPresence>(userId);

                    await presence.Untrack();
                    channel.Unsubscribe();

                    _activeChannels.Remove(channelName);

                    Debug.WriteLine($"[Presence] Đã rời phòng {classroomId}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Presence] Lỗi rời phòng: {ex.Message}");
                }
            }
        }

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