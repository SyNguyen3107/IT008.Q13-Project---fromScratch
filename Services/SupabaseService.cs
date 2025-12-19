using EasyFlips.Models;
using Newtonsoft.Json;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
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
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;

namespace EasyFlips.Services
{
    /// <summary>
    /// Broadcast model cho flashcard sync.
    /// </summary>
    public class FlashcardBroadcast : BaseBroadcast<Dictionary<string, object>> { }

    /// <summary>
    /// Service trung gian xử lý mọi giao tiếp với Supabase (Auth, Database, Realtime, Storage).
    /// </summary>
    public class SupabaseService
    {
        private readonly Supabase.Client _client;
        private readonly CustomFileSessionHandler _sessionHandler;
        private readonly Dictionary<string, RealtimeChannel> _activeChannels = new Dictionary<string, RealtimeChannel>();
        private readonly Dictionary<string, RealtimeBroadcast<FlashcardBroadcast>> _activeBroadcasts = new Dictionary<string, RealtimeBroadcast<FlashcardBroadcast>>();
        public Supabase.Client Client => _client;

        /// <summary>
        /// Khởi tạo SupabaseService, thiết lập đường dẫn cache và cấu hình Client.
        /// </summary>
        public SupabaseService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Mặc định là thư mục chính
            var folderName = "EasyFlips";

            // [MẸO] Kiểm tra xem có đang chạy instance thứ 2 không 
            // (Bằng cách check xem process EasyFlips có đang chạy nhiều hơn 1 không)
            var procName = Process.GetCurrentProcess().ProcessName;
            if (Process.GetProcessesByName(procName).Length > 1)
            {
                // Nếu đây là cửa sổ thứ 2, dùng thư mục cache tạm khác
                // Để không đụng chạm vào session của cửa sổ 1
                folderName = "EasyFlips_Instance2";
            }

            var cacheDir = Path.Combine(appData, folderName);
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

        /// <summary>
        /// Khởi động Client và cố gắng khôi phục phiên đăng nhập (Session) từ file cache cũ.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                var loadedSession = _sessionHandler.LoadSession();
                await _client.InitializeAsync();

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

        /// <summary>
        /// Gửi tín hiệu Heartbeat (cập nhật last_active) để báo hiệu user vẫn còn kết nối.
        /// </summary>
        public async Task SendHeartbeatAsync(string classroomId, string userId)
        {
            try
            {
                await _client.From<Member>()
                             .Where(x => x.ClassroomId == classroomId && x.UserId == userId)
                             .Set(x => x.LastActive, DateTime.UtcNow) // Cập nhật cột LastActive theo giờ UTC
                             .Update();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Heartbeat] Failed: {ex.Message}");
            }
        }

        #region Profile Operations

        /// <summary>
        /// Lấy thông tin Profile cơ bản của người dùng.
        /// </summary>
        /// <param name="userId">ID người dùng (GUID).</param>
        /// <returns>Đối tượng Profile hoặc null.</returns>
        public async Task<Profile?> GetProfileAsync(string userId)
        {
            var result = await _client.From<Profile>().Where(x => x.Id == userId).Single();
            return result;
        }

        /// <summary>
        /// Lấy thông tin UserProfile chi tiết (Bảng mở rộng nếu có).
        /// </summary>
        /// <param name="userId">ID người dùng.</param>
        public async Task<UserProfile?> GetUserProfileAsync(string userId)
        {
            try
            {
                var profile = await _client
                    .From<UserProfile>()
                    .Where(x => x.UserId == userId)
                    .Single();

                return profile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SupabaseService] GetUserProfileAsync error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cập nhật thông tin hiển thị và avatar cho người dùng.
        /// </summary>
        /// <param name="userId">ID người dùng.</param>
        /// <param name="displayName">Tên hiển thị mới.</param>
        /// <param name="avatarUrl">Đường dẫn ảnh đại diện mới.</param>
        public async Task<Profile?> UpdateProfileAsync(string userId, string? displayName, string? avatarUrl)
        {
            var result = await _client
                .From<Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.DisplayName, displayName)
                .Set(p => p.AvatarUrl, avatarUrl)
                .Set(p => p.UpdatedAt, DateTime.UtcNow)
                .Update();

            return result.Models.FirstOrDefault();
        }


