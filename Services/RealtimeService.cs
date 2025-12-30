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
    // Giữ nguyên các class bổ trợ cho Supabase
    public class DictionaryBroadcast : BaseBroadcast<Dictionary<string, object>> { }

    public class RealtimeService
    {
        private Supabase.Client _client;
        private RealtimeChannel _channel;
        private RealtimeBroadcast<DictionaryBroadcast> _broadcast;

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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Connect error: {ex.Message}");
            }
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
                                JObject payloadData = json["payload"] as JObject ?? new JObject();

                                if (!string.IsNullOrEmpty(action))
                                {
                                    OnMessageReceived?.Invoke(action, payloadData);
                                }
                            }
                        }
                        catch { }
                    }
                });

                await _channel.Subscribe();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Join error: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Send error: {ex.Message}");
            }
        }

        public async Task LeaveRoom()
        {
            if (_channel != null)
            {
                try
                {
                    _channel.Unsubscribe();
                    if (_client?.Realtime != null)
                        _client.Realtime.Remove(_channel);
                }
                catch { }
                finally { _channel = null; _broadcast = null; }
            }
            await Task.CompletedTask;
        }
    }
}