using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace EasyFlips.Models
{
    /// <summary>
    /// Profile model - Thông tin người dùng
    /// Tương ứng với bảng public.profiles
    /// </summary>
    [Table("profiles")]
    public class Profile : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("display_name")]
        public string? DisplayName { get; set; }

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Classroom model - Thông tin lớp học
    /// Tương ứng với bảng public.classrooms
    /// </summary>
    [Table("classrooms")]
    public class Classroom : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "New classroom";

        [Column("description")]
        public string? Description { get; set; }

        [Column("room_code")]
        public string RoomCode { get; set; } = string.Empty;

        [Column("owner_id")]
        public string HostId { get; set; }
        [Column("max_members")]
        public int MaxPlayers { get; set; } = 30;

        // cột Status (WAITING, PLAYING, CLOSED)
        [Column("status")]
        public string Status { get; set; } = "WAITING";

        [Column("time_per_round")]
        public int TimePerRound { get; set; } = 30;

        // Thời gian chờ trước khi bắt đầu game (giây), mặc định 300 giây = 5 phút
        [Column("wait_time")]
        public int WaitTime { get; set; } = 300;

        // [MỚI]: Thêm cột DeckId để biết phòng đang học bộ bài nào
        [Column("deck_id")]
        public string? DeckId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Member model - Thành viên trong lớp học
    /// Tương ứng với bảng public.members
    /// </summary>
    [Table("members")]
    public class Member : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("classroom_id")]
        public string ClassroomId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("role")]
        public string Role { get; set; } = "member";

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }

        // [MỚI] Thêm thuộc tính này để hứng dữ liệu từ bảng Profile khi Join
        [Reference(typeof(Profile))]
        public Profile Profile { get; set; }
    }

    /// <summary>
    /// DTO: Kết quả trả về từ RPC join_classroom_by_code
    /// </summary>
    public class JoinClassroomResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("classroom_id")]
        public string? ClassroomId { get; set; }
    }

    /// <summary>
    /// DTO: Kết quả trả về từ RPC get_user_classrooms
    /// </summary>
    public class UserClassroom
    {
        [JsonProperty("classroom_id")]
        public string ClassroomId { get; set; }

        [JsonProperty("classroom_name")]
        public string ClassroomName { get; set; }

        [JsonProperty("room_code")]
        public string RoomCode { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("owner_id")]
        public string OwnerId { get; set; }

        [JsonProperty("member_count")]
        public long MemberCount { get; set; }

        [JsonProperty("joined_at")]
        public DateTime JoinedAt { get; set; }
    }

    /// <summary>
    /// DTO: Thông tin thành viên kèm profile (tên, avatar) để hiển thị trên UI
    /// </summary>
    public class MemberWithProfile
    {
        public string MemberId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ClassroomId { get; set; } = string.Empty;
        public string Role { get; set; } = "member";
        public DateTime JoinedAt { get; set; }

        // Thông tin từ bảng Profile
        public string DisplayName { get; set; } = "Unknown";
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        // Thuộc tính hỗ trợ cho UI
        public bool IsHost => Role.Equals("host", StringComparison.OrdinalIgnoreCase);
        public string RoleDisplay => IsHost ? "👑 Host" : "👤 Thành viên";
    }
}