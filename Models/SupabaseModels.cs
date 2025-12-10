using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace EasyFlips.Models
{
    /// <summary>
    /// Profile model - Thông tin người dùng
    /// Tương ứng với bảng public.profiles trong Supabase
    /// </summary>
    [Table("profiles")]
    public class Profile : BaseModel
    {
        // [ĐÃ SỬA]: Chuyển từ Guid sang string để đồng bộ với Deck/Card local
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
    /// Tương ứng với bảng public.classrooms trong Supabase
    /// </summary>
    [Table("classrooms")]
    public class Classroom : BaseModel
    {
        // [ĐÃ SỬA]: Chuyển từ Guid sang string
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("room_code")]
        public string RoomCode { get; set; } = string.Empty;

        // [ĐÃ SỬA]: Chuyển từ Guid sang string
        [Column("owner_id")]
        public string OwnerId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Member model - Thành viên trong lớp học
    /// Tương ứng với bảng public.members trong Supabase
    /// </summary>
    [Table("members")]
    public class Member : BaseModel
    {
        // [ĐÃ SỬA]: Chuyển từ Guid sang string
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        // [ĐÃ SỬA]: Chuyển từ Guid sang string
        [Column("classroom_id")]
        public string ClassroomId { get; set; }

        // [ĐÃ SỬA]: Chuyển từ Guid sang string
        [Column("user_id")]
        public string UserId { get; set; }

        [Column("role")]
        public string Role { get; set; } = "member";

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }
    }

    /// <summary>
    /// DTO cho kết quả join classroom by code
    /// </summary>
    public class JoinClassroomResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        // [ĐÃ SỬA]: Chuyển từ Guid? sang string?
        public string? ClassroomId { get; set; }
    }

    /// <summary>
    /// DTO cho danh sách classroom của user
    /// </summary>
    public class UserClassroom
    {
        // [ĐÃ SỬA]: Chuyển từ Guid sang string
        public string ClassroomId { get; set; }
        public string ClassroomName { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        // [ĐÃ SỬA]: Chuyển từ Guid sang string
        public string OwnerId { get; set; }

        public long MemberCount { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}