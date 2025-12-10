using EasyFlips.Interfaces;
using EasyFlips.Properties;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows; // [DEBUG]: Cần cho MessageBox

namespace EasyFlips.Services
{
    public class SupabaseAuthService : IAuthService
    {
        private readonly SupabaseService _supabaseService;

        public string CurrentUserId { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUserId);

        public SupabaseAuthService(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                var session = await _supabaseService.Client.Auth.SignIn(email, password);
                if (session?.User != null)
                {
                    CurrentUserId = session.User.Id;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public async Task<bool> RegisterAsync(string email, string password, string username)
        {
            try
            {
                var options = new Supabase.Gotrue.SignUpOptions
                {
                    Data = new Dictionary<string, object> { { "display_name", username } }
                };
                var session = await _supabaseService.Client.Auth.SignUp(email, password, options);
                if (session?.User != null)
                {
                    CurrentUserId = session.User.Id;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public async Task LogoutAsync()
        {
            Settings.Default.UserId = "";
            Settings.Default.UserToken = "";
            Settings.Default.Save();

            await _supabaseService.Client.Auth.SignOut();
            CurrentUserId = null;
        }

        public bool RestoreSession()
        {
            if (_supabaseService.Client == null) return false;

            // 1. Kiểm tra Settings (Remember Me có được tick lần trước không?)
            if (string.IsNullOrEmpty(Settings.Default.UserId))
            {
                // Xóa session rác nếu có để đồng bộ
                if (_supabaseService.Client.Auth.CurrentSession != null)
                {
                    _ = _supabaseService.Client.Auth.SignOut();
                }
                return false;
            }

            // 2. Kiểm tra Supabase Client đã load được session chưa
            var session = _supabaseService.Client.Auth.CurrentSession;

            if (session == null)
            {
                return false;
            }

            if (session.User == null)
            {
                //MessageBox.Show("[DEBUG RESTORE] Session tồn tại nhưng User là NULL.");
                return false;
            }

            // 3. So khớp ID
            // Sử dụng string.Equals để so sánh an toàn hơn (bỏ qua hoa thường)
            if (string.Equals(session.User.Id, Settings.Default.UserId, System.StringComparison.OrdinalIgnoreCase))
            {
                CurrentUserId = session.User.Id;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}