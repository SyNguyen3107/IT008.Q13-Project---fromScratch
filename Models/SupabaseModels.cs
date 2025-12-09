using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace EasyFlips.Models
{
    /// <summary>
    /// Profile model - Thông tin ng??i dùng
    /// T??ng ?ng v?i b?ng public.profiles trong Supabase
    /// </summary>
    [Table("profiles")]
    public class Profile : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }
        
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
    /// Classroom model - Thông tin l?p h?c
    /// T??ng ?ng v?i b?ng public.classrooms trong Supabase
    /// </summary>
    [Table("classrooms")]
    public class Classroom : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }
        
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        
        [Column("description")]
        public string? Description { get; set; }
        
        [Column("room_code")]
        public string RoomCode { get; set; } = string.Empty;
        
        [Column("owner_id")]
        public Guid OwnerId { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Member model - Thành viên trong l?p h?c
    /// T??ng ?ng v?i b?ng public.members trong Supabase
    /// </summary>
    [Table("members")]
    public class Member : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }
        
        [Column("classroom_id")]
        public Guid ClassroomId { get; set; }
        
        [Column("user_id")]
        public Guid UserId { get; set; }
        
        [Column("role")]
        public string Role { get; set; } = "member";
        
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }
    }

    /// <summary>
    /// DTO cho k?t qu? join classroom by code
    /// </summary>
    public class JoinClassroomResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? ClassroomId { get; set; }
    }

    /// <summary>
    /// DTO cho danh sách classroom c?a user
    /// </summary>
    public class UserClassroom
    {
        public Guid ClassroomId { get; set; }
        public string ClassroomName { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid OwnerId { get; set; }
        public long MemberCount { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