        /// <summary>
        /// Tìm kiếm người dùng dựa trên email (gần đúng).
        /// </summary>
        /// <param name="emailQuery">Chuỗi email cần tìm.</param>
        /// <returns>Danh sách các Profile khớp.</returns>
        public async Task<List<Profile>> SearchProfilesByEmailAsync(string emailQuery)
        {
            var result = await _client.From<Profile>().Where(x => x.Email.Contains(emailQuery)).Get();
            return result.Models;
        }
        #endregion

        #region Classroom Operations

        /// <summary>
        /// Tạo một phòng học mới.
        /// </summary>
        /// <param name="name">Tên phòng học.</param>
        /// <param name="description">Mô tả phòng học (tùy chọn).</param>
        /// <param name="ownerId">ID của người tạo (Host).</param>
        /// <param name="waitTime">Thời gian chờ tự động bắt đầu (giây), mặc định 300s.</param>
        /// <returns>Đối tượng Classroom vừa tạo.</returns>
        public async Task<Classroom?> CreateClassroomAsync(string name, string? description, string ownerId, int waitTime = 300)
        {
            var roomCode = await GenerateRoomCodeAsync();
            var classroom = new Classroom
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                RoomCode = roomCode,
                HostId = ownerId,
                WaitTime = waitTime,
                IsActive = true,
            };
            var json = JsonConvert.SerializeObject(classroom);
            Console.WriteLine(json);

            var result = await _client.From<Classroom>().Insert(classroom);
            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Lấy thông tin chi tiết một phòng học theo ID.
        /// </summary>
        /// <param name="classroomId">ID của phòng.</param>
        public async Task<Classroom?> GetClassroomAsync(string classroomId)
        {
            var result = await _client.From<Classroom>().Where(x => x.Id == classroomId).Single();
            return result;
        }

