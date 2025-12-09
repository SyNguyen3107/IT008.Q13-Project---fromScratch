using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Supabase.Realtime;
using Supabase.Realtime.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EasyFlips.Services
{
    // Class hứng dữ liệu linh hoạt
    public class GenericMessage
    {
        // [JsonExtensionData] sẽ gom tất cả các trường (content, user, timestamp...) vào Dictionary này
        [JsonExtensionData]
        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
    }

    public class GenericBroadcast : BaseBroadcast<GenericMessage>
    {
    }

    public class RealtimeService
    {
        private readonly string SupabaseUrl = AppConfig.SupabaseUrl;
        private readonly string SupabaseKey = AppConfig.SupabaseKey;//lấy key từ AppConfig để tránh lộ key

        private readonly Supabase.Client _client;
        private RealtimeChannel _channel;
        private RealtimeBroadcast<GenericBroadcast> _broadcast;

        public event Action<string, object> OnMessageReceived;

        public RealtimeService()
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };
            _client = new Supabase.Client(SupabaseUrl, SupabaseKey, options);
        }

        public async Task ConnectAsync()
        {
            Console.WriteLine("[Realtime] Connecting...");
            await _client.InitializeAsync();

            // Đảm bảo Socket kết nối
            if (!_client.Realtime.Socket.IsConnected)
            {
                await _client.Realtime.ConnectAsync();
            }
            Console.WriteLine("[Realtime] Connected.");
        }

        public async Task JoinRoomAsync(string roomId)
        {
            LeaveRoom();
            Console.WriteLine($"[Realtime] Joining room: {roomId}");

            _channel = _client.Realtime.Channel($"room:{roomId}");

            // Đăng ký Broadcast với GenericBroadcast
            _broadcast = _channel.Register<GenericBroadcast>(true, true);

            _broadcast.AddBroadcastEventHandler((sender, args) =>
            {
                try
                {
                    var state = _broadcast.Current();

                    // Kiểm tra và xử lý dữ liệu nhận được
                    if (state?.Payload != null)
                    {
                        Console.WriteLine($"[Realtime] Event: {state.Event}");

                        // Lấy Dictionary từ ExtensionData
                        var rawPayload = state.Payload.Payload;

                        if (rawPayload != null && rawPayload.Count > 0)
                        {
                            // Chuyển về JObject để dễ thao tác
                            var jsonObject = JObject.FromObject(rawPayload);

                            // Log để debug
                            Console.WriteLine($"[Realtime] Data: {jsonObject}");

                            OnMessageReceived?.Invoke(state.Event, jsonObject);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Realtime] Error receive: {ex.Message}");
                }
            });

            try
            {
                await _channel.Subscribe();
                Console.WriteLine("[Realtime] Subscribed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Realtime] Subscribe Error: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(string eventName, object data)
        {
            if (_broadcast == null)
            {
                Console.WriteLine("[Realtime] Not in room.");
                return;
            }

            try
            {
                // [FIX LỖI QUAN TRỌNG]
                // Phải đóng gói data vào GenericMessage trước khi gửi
                // Thư viện mong đợi kiểu T (GenericMessage), nếu truyền object lạ nó sẽ không serialize đúng.

                var message = new GenericMessage();

                // Chuyển object data sang Dictionary để gán vào Payload (ExtensionData)
                var jsonString = JsonConvert.SerializeObject(data);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);

                message.Payload = dict;

                // Gửi message đi
                await _broadcast.Send(eventName, message);
                Console.WriteLine($"[Realtime] Sent: {eventName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Realtime] Send Error: {ex.Message}");
            }
        }

        public void LeaveRoom()
        {
            if (_channel != null)
            {
                try { _channel.Unsubscribe(); } catch { }
                _channel = null;
                _broadcast = null;
            }
        }
    }
}