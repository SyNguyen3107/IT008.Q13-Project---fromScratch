using EasyFlips.Models;
using System.Threading.Tasks;

namespace EasyFlips.Interfaces
{
    public interface IClassroomRepository
    {
        // Tạo phòng mới (Insert vào DB)
        Task<Classroom> CreateClassroomAsync(Classroom room);

        // Tìm kiếm phòng theo Mã (RoomCode)
        Task<Classroom> GetClassroomByCodeAsync(string code);

        // Xóa phòng (Dùng khi Giáo viên đóng phòng)
        Task DeleteClassroomAsync(string code);

        // Cập nhật trạng thái phòng (WAITING -> PLAYING -> CLOSED)
        Task UpdateStatusAsync(string code, string status);
        Task<List<Member>> GetMembersAsync(string roomId); // Lấy danh sách thành viên trong phòng
        Task AddMemberAsync(string classId, string userId);
        Task RemoveMemberAsync(string classId, string userId);
    }
}