        /// <summary>
        /// Cập nhật thông tin tên và mô tả của phòng học.
        /// </summary>
        public async Task<Classroom?> UpdateClassroomAsync(string classroomId, string name, string? description)
        {
            var classroom = new Classroom { Id = classroomId, Name = name, Description = description, UpdatedAt = DateTime.UtcNow };
            var result = await _client.From<Classroom>().Update(classroom);
            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Vô hiệu hóa phòng học (Soft Delete).
        /// </summary>
        public async Task<bool> DeactivateClassroomAsync(string classroomId)
        {
            var classroom = new Classroom { Id = classroomId, IsActive = false, UpdatedAt = DateTime.UtcNow };
            var result = await _client.From<Classroom>().Update(classroom);
            return result.Models.Any();
        }

        /// <summary>
        /// Giải tán phòng hoàn toàn (Hard Delete) - Xóa Members trước rồi xóa Classroom.
        /// </summary>
        /// <param name="classroomId">ID phòng cần giải tán.</param>
        /// <param name="hostId">ID của Host (để xác thực quyền).</param>
        /// <returns>Tuple (Thành công/Thất bại, Thông báo lỗi).</returns>
        public async Task<(bool Success, string Message)> DissolveClassroomAsync(string classroomId, string hostId)
        {
            try
            {
                var classroom = await GetClassroomAsync(classroomId);
                if (classroom == null) return (false, "Phòng không tồn tại.");

                if (classroom.HostId != hostId) return (false, "Bạn không có quyền giải tán phòng này.");

                // Xóa members trước để tránh lỗi Foreign Key
                await _client.From<Member>().Where(x => x.ClassroomId == classroomId).Delete();
                Debug.WriteLine($"[SupabaseService] Deleted all members from room {classroomId}");

                // Xóa phòng
                await _client.From<Classroom>().Where(x => x.Id == classroomId).Delete();
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
        /// Lấy danh sách thành viên trong phòng kèm theo thông tin Profile (Tên, Avatar).
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <returns>Danh sách MemberWithProfile.</returns>
        public async Task<List<MemberWithProfile>> GetClassroomMembersWithProfileAsync(string classroomId)
        {
            try
            {
                // 1. Lấy danh sách Member (1 Request)
                var members = await _client.From<Member>()
                                           .Where(x => x.ClassroomId == classroomId)
                                           .Get();

                if (members.Models.Count == 0) return new List<MemberWithProfile>();

                // 2. Lấy danh sách ID của các user
                var userIds = members.Models.Select(m => m.UserId).ToList();

                // 3. Lấy TẤT CẢ Profile của các user này trong 1 Request duy nhất (Dùng Filter IN)
                var profilesResponse = await _client.From<Profile>()
                                                    .Filter("id", Supabase.Postgrest.Constants.Operator.In, userIds)
                                                    .Get();
                var profiles = profilesResponse.Models;

                // 4. Ghép dữ liệu lại trong bộ nhớ (RAM) - cực nhanh
                var result = new List<MemberWithProfile>();

                foreach (var member in members.Models)
                {
                    var profile = profiles.FirstOrDefault(p => p.Id == member.UserId);
                    result.Add(new MemberWithProfile
                    {
                        MemberId = member.Id,
                        UserId = member.UserId,
                        ClassroomId = member.ClassroomId,
                        Role = member.Role,
                        DisplayName = profile?.DisplayName ?? "Unknown",
                        LastActive = member.LastActive,
                        AvatarUrl = profile?.AvatarUrl
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Error: {ex.Message}");
                return new List<MemberWithProfile>();
            }
        }

        /// <summary>
        /// Kiểm tra xem một user có phải là Host của phòng không.
        /// </summary>
        public async Task<bool> IsHostAsync(string classroomId, string userId)
        {
            var classroom = await GetClassroomAsync(classroomId);
            return classroom?.HostId == userId;
        }

        /// <summary>
        /// Lấy danh sách các phòng mà user đã tham gia (sử dụng RPC).
        /// </summary>
        public async Task<List<UserClassroom>> GetUserClassroomsAsync(string userId)
        {
            var result = await _client.Rpc("get_user_classrooms", new Dictionary<string, object> { { "p_user_id", userId } });
            return new List<UserClassroom>(); // Lưu ý: Hàm này đang trả về list rỗng, cần map result.Content
        }
        #endregion

        #region Member Operations

        /// <summary>
        /// Tham gia phòng học bằng Mã Code (Sử dụng RPC Database).
        /// </summary>
        /// <param name="roomCode">Mã phòng 6 ký tự.</param>
        /// <param name="userId">ID người tham gia.</param>
        public async Task<JoinClassroomResult> JoinClassroomByCodeAsync(string roomCode, string userId)
        {
            var result = await _client.Rpc("join_classroom_by_code", new Dictionary<string, object> { { "p_room_code", roomCode }, { "p_user_id", userId } });
            return new JoinClassroomResult { Success = true, Message = "Joined successfully" };
        }

        /// <summary>
        /// Lấy danh sách thô các thành viên trong phòng (chỉ bảng Members).
        /// </summary>
        public async Task<List<Member>> GetClassroomMembersAsync(string classroomId)
        {
            var result = await _client.From<Member>().Where(x => x.ClassroomId == classroomId).Get();
            return result.Models;
        }

        /// <summary>
        /// Thêm thành viên vào phòng thủ công.
        /// </summary>
        public async Task<Member?> AddMemberAsync(string classroomId, string userId, string role = "member")
        {
            var member = new Member { Id = Guid.NewGuid().ToString(), ClassroomId = classroomId, UserId = userId, Role = role, JoinedAt = DateTime.UtcNow };
            var result = await _client.From<Member>().Insert(member);
            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Xóa thành viên khỏi phòng.
        /// </summary>
        public async Task<bool> RemoveMemberAsync(string classroomId, string userId)
        {
            await _client.From<Member>().Where(x => x.ClassroomId == classroomId && x.UserId == userId).Delete();
            return true;
        }

        /// <summary>
        /// Rời khỏi phòng (Alias cho RemoveMemberAsync).
        /// </summary>
        public async Task<bool> LeaveClassroomAsync(string classroomId, string userId)
        {
            return await RemoveMemberAsync(classroomId, userId);
        }

        /// <summary>
        /// Cập nhật vai trò (Role) của thành viên.
        /// </summary>
        public async Task<Member?> UpdateMemberRoleAsync(string classroomId, string userId, string newRole)
        {
            var member = new Member { ClassroomId = classroomId, UserId = userId, Role = newRole };
            var result = await _client.From<Member>().Where(x => x.ClassroomId == classroomId && x.UserId == userId).Update(member);
            return result.Models.FirstOrDefault();
        }
        #endregion

        #region Storage Operations

        /// <summary>
        /// Upload ảnh avatar từ mảng byte.
        /// </summary>
        /// <param name="userId">ID User (dùng làm tên folder).</param>
        /// <param name="imageData">Dữ liệu ảnh.</param>
        /// <param name="fileName">Tên file lưu trữ.</param>
        public async Task<string?> UploadAvatarAsync(string userId, byte[] imageData, string fileName)
        {
            var path = $"{userId}/{fileName}";
            try
            {
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
        /// Upload avatar từ đường dẫn file trên máy và cập nhật vào Profile.
        /// </summary>
        public async Task<string?> UploadAvatarFromFileAsync(string userId, string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                var imageData = await File.ReadAllBytesAsync(filePath);
                var extension = Path.GetExtension(filePath).ToLower();
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
                Debug.WriteLine($"[SupabaseService] Upload avatar from file error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Upload avatar từ Stream (MemoryStream, Camera stream...).
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
        /// Cập nhật trường AvatarUrl trong bảng Profile.
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
        /// Xóa file avatar khỏi Storage.
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
        /// Thay thế avatar: Xóa HẾT ảnh cũ và upload ảnh mới.
        /// </summary>
        public async Task<string?> ReplaceAvatarAsync(string userId, string newFilePath)
        {
            try
            {
                var existingFiles = await _client.Storage.From("avatars").List(userId);

                if (existingFiles != null && existingFiles.Any())
                {
                    var pathsToDelete = existingFiles.Select(f => $"{userId}/{f.Name}").ToList();
                    await _client.Storage.From("avatars").Remove(pathsToDelete);
                }

                return await UploadAvatarFromFileAsync(userId, newFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Replace avatar error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Thay thế avatar an toàn: Upload ảnh mới, giữ lại 1 ảnh gần nhất làm backup.
        /// </summary>
        public async Task<string?> ReplaceAvatarWithBackupAsync(string userId, string newFilePath)
        {
            try
            {
                if (!File.Exists(newFilePath)) return null;

                var existingFiles = await _client.Storage.From("avatars").List(userId);

                if (existingFiles != null && existingFiles.Count >= 2)
                {
                    var sortedFiles = existingFiles.OrderByDescending(f => f.Name).ToList();
                    var filesToDelete = sortedFiles.Skip(1).Select(f => $"{userId}/{f.Name}").ToList();

                    if (filesToDelete.Any())
                    {
                        await _client.Storage.From("avatars").Remove(filesToDelete);
                    }
                }

                var imageData = await File.ReadAllBytesAsync(newFilePath);
                var extension = Path.GetExtension(newFilePath).ToLower();
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
                Debug.WriteLine($"[SupabaseService] Replace avatar with backup error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy Public URL để hiển thị avatar.
        /// </summary>
        public string GetAvatarUrl(string userId, string fileName)
        {
            var path = $"{userId}/{fileName}";
            return _client.Storage.From("avatars").GetPublicUrl(path);
        }

        /// <summary>
        /// Upload hình ảnh cho Flashcard.
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
        /// Upload âm thanh cho Flashcard.
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
        /// <summary>
        /// Gọi RPC Database để sinh mã phòng ngẫu nhiên duy nhất.
        /// </summary>
        private async Task<string> GenerateRoomCodeAsync()
        {
            try { var result = await _client.Rpc("generate_room_code", null); return result.Content ?? "TEMP1234"; }
            catch { return "TEMP1234"; }
        }
        #endregion

        #region Realtime Subscriptions

        /// <summary>
        /// Đăng ký lắng nghe sự kiện: Có thành viên mới vào phòng (Insert).
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="onMemberJoined">Hàm callback xử lý khi có dữ liệu mới.</param>
        public async Task SubscribeToClassroomMembersAsync(string classroomId, Action<Member> onMemberJoined)
        {
            try
            {
                await _client.Realtime.ConnectAsync();
                var channel = _client.Realtime.Channel($"room:{classroomId}");

                var options = new PostgresChangesOptions("public", "members")
                {
                    Filter = $"classroom_id=eq.{classroomId}"
                };

                channel.Register(options);

                channel.AddPostgresChangeHandler(ListenType.Inserts, (sender, change) =>
                {
                    try
                    {
                        var member = change.Model<Member>();
                        if (member != null) onMemberJoined?.Invoke(member);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Realtime Error] Parse member failed: {ex.Message}");
                    }
                });

                await channel.Subscribe();
                Debug.WriteLine($"[SupabaseService] Subscribed to members of room {classroomId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Realtime subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Hủy đăng ký lắng nghe (khi rời phòng hoặc đóng ứng dụng).
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
        /// Đăng ký lắng nghe sự kiện cập nhật thông tin phòng (Update).
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
                        if (classroom != null) onUpdate?.Invoke(classroom);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Realtime Error] Parse classroom failed: {ex.Message}");
                    }
                });

                await channel.Subscribe();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Classroom subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Đăng ký lắng nghe tất cả sự kiện (Thêm, Sửa, Xóa) của thành viên trong phòng.
        /// </summary>
        /// <param name="classroomId">ID phòng.</param>
        /// <param name="onInsert">Callback khi thêm.</param>
        /// <param name="onUpdate">Callback khi sửa.</param>
        /// <param name="onDelete">Callback khi xóa.</param>
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
        /// Tham gia kênh Presence để theo dõi ai đang Online trong phòng.
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="userId">ID người dùng hiện tại.</param>
        /// <param name="displayName">Tên hiển thị.</param>
        /// <param name="onPresenceSync">Callback trả về danh sách UserID đang online.</param>
        public async Task JoinRoomPresenceAsync(string classroomId, string userId, string? displayName, Action<List<string>> onPresenceSync)
        {
            try
            {
                await _client.Realtime.ConnectAsync();

                string channelName = $"presence:{classroomId}";

                if (_activeChannels.TryGetValue(channelName, out var oldChannel))
                {
                    oldChannel.Unsubscribe();
                    _activeChannels.Remove(channelName);
                }

                var channel = _client.Realtime.Channel(channelName);
                _activeChannels[channelName] = channel;

                string presenceKey = userId;
                var presence = channel.Register<UserPresence>(presenceKey);

                presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (sender, args) =>
                {
                    try
                    {
                        var onlineUserIds = new List<string>();
                        foreach (var presences in presence.CurrentState.Values)
                        {
                            foreach (var p in presences)
                            {
                                if (!string.IsNullOrEmpty(p.UserId)) onlineUserIds.Add(p.UserId);
                            }
                        }
                        onPresenceSync?.Invoke(onlineUserIds.Distinct().ToList());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Presence Error] Lỗi xử lý Sync: {ex.Message}");
                    }
                });

                await channel.Subscribe();
                if (channel.State != ChannelState.Joined)
                {
                    Debug.WriteLine("[Presence] Subscribe thất bại");
                    return;
                }

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
        /// Rời khỏi kênh Presence (ngừng báo Online).
        /// </summary>
        public async Task LeaveRoomPresenceAsync(string classroomId, string userId)
        {
            string channelName = $"presence:{classroomId}";
            if (_activeChannels.TryGetValue(channelName, out var channel))
            {
                try
                {
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

        #region Flashcard Sync Operations

        /// <summary>
        /// Tham gia kênh đồng bộ flashcard trong phòng học.
        /// Sử dụng Broadcast để gửi/nhận trạng thái card realtime.
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="userId">ID người dùng hiện tại.</param>
        /// <param name="onStateReceived">Callback khi nhận được trạng thái mới từ Host.</param>
        public async Task JoinFlashcardSyncChannelAsync(
            string classroomId, 
            string userId,
            Action<FlashcardSyncState> onStateReceived)
        {
            try
            {
                await _client.Realtime.ConnectAsync();

                string channelName = $"flashcard-sync:{classroomId}";

                // Hủy kênh cũ nếu có
                if (_activeChannels.TryGetValue(channelName, out var oldChannel))
                {
                    oldChannel.Unsubscribe();
                    _activeChannels.Remove(channelName);
                    _activeBroadcasts.Remove(channelName);
                }

                var channel = _client.Realtime.Channel(channelName);
                _activeChannels[channelName] = channel;

                // Đăng ký Broadcast (true = lắng nghe broadcast, false = không ack)
                var broadcast = channel.Register<FlashcardBroadcast>(true, false);
                _activeBroadcasts[channelName] = broadcast;

                // Lắng nghe sự kiện broadcast
                broadcast.AddBroadcastEventHandler((sender, args) =>
                {
                    // Chỉ xử lý event FLASHCARD_SYNC
                    if (args.Event == "FLASHCARD_SYNC")
                    {
                        try
                        {
                            var payload = args.Payload;
                            if (payload != null)
                            {
                                var state = ParseFlashcardState(payload);
                                if (state != null)
                                {
                                    Debug.WriteLine($"[FlashcardSync] Nhận trạng thái: {state.Action} - Card {state.CurrentCardIndex + 1}/{state.TotalCards}, Lật: {state.IsFlipped}");
                                    onStateReceived?.Invoke(state);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[FlashcardSync] Lỗi parse state: {ex.Message}");
                        }
                    }
                });

                await channel.Subscribe();
                Debug.WriteLine($"[FlashcardSync] Đã tham gia kênh đồng bộ phòng {classroomId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FlashcardSync] Lỗi tham gia kênh: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse Dictionary payload thành FlashcardSyncState.
        /// </summary>
        private FlashcardSyncState? ParseFlashcardState(Dictionary<string, object> payload)
        {
            try
            {
                var state = new FlashcardSyncState
                {
                    ClassroomId = payload.GetValueOrDefault("classroom_id")?.ToString() ?? string.Empty,
                    DeckId = payload.GetValueOrDefault("deck_id")?.ToString() ?? string.Empty,
                    CurrentCardId = payload.GetValueOrDefault("current_card_id")?.ToString() ?? string.Empty,
                    CurrentCardIndex = Convert.ToInt32(payload.GetValueOrDefault("current_card_index", 0)),
                    TotalCards = Convert.ToInt32(payload.GetValueOrDefault("total_cards", 0)),
                    IsFlipped = Convert.ToBoolean(payload.GetValueOrDefault("is_flipped", false)),
                    TriggeredBy = payload.GetValueOrDefault("triggered_by")?.ToString() ?? string.Empty,
                    TimeRemaining = Convert.ToInt32(payload.GetValueOrDefault("time_remaining", 0)),
                    IsSessionActive = Convert.ToBoolean(payload.GetValueOrDefault("is_session_active", false)),
                    IsPaused = Convert.ToBoolean(payload.GetValueOrDefault("is_paused", false))
                };

                // Parse action enum
                var actionStr = payload.GetValueOrDefault("action")?.ToString();
                if (Enum.TryParse<FlashcardAction>(actionStr, out var action))
                {
                    state.Action = action;
                }

                // Parse timestamp
                var timestampStr = payload.GetValueOrDefault("timestamp")?.ToString();
                if (DateTime.TryParse(timestampStr, out var timestamp))
                {
                    state.Timestamp = timestamp;
                }

                return state;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Broadcast trạng thái flashcard mới tới tất cả client trong phòng.
        /// Chỉ Host mới nên gọi phương thức này.
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="state">Trạng thái cần broadcast.</param>
        public async Task BroadcastFlashcardStateAsync(string classroomId, FlashcardSyncState state)
        {
            try
            {
                string channelName = $"flashcard-sync:{classroomId}";

                if (!_activeBroadcasts.TryGetValue(channelName, out var broadcast))
                {
                    Debug.WriteLine($"[FlashcardSync] Chưa tham gia kênh {channelName}");
                    return;
                }

                state.Timestamp = DateTime.UtcNow;
                var payload = new Dictionary<string, object>
                {
                    { "classroom_id", state.ClassroomId },
                    { "deck_id", state.DeckId },
                    { "current_card_id", state.CurrentCardId },
                    { "current_card_index", state.CurrentCardIndex },
                    { "total_cards", state.TotalCards },
                    { "is_flipped", state.IsFlipped },
                    { "action", state.Action.ToString() },
                    { "triggered_by", state.TriggeredBy },
                    { "time_remaining", state.TimeRemaining },
                    { "timestamp", state.Timestamp.ToString("O") },
                    { "is_session_active", state.IsSessionActive },
                    { "is_paused", state.IsPaused }
                };

                await broadcast.Send("FLASHCARD_SYNC", payload);
                Debug.WriteLine($"[FlashcardSync] Đã broadcast: {state.Action} - Card {state.CurrentCardIndex + 1}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FlashcardSync] Lỗi broadcast: {ex.Message}");
            }
        }

        /// <summary>
        /// Host bắt đầu phiên học flashcard.
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="hostId">ID của Host.</param>
        /// <param name="deckId">ID của bộ thẻ.</param>
        /// <param name="firstCardId">ID của card đầu tiên.</param>
        /// <param name="totalCards">Tổng số card trong bộ.</param>
        /// <param name="timePerCard">Thời gian mỗi card (giây).</param>
        public async Task StartFlashcardSessionAsync(
            string classroomId, 
            string hostId, 
            string deckId,
            string firstCardId,
            int totalCards,
            int timePerCard)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                DeckId = deckId,
                CurrentCardId = firstCardId,
                CurrentCardIndex = 0,
                TotalCards = totalCards,
                IsFlipped = false,
                Action = FlashcardAction.StartSession,
                TriggeredBy = hostId,
                TimeRemaining = timePerCard,
                IsSessionActive = true,
                IsPaused = false
            };

            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        /// <summary>
        /// Host hiển thị card (SHOW_CARD).
        /// </summary>
        public async Task ShowCardAsync(
            string classroomId,
            string hostId,
            string deckId,
            string cardId,
            int cardIndex,
            int totalCards,
            int timeRemaining)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                DeckId = deckId,
                CurrentCardId = cardId,
                CurrentCardIndex = cardIndex,
                TotalCards = totalCards,
                IsFlipped = false,
                Action = FlashcardAction.ShowCard,
                TriggeredBy = hostId,
                TimeRemaining = timeRemaining,
                IsSessionActive = true,
                IsPaused = false
            };

            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        /// <summary>
        /// Host lật card (FLIP_CARD).
        /// </summary>
        public async Task FlipCardAsync(
            string classroomId,
            string hostId,
            string deckId,
            string cardId,
            int cardIndex,
            int totalCards,
            int timeRemaining)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                DeckId = deckId,
                CurrentCardId = cardId,
                CurrentCardIndex = cardIndex,
                TotalCards = totalCards,
                IsFlipped = true,
                Action = FlashcardAction.FlipCard,
                TriggeredBy = hostId,
                TimeRemaining = timeRemaining,
                IsSessionActive = true,
                IsPaused = false
            };

            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        /// <summary>
        /// Host chuyển sang card tiếp theo (NEXT_CARD).
        /// Gọi khi Host bấm "Next" hoặc tự động khi hết giờ.
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="hostId">ID của Host.</param>
        /// <param name="deckId">ID của bộ thẻ.</param>
        /// <param name="nextCardId">ID của card tiếp theo.</param>
        /// <param name="nextCardIndex">Index của card tiếp theo.</param>
        /// <param name="totalCards">Tổng số card.</param>
        /// <param name="timePerCard">Thời gian cho card mới (giây).</param>
        public async Task NextCardAsync(
            string classroomId,
            string hostId,
            string deckId,
            string nextCardId,
            int nextCardIndex,
            int totalCards,
            int timePerCard)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                DeckId = deckId,
                CurrentCardId = nextCardId,
                CurrentCardIndex = nextCardIndex,
                TotalCards = totalCards,
                IsFlipped = false, // Card mới luôn hiển thị mặt trước
                Action = FlashcardAction.NextCard,
                TriggeredBy = hostId,
                TimeRemaining = timePerCard,
                IsSessionActive = true,
                IsPaused = false
            };

            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        /// <summary>
        /// Host quay lại card trước đó (PREVIOUS_CARD).
        /// </summary>
        public async Task PreviousCardAsync(
            string classroomId,
            string hostId,
            string deckId,
            string prevCardId,
            int prevCardIndex,
            int totalCards,
            int timePerCard)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                DeckId = deckId,
                CurrentCardId = prevCardId,
                CurrentCardIndex = prevCardIndex,
                TotalCards = totalCards,
                IsFlipped = false,
                Action = FlashcardAction.PreviousCard,
                TriggeredBy = hostId,
                TimeRemaining = timePerCard,
                IsSessionActive = true,
                IsPaused = false
            };

            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        /// <summary>
        /// Host tạm dừng phiên học.
        /// </summary>
        public async Task PauseSessionAsync(
            string classroomId,
            string hostId,
            FlashcardSyncState currentState)
        {
            currentState.Action = FlashcardAction.PauseSession;
            currentState.TriggeredBy = hostId;
            currentState.IsPaused = true;

            await BroadcastFlashcardStateAsync(classroomId, currentState);
        }

        /// <summary>
        /// Host tiếp tục phiên học.
        /// </summary>
        public async Task ResumeSessionAsync(
            string classroomId,
            string hostId,
            FlashcardSyncState currentState)
        {
            currentState.Action = FlashcardAction.ResumeSession;
            currentState.TriggeredBy = hostId;
            currentState.IsPaused = false;

            await BroadcastFlashcardStateAsync(classroomId, currentState);
        }

        /// <summary>
        /// Host kết thúc phiên học flashcard.
        /// </summary>
        public async Task EndFlashcardSessionAsync(string classroomId, string hostId)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                Action = FlashcardAction.EndSession,
                TriggeredBy = hostId,
                IsSessionActive = false,
                IsPaused = false
            };

            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        /// <summary>
        /// Rời khỏi kênh đồng bộ flashcard.
        /// </summary>
        public async Task LeaveFlashcardSyncChannelAsync(string classroomId)
        {
            string channelName = $"flashcard-sync:{classroomId}";
            if (_activeChannels.TryGetValue(channelName, out var channel))
            {
                try
                {
                    channel.Unsubscribe();
                    _activeChannels.Remove(channelName);
                    _activeBroadcasts.Remove(channelName);
                    Debug.WriteLine($"[FlashcardSync] Đã rời kênh đồng bộ phòng {classroomId}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FlashcardSync] Lỗi rời kênh: {ex.Message}");
                }
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Rời tất cả các kênh Realtime đang hoạt động.
        /// Gọi khi đóng ứng dụng hoặc đăng xuất.
        /// </summary>
        public async Task LeaveAllChannelsAsync()
        {
            try
            {
                foreach (var channel in _activeChannels.Values)
                {
                    channel.Unsubscribe();
                }
                _activeChannels.Clear();
                _activeBroadcasts.Clear();
                Debug.WriteLine("[SupabaseService] Đã rời tất cả các kênh Realtime");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseService] Lỗi rời kênh: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        #endregion
    }

    /// <summary>
    /// Class hỗ trợ lưu và đọc Session từ file JSON cục bộ.
    /// </summary>
    public class CustomFileSessionHandler : IGotrueSessionPersistence<Session>
    {
        private readonly string _cachePath;
        private readonly string _fileName = ".gotrue.cache";

        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        public CustomFileSessionHandler(string cachePath)
        {
            _cachePath = cachePath;
        }

        /// <summary>
        /// Lưu session vào file.
        /// </summary>
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

        /// <summary>
        /// Đọc session từ file.
        /// </summary>
        public Session? LoadSession()
        {
            try
            {
                var fullPath = Path.Combine(_cachePath, _fileName);
                if (!File.Exists(fullPath)) return null;

                var json = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(json)) return null;

                var session = JsonConvert.DeserializeObject<Session>(json, _jsonSettings);
                return session;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHandler] Load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Xóa file session (Đăng xuất).
        /// </summary>
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