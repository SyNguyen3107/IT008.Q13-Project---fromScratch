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
        [JsonExtensionData]
        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
    }

    public class GenericBroadcast : BaseBroadcast<GenericMessage>
    {
    }

    public class RealtimeService
    {
        private readonly string SupabaseUrl = AppConfig.SupabaseUrl;
        private readonly string SupabaseKey = AppConfig.SupabaseKey;

        private readonly Supabase.Client _client;
        private RealtimeChannel _channel;
        private RealtimeBroadcast<GenericBroadcast> _broadcast;

        // Chỉ giữ lại event nhận tin nhắn, bỏ Presence
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
            await _client.InitializeAsync();
            if (!_client.Realtime.Socket.IsConnected)
            {
                await _client.Realtime.ConnectAsync();
            }
        }

        public async Task JoinRoomAsync(string roomId)
        {
            string topic = $"room:{roomId}";

            if (_channel != null)
            {
                if (_channel.Topic == topic && _channel.IsJoined) return;
                await LeaveRoom();
            }

            _channel = _client.Realtime.Channel(topic);

            // Chỉ đăng ký Broadcast (Broadcast = true, Presence = false)
            // Để tránh lỗi biên dịch với thư viện cũ
            _broadcast = _channel.Register<GenericBroadcast>(true, false);

            _broadcast.AddBroadcastEventHandler((sender, args) =>
            {
                Console.WriteLine($"[Realtime RAW] Event received!");
                try
                {
                    var state = _broadcast.Current();
                    if (state?.Payload != null)
                    {
                        Console.WriteLine($"[Realtime Event]: {state.Event}");
                        var rawPayload = state.Payload.Payload;
                        if (rawPayload != null && rawPayload.Count > 0)
                        {
                            var jsonObject = JObject.FromObject(rawPayload);
                            OnMessageReceived?.Invoke(state.Event, jsonObject);
                        }
                    }
                }
                catch { }
            });

            await _channel.Subscribe();
        }

        public async Task SendMessageAsync(string eventName, object data)
        {
            if (_broadcast == null) return;
            try
            {
                var message = new GenericMessage();
                var jsonString = JsonConvert.SerializeObject(data);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                message.Payload = dict;

                await _broadcast.Send(eventName, message);
            }
            catch { }
        }

        public async Task LeaveRoom()
        {
            if (_channel != null)
            {
                try
                {
                    _channel.Unsubscribe();
                    _client.Realtime.Remove(_channel);
                }
                catch { }
                finally
                {
                    _channel = null;
                    _broadcast = null;
                }
            }
        }
    }
}