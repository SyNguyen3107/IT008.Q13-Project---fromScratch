using EasyFlips.Interfaces;
using EasyFlips.Properties;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;


namespace EasyFlips.Services
{
    public class SupabaseAuthService : IAuthService
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _userSession;

        public string CurrentUserId { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUserId);

        public SupabaseAuthService(SupabaseService supabaseService, UserSession userSession)
        {
            _supabaseService = supabaseService;
            _userSession = userSession;
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            // Không dùng try-catch ở đây để Exception (nếu có) được bắn thẳng sang ViewModel
            var session = await _supabaseService.Client.Auth.SignIn(email, password);

            if (session?.User != null)
            {
                CurrentUserId = session.User.Id;

                var displayName = session.User.UserMetadata?.ContainsKey("display_name") == true
                    ? session.User.UserMetadata["display_name"]?.ToString()
                    : "";
                var avatarUrl = session.User.UserMetadata?.ContainsKey("avatar_url") == true
                    ? session.User.UserMetadata["avatar_url"]?.ToString()
                    : "";

                // Cập nhật thông tin vào bộ nhớ phiên làm việc (RAM)
                _userSession.SetUser(
                    session.User.Id,
                    session.User.Email ?? email,
                    session.AccessToken ?? "",
                    session.RefreshToken ?? "",
                    displayName ?? "",
                    avatarUrl ?? ""
                );

                return true;
            }

            return false;
        }

        public async Task<bool> RegisterAsync(string email, string password, string username)
        {
            var options = new Supabase.Gotrue.SignUpOptions
            {
                Data = new Dictionary<string, object> { { "display_name", username } }
            };

            var session = await _supabaseService.Client.Auth.SignUp(email, password, options);

            if (session?.User != null)
            {
                CurrentUserId = session.User.Id;

                _userSession.SetUser(
                    session.User.Id,
                    session.User.Email ?? email,
                    session.AccessToken ?? "",
                    username,
                    ""
                );

                return true;
            }
            return false;
        }

        public async Task LogoutAsync()
        {
            // Xóa Settings trước
            Settings.Default.UserId = string.Empty;
            Settings.Default.UserToken = string.Empty;
            Settings.Default.RefreshToken = string.Empty;
            Settings.Default.Save();

            await _supabaseService.Client.Auth.SignOut();

            CurrentUserId = null;
            _userSession.Clear();
        }

        public bool RestoreSession()
        {
            if (_supabaseService.Client == null) return false;

            // 1. Kiểm tra Settings
            if (string.IsNullOrEmpty(Settings.Default.UserId))
            {
                if (_supabaseService.Client.Auth.CurrentSession != null)
                {
                    _ = _supabaseService.Client.Auth.SignOut();
                }
                return false;
            }

            // 2. Kiểm tra Session hiện tại của Client
            var session = _supabaseService.Client.Auth.CurrentSession;
            if (session == null || session.User == null)
            {
                return false;
            }

            // 3. So khớp ID
            if (string.Equals(session.User.Id, Settings.Default.UserId, System.StringComparison.OrdinalIgnoreCase))
            {
                CurrentUserId = session.User.Id;

                var displayName = session.User.UserMetadata?.ContainsKey("display_name") == true
                    ? session.User.UserMetadata["display_name"]?.ToString()
                    : "";
                var avatarUrl = session.User.UserMetadata?.ContainsKey("avatar_url") == true
                    ? session.User.UserMetadata["avatar_url"]?.ToString()
                    : "";

                _userSession.SetUser(
                    session.User.Id,
                    session.User.Email ?? "",
                    session.AccessToken ?? "",
                    session.RefreshToken ?? "",
                    displayName ?? "",
                    avatarUrl ?? ""
                );

                return true;
            }

            return false;
        }
        public async Task<bool> ForgotPasswordAsync(string email)
        {
            try
            {
                await _supabaseService.Client.Auth.ResetPasswordForEmail(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendMagicLinkAsync(string email)
        {
            try
            {
                await _supabaseService.Client.Auth.SignIn(email);
                // Supabase sẽ gửi magic link qua email
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdatePasswordAsync(string newPassword)
        {
            try
            {
                var authClient = _supabaseService.Client.Auth as Supabase.Gotrue.Client;
                if (authClient == null || authClient.CurrentSession == null)
                    return false;

                var user = await authClient.Update(new Supabase.Gotrue.UserAttributes
                {
                    Password = newPassword
                });

                return user != null;
            }
            catch (Exception ex)
            {
                // Log lỗi để debug
                MessageBox.Show($"Update password error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool IsRecoverySession()
        {
            var session = _supabaseService.Client.Auth.CurrentSession;
            return session != null && session.User != null && string.IsNullOrEmpty(CurrentUserId);
        }

        public async Task<bool> SendOtpAsync(string email)
        {
            try
            {
                await _supabaseService.Client.Auth.ResetPasswordForEmail(email);
                return true;
            }
            catch
            {
                return false;
            }
        }




        public async Task<bool> VerifyOtpAsync(string email, string token)
        {
            try
            {
                var session = await _supabaseService.Client.Auth.VerifyOTP(
                    email,
                    token,
                    Supabase.Gotrue.Constants.EmailOtpType.Recovery
                );

                if (session?.User != null)
                {
                    CurrentUserId = session.User.Id;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }



    }
}