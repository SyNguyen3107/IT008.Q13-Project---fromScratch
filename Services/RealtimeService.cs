using Supabase.Realtime;
using Supabase.Realtime.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace EasyFlips.Services
{
    // Định nghĩa kiểu dữ liệu nhận về là Dictionary
    public class DictionaryBroadcast : BaseBroadcast<Dictionary<string, object>> { }

    public class RealtimeService
    {
        private Supabase.Client _client;
        private RealtimeChannel _channel;
        private RealtimeBroadcast<DictionaryBroadcast> _broadcast;

        // Sự kiện trùng khớp với ViewModel
        public event Action<string, JObject> OnMessageReceived;

        public void SetClient(Supabase.Client client) => _client = client;

        public async Task ConnectAsync()
        {
            if (_client?.Realtime?.Socket == null) return;
            try
            {
                if (!_client.Realtime.Socket.IsConnected)
                {
                    await _client.Realtime.ConnectAsync();
                    await Task.Delay(500);
                }
            }
            catch { }
        }

        public async Task JoinRoomAsync(string roomId)
        {
            if (_client == null) return;

            await LeaveRoom();

            try
            {
                _channel = _client.Realtime.Channel($"room:{roomId}");

                _broadcast = _channel.Register<DictionaryBroadcast>(true, false);

                _broadcast.AddBroadcastEventHandler((sender, args) =>
                {
                    if (args.Event == "SYNC")
                    {
                        try
                        {
                            var dict = args.Payload;

                            if (dict != null)
                            {
                                var json = JObject.FromObject(dict);
                                string action = json["action"]?.ToString();

                                JObject payloadData = new JObject();
                                if (json["payload"] != null)
                                {
                                    if (json["payload"] is JObject jObj)
                                        payloadData = jObj;
                                    else
                                        payloadData = JObject.FromObject(json["payload"]);
                                }

                                if (!string.IsNullOrEmpty(action))
                                {
                                    Debug.WriteLine($"[IN] {action}");
                                    OnMessageReceived?.Invoke(action, payloadData);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Parse Error] {ex.Message}");
                        }
                    }
                });

                await _channel.Subscribe();
                Debug.WriteLine($"[REALTIME] Joined Room: {roomId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REALTIME ERROR] Join: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(string eventName, object data)
        {
            if (_broadcast == null) return;
            try
            {
                var wrapper = new Dictionary<string, object>
                {
                    { "action", eventName },
                    { "payload", data }
                };

                await _broadcast.Send("SYNC", wrapper);
                Debug.WriteLine($"[OUT] {eventName}");
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
                finally { _channel = null; _broadcast = null; }
            }
            await Task.CompletedTask;
        }
    }
}