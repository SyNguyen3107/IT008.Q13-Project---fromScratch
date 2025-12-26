using EasyFlips.Interfaces;
using EasyFlips.Models;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace EasyFlips.Services
{
    public class FlashcardBroadcast : BaseBroadcast<Dictionary<string, object>> { }

    public class SupabaseService
    {
        #region Fields & Init
        private readonly Supabase.Client _client;
        private readonly CustomFileSessionHandler _sessionHandler;
        private readonly Dictionary<string, RealtimeChannel> _activeChannels = new();
        private readonly Dictionary<string, RealtimeBroadcast<FlashcardBroadcast>> _activeBroadcasts = new();

        public Supabase.Client Client => _client;

        public SupabaseService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folderName = "EasyFlips";

            // Check instance thứ 2 để debug trên 1 máy
            var procName = Process.GetCurrentProcess().ProcessName;
            if (Process.GetProcessesByName(procName).Length > 1) folderName = "EasyFlips_Instance2";

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

        public async Task InitializeAsync()
        {
            try
            {
                var loadedSession = _sessionHandler.LoadSession();
                await _client.InitializeAsync();
                if (_client.Auth.CurrentSession == null && loadedSession != null)
                {
                    if (!string.IsNullOrEmpty(loadedSession.AccessToken))
                        await _client.Auth.SetSession(loadedSession.AccessToken, loadedSession.RefreshToken);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Init Error] {ex.Message}"); }
        }
        #endregion

        #region Profile Operations

        public async Task<Profile?> GetProfileAsync(string userId)
        {
            return await _client.From<Profile>().Where(x => x.Id == userId).Single();
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
                Debug.WriteLine($"[SupabaseService] GetUserProfileAsync error: {ex.Message}");
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
            var result = await _client.From<Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.DisplayName, displayName)
                .Set(p => p.AvatarUrl, avatarUrl)
                .Set(p => p.UpdatedAt, DateTime.UtcNow)
                .Update();
            return result.Models.FirstOrDefault();
        }

        public async Task<UserProfile?> GetUserProfileAsync(string userId)
        {
            try { return await _client.From<UserProfile>().Where(x => x.UserId == userId).Single(); }
            catch { return null; }
        }

        #endregion

        #region Classroom & Member Operations

        // --- Classroom ---
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
                IsActive = true
            };
            var json = JsonConvert.SerializeObject(classroom);
            Console.WriteLine(json);

            Console.WriteLine(json);

            Console.WriteLine(json);

            Console.WriteLine(json);

            Console.WriteLine(json);

            var result = await _client.From<Classroom>().Insert(classroom);
            return result.Models.FirstOrDefault();
        }

        public async Task<Classroom?> GetClassroomAsync(string classroomId)
        {
            return await _client.From<Classroom>().Where(x => x.Id == classroomId).Single();
        }

        public async Task<List<UserClassroom>> GetUserClassroomsAsync(string userId)
        {
            await _client.Rpc("get_user_classrooms", new Dictionary<string, object> { { "p_user_id", userId } });
            return new List<UserClassroom>(); // Cần map nếu dùng RPC này
        }

        public async Task<bool> IsHostAsync(string classroomId, string userId)
        {
            var c = await GetClassroomAsync(classroomId);
            return c?.HostId == userId;
        }

        // --- Members ---

        /// <summary>
        /// [FIXED] Gửi tín hiệu Heartbeat (cập nhật last_active)
        /// </summary>
        public async Task SendHeartbeatAsync(string classroomId, string userId)
        {
            try
            {
                await _client.From<Member>()
                             .Where(x => x.ClassroomId == classroomId && x.UserId == userId)
                             .Set(x => x.LastActive, DateTime.UtcNow)
                             .Update();
            }
            catch (Exception ex) { Debug.WriteLine($"[Heartbeat] Failed: {ex.Message}"); }
        }

        public async Task<Member?> AddMemberAsync(string classroomId, string userId, string role = "member")
        {
            var member = new Member
            {
                Id = Guid.NewGuid().ToString(),
                ClassroomId = classroomId,
                UserId = userId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            };
            var result = await _client.From<Member>().Insert(member);
            return result.Models.FirstOrDefault();
        }

        public async Task<JoinClassroomResult> JoinClassroomByCodeAsync(string roomCode, string userId)
        {
            await _client.Rpc("join_classroom_by_code", new Dictionary<string, object> { { "p_room_code", roomCode }, { "p_user_id", userId } });
            return new JoinClassroomResult { Success = true, Message = "Joined" };
        }

        public async Task<List<MemberWithProfile>> GetClassroomMembersWithProfileAsync(string classroomId)
        {
            try
            {
                var members = await _client.From<Member>().Where(x => x.ClassroomId == classroomId).Get();
                if (members.Models.Count == 0) return new List<MemberWithProfile>();

                var userIds = members.Models.Select(m => m.UserId).ToList();
                var profiles = await _client.From<Profile>().Filter("id", Operator.In, userIds).Get();

                return members.Models.Select(m => new MemberWithProfile
                {
                    MemberId = m.Id,
                    UserId = m.UserId,
                    ClassroomId = m.ClassroomId,
                    Role = m.Role,
                    DisplayName = profiles.Models.FirstOrDefault(p => p.Id == m.UserId)?.DisplayName ?? "Unknown",
                    AvatarUrl = profiles.Models.FirstOrDefault(p => p.Id == m.UserId)?.AvatarUrl,
                    LastActive = m.LastActive
                }).ToList();
            }
            catch { return new List<MemberWithProfile>(); }
        }

        public async Task<bool> RemoveMemberAsync(string classroomId, string userId)
        {
            await _client.From<Member>().Where(x => x.ClassroomId == classroomId && x.UserId == userId).Delete();
            return true;
        }

        #endregion

        #region Storage & Avatar

        public async Task<string?> UploadAvatarAsync(string userId, byte[] data, string fileName)
        {
            var path = $"{userId}/{fileName}";
            await _client.Storage.From("avatars").Upload(data, path, new Supabase.Storage.FileOptions { Upsert = true });
            return _client.Storage.From("avatars").GetPublicUrl(path);
        }

        public async Task<string?> UploadAvatarFromFileAsync(string userId, string filePath)
        {
            if (!File.Exists(filePath)) return null;
            var data = await File.ReadAllBytesAsync(filePath);
            var ext = Path.GetExtension(filePath).ToLower();
            var fileName = $"avatar_{DateTime.UtcNow.Ticks}{ext}";
            return await UploadAvatarAsync(userId, data, fileName);
        }

        public async Task<string?> ReplaceAvatarWithBackupAsync(string userId, string newFilePath)
        {
            var url = await UploadAvatarFromFileAsync(userId, newFilePath);
            if (url != null) await UpdateProfileAvatarAsync(userId, url);
            return url;
        }

        public async Task<bool> UpdateProfileAvatarAsync(string userId, string avatarUrl)
        {
            await _client.From<Profile>().Where(x => x.Id == userId).Set(x => x.AvatarUrl, avatarUrl).Update();
            return true;
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

        #region Game Logic & Hybrid Sync

        public async Task<Deck?> GetDeckByClassroomIdAsync(string classroomId)
        {
            try
            {
                // 1. Lấy classroom trước để có DeckId
                var classroom = await _client.From<Classroom>()
                    .Where(c => c.Id == classroomId)
                    .Single();

                if (string.IsNullOrEmpty(classroom?.DeckId)) return null;

                // 2. Lấy thông tin Deck
                var deck = await _client.From<Deck>()
                    .Where(d => d.Id == classroom.DeckId)
                    .Single();

                if (deck == null) return null;

                // 3. Lấy tất cả Cards thuộc Deck này
                var response = await _client.From<Card>()
                    .Where(c => c.DeckId == deck.Id)
                    .Get();

                // [NHẤN NHÁ LOGIC] Sắp xếp để khi "Next thẻ" không bị lộn xộn
                deck.Cards = response.Models?.OrderBy(c => c.Id).ToList() ?? new List<Card>();

                return deck;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Supabase] Lỗi lấy Deck: {ex.Message}");
                return null;
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
        /// [WRAPPER] Subscribe vào kênh flashcard sync của phòng học.
        /// Đây là hàm wrapper đơn giản hóa việc tham gia kênh Realtime.
        /// </summary>
        /// <param name="classroomId">ID phòng học.</param>
        /// <param name="onStateReceived">Callback khi nhận được trạng thái mới.</param>
        /// <returns>Kết quả subscribe (Success/Fail).</returns>
        public async Task<ChannelSubscriptionResult> SubscribeToFlashcardChannelAsync(
            string classroomId,
            Action<FlashcardSyncState> onStateReceived)
        {
            var result = new ChannelSubscriptionResult
            {
                ChannelName = $"flashcard-sync:{classroomId}"
            };

            try
            {
                await _client.Realtime.ConnectAsync();

                // Hủy kênh cũ nếu có
                if (_activeChannels.TryGetValue(result.ChannelName, out var oldChannel))
                {
                    oldChannel.Unsubscribe();
                    _activeChannels.Remove(result.ChannelName);
                    _activeBroadcasts.Remove(result.ChannelName);
                }

                var channel = _client.Realtime.Channel(result.ChannelName);
                _activeChannels[result.ChannelName] = channel;

                var broadcast = channel.Register<FlashcardBroadcast>(true, false);
                _activeBroadcasts[result.ChannelName] = broadcast;

                broadcast.AddBroadcastEventHandler((sender, args) =>
                {
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
                                    // Log JSON để debug
                                    var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                                    Debug.WriteLine($"[FlashcardSync] Received JSON:\n{json}");

                                    onStateReceived?.Invoke(state);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[FlashcardSync] Parse error: {ex.Message}");
                        }
                    }
                });

                await channel.Subscribe();

                result.Success = true;
                Debug.WriteLine($"[FlashcardSync] Subscribed to channel: {result.ChannelName}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"[FlashcardSync] Subscribe failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// [TEST] Gửi gói tin mẫu để test broadcast.
        /// </summary>
        public async Task<bool> SendTestBroadcastAsync(string classroomId, string hostId)
        {
            try
            {
                var testState = new FlashcardSyncState
                {
                    ClassroomId = classroomId,
                    DeckId = "test-deck-001",
                    CurrentCardId = "test-card-001",
                    CurrentCardIndex = 0,
                    TotalCards = 10,
                    IsFlipped = false,
                    Action = FlashcardAction.ShowCard,
                    TriggeredBy = hostId,
                    TimeRemaining = 15,
                    IsSessionActive = true,
                    IsPaused = false,
                    Phase = GamePhase.Question
                };

                await BroadcastFlashcardStateAsync(classroomId, testState);
                Debug.WriteLine($"[TEST] Sent test broadcast to room {classroomId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TEST] Send test broadcast failed: {ex.Message}");
                return false;
            }
        }

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

                // Parse phase enum
                var phaseStr = payload.GetValueOrDefault("phase")?.ToString();
                if (Enum.TryParse<GamePhase>(phaseStr, out var phase))
                {
                    state.Phase = phase;
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
                    { "event_type", "FLASHCARD_SYNC" },
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
                    { "is_paused", state.IsPaused },
                    { "phase", state.Phase.ToString() }
};



                Debug.WriteLine("[Broadcast] Sending payload...");
                await broadcast.Send("FLASHCARD_SYNC", payload);
                Debug.WriteLine("[Broadcast] Sent payload.");


            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FlashcardSync] Lỗi broadcast: {ex.Message}");
            }
        }

        // --- Wrappers cho GameViewModel ---

        public async Task StartFlashcardSessionAsync(string classroomId, string hostId, string deckId, string firstCardId, int totalCards, int timePerCard)
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
                IsSessionActive = true
            };
            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        public async Task FlipCardAsync(string classroomId, string hostId, string deckId, string cardId, int cardIndex, int totalCards, int timeRemaining)
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
                IsSessionActive = true
            };
            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        public async Task NextCardAsync(string classroomId, string hostId, string deckId, string nextCardId, int nextCardIndex, int totalCards, int timePerCard)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                DeckId = deckId,
                CurrentCardId = nextCardId,
                CurrentCardIndex = nextCardIndex,
                TotalCards = totalCards,
                IsFlipped = false,
                Action = FlashcardAction.NextCard,
                TriggeredBy = hostId,
                TimeRemaining = timePerCard,
                IsSessionActive = true
            };
            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        public async Task EndFlashcardSessionAsync(string classroomId, string hostId)
        {
            var state = new FlashcardSyncState
            {
                ClassroomId = classroomId,
                Action = FlashcardAction.EndSession,
                TriggeredBy = hostId,
                IsSessionActive = false
            };
            await BroadcastFlashcardStateAsync(classroomId, state);
        }

        public async Task<SubscribeResult> SubscribeToFlashcardChannelAsync(
            string classroomId,
            Action<FlashcardSyncState> onStateReceived,
            Action<ScoreSubmission>? onScoreReceived = null)
        {
            var result = new SubscribeResult { ChannelName = $"hybrid-sync:{classroomId}" };
            try
            {
                await _client.Realtime.ConnectAsync();

                string channelName = $"room-hybrid:{classroomId}";
                if (_activeChannels.TryGetValue(channelName, out var old))
                {
                    old.Unsubscribe();
                    _activeChannels.Remove(channelName);
                    _activeBroadcasts.Remove(channelName);
                }

                var channel = _client.Realtime.Channel(channelName);
                _activeChannels[channelName] = channel;

                // Đăng ký Broadcast (true = lắng nghe broadcast, false = không ack)
                var broadcast = channel.Register<FlashcardBroadcast>(true, false);
                _activeBroadcasts[channelName] = broadcast;

                broadcast.AddBroadcastEventHandler((sender, args) =>
                {
                    Debug.WriteLine("[FlashcardSync] Nhận broadcast event");
                    if (args.Payload == null) { Debug.WriteLine("[FlashcardSync] Payload NULL"); return; }
                    foreach (var kv in args.Payload) { Debug.WriteLine($" {kv.Key} = {kv.Value}"); }

                    var payload = args.Payload as Dictionary<string, object>;
                    var eventType = payload?.GetValueOrDefault("event_type")?.ToString();


                    if (eventType == "FLASHCARD_SYNC")
                    {
                        try
                        {
                            payload = args.Payload;
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
                            Debug.WriteLine($"[FlashcardSync] Parse error: {ex.Message}");
                        }
                    }
                    else if (eventType == "FLASHCARD_SCORE" && onScoreReceived != null)
                    {
                        try
                        {
                            var json = JsonConvert.SerializeObject(args.Payload);
                            var submission = JsonConvert.DeserializeObject<ScoreSubmission>(json);
                            if (submission != null) onScoreReceived?.Invoke(submission);
                        }
                        catch { }
                    }
                });

                Debug.WriteLine($"[FlashcardSync] Đang subscribe channel: {channelName}");

                await channel.Subscribe();

                result.Success = true;
                Debug.WriteLine($"[FlashcardSync] Subscribed to channel: {result.ChannelName}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"[FlashcardSync] Subscribe failed: {ex.Message}");
            }
        }


        // Wrapper để tương thích ngược với GameViewModel cũ
        public async Task JoinFlashcardSyncChannelAsync(string classroomId, string userId, Action<FlashcardSyncState> onStateReceived)
        {
            await SubscribeToFlashcardChannelAsync(classroomId, onStateReceived);
        }

        public async Task LeaveFlashcardSyncChannelAsync(string classroomId)
        {
            string channelName = $"room-hybrid:{classroomId}";
            if (_activeChannels.TryGetValue(channelName, out var channel))
            {
                channel.Unsubscribe();
                _activeChannels.Remove(channelName);
                _activeBroadcasts.Remove(channelName);
            }
            await Task.CompletedTask;
        }

        public async Task SendFlashcardScoreAsync(string classroomId, string userId, int score, int correctCount, int totalAnswered)
        {
            try
            {
                string channelName = $"room-hybrid:{classroomId}";
                if (!_activeBroadcasts.TryGetValue(channelName, out var broadcast)) return;

                var payload = new Dictionary<string, object>
                {
                    { "user_id", userId }, { "score", score },
                    { "correct_count", correctCount }, { "total_answered", totalAnswered },
                    { "timestamp", DateTime.UtcNow }
                };
                await broadcast.Send("FLASHCARD_SCORE", payload);
            }
            catch (Exception ex) { Debug.WriteLine($"[Score] Failed: {ex.Message}"); }
        }

        #endregion

        #region Helpers
        private async Task<string> GenerateRoomCodeAsync()
        {
            try { var result = await _client.Rpc("generate_room_code", null); return result.Content ?? "TEMP1234"; }
            catch { return "TEMP1234"; }
        }
        #endregion


        public class CustomFileSessionHandler : IGotrueSessionPersistence<Session>
        {
            private readonly string _cachePath;
            private readonly string _fileName = ".gotrue.cache";
            private readonly JsonSerializerSettings _jsonSettings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };

            public CustomFileSessionHandler(string cachePath) { _cachePath = cachePath; }

            public void SaveSession(Session session)
            {
                try
                {
                    var fullPath = Path.Combine(_cachePath, _fileName);
                    if (session == null) { if (File.Exists(fullPath)) File.Delete(fullPath); return; }
                    File.WriteAllText(fullPath, JsonConvert.SerializeObject(session, Formatting.Indented, _jsonSettings));
                }
                catch { }
            }

            public Session? LoadSession()
            {
                try
                {
                    var fullPath = Path.Combine(_cachePath, _fileName);
                    if (!File.Exists(fullPath)) return null;
                    return JsonConvert.DeserializeObject<Session>(File.ReadAllText(fullPath), _jsonSettings);
                }
                catch { return null; }
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
}


