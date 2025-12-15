using EasyFlips.Interfaces;
using EasyFlips.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;
using System.Collections.Generic;

namespace EasyFlips.Repositories
{
    public class ClassroomRepository : IClassroomRepository
    {
        private readonly Supabase.Client _client;

        public ClassroomRepository(Supabase.Client client)
        {
            _client = client;
        }

        public async Task<Classroom> CreateClassroomAsync(Classroom room)
        {
            var response = await _client.From<Classroom>().Insert(room);
            return response.Models.FirstOrDefault();
        }

        public async Task<Classroom> GetClassroomByCodeAsync(string code)
        {
            try
            {
                var response = await _client.From<Classroom>()
                                            .Select("*")
                                            .Match(new Dictionary<string, string>
                                            {
                                                { "room_code", code },
                                                { "is_active", "true" }
                                            })
                                            .Get();

                return response.Models.FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task UpdateStatusAsync(string code, string status)
        {
            await _client.From<Classroom>()
                         .Filter(x => x.RoomCode, Operator.Equals, code)
                         .Set(x => x.Status, status)
                         .Update();
        }

        public async Task DeleteClassroomAsync(string code)
        {
            await _client.From<Classroom>()
                         .Filter(x => x.RoomCode, Operator.Equals, code)
                         .Delete();
        }

        public async Task<List<Member>> GetMembersAsync(string roomId)
        {
            try
            {
                var response = await _client.From<Member>()
                                            .Select("*")
                                            .Filter("classroom_id", Supabase.Postgrest.Constants.Operator.Equals, roomId)
                                            .Get();

                if (response.Models == null) return new List<Member>();
                return response.Models;
            }
            catch (Exception ex)
            {
                throw new Exception($"GetMembers Failed: {ex.Message}");
            }
        }

        public async Task AddMemberAsync(string classId, string userId)
        {
            try
            {
                var member = new Member
                {
                    ClassroomId = classId,
                    UserId = userId,
                    Role = "member",
                    JoinedAt = DateTime.UtcNow
                };
                await _client.From<Member>().Insert(member);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
                {
                    return;
                }
                throw new Exception($"AddMember Failed: {ex.Message}");
            }
        }

        public async Task RemoveMemberAsync(string classId, string userId)
        {
            try
            {
                await _client.From<Member>()
                             .Match(new Dictionary<string, string>
                             {
                                 { "classroom_id", classId },
                                 { "user_id", userId }
                             })
                             .Delete();
            }
            catch { }
        }

        public async Task<Classroom> UpdateClassroomSettingsAsync(string classroomId, string? deckId, int maxPlayers, int timePerRound, int waitTimeSeconds)
        {
            try
            {
                var response = await _client.From<Classroom>()
                                            .Filter(x => x.Id, Operator.Equals, classroomId)
                                            .Set(x => x.DeckId, deckId)
                                            .Set(x => x.MaxPlayers, maxPlayers)
                                            .Set(x => x.TimePerRound, timePerRound)
                                            .Set(x => x.WaitTime, waitTimeSeconds)
                                            .Set(x => x.UpdatedAt, DateTime.UtcNow)
                                            .Update();

                return response.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception($"UpdateClassroomSettings Failed: {ex.Message}");
            }
        }
    }
}