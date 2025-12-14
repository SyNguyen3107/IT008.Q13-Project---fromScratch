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

                // Lấy thông tin profile từ bảng profiles (ưu tiên hơn UserMetadata)
                var profile = await _supabaseService.GetProfileAsync(session.User.Id);
                
                string displayName;
                string avatarUrl;

                if (profile != null)
                {
                    // Ưu tiên dùng dữ liệu từ bảng profiles
                    displayName = profile.DisplayName ?? "";
                    avatarUrl = profile.AvatarUrl ?? "";
                }
                else
                {
                    // Fallback về UserMetadata nếu chưa có profile
                    displayName = session.User.UserMetadata?.ContainsKey("display_name") == true
                        ? session.User.UserMetadata["display_name"]?.ToString() ?? ""
                        : "";
                    avatarUrl = session.User.UserMetadata?.ContainsKey("avatar_url") == true
                        ? session.User.UserMetadata["avatar_url"]?.ToString() ?? ""
                        : "";
                }

                // Cập nhật thông tin vào bộ nhớ phiên làm việc (RAM)
                _userSession.SetUser(
                    session.User.Id,
                    session.User.Email ?? email,
                    session.AccessToken ?? "",
                    session.RefreshToken ?? "",
                    displayName,
                    avatarUrl
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
            // 1. Nếu Client đã tự động nhận diện được Session (do tính năng AutoSave của thư viện) -> OK
            if (_supabaseService.Client.Auth.CurrentSession != null &&
                !string.IsNullOrEmpty(_supabaseService.Client.Auth.CurrentSession.AccessToken))
            {
                // Đồng bộ lại thông tin vào UserSession (RAM)
                var user = _supabaseService.Client.Auth.CurrentUser;
                if (user != null)
                {
                    CurrentUserId = user.Id;
                    _userSession.SetUser(user.Id, user.Email, _supabaseService.Client.Auth.CurrentSession.AccessToken, "", "", "");
                    return true;
                }
            }

            // 2. [QUAN TRỌNG] Nếu Client quên, nhưng Settings (Local Storage) vẫn còn lưu
            var savedAccessToken = Settings.Default.UserToken;
            var savedRefreshToken = Settings.Default.RefreshToken;

            if (!string.IsNullOrEmpty(savedAccessToken))
            {
                try
                {
                    // [FIX]: Ép buộc Supabase Client sử dụng Token cũ
                    _supabaseService.Client.Auth.SetSession(savedAccessToken, savedRefreshToken);

                    // Cập nhật lại UserSession (RAM)
                    CurrentUserId = Settings.Default.UserId;
                    _userSession.SetUser(
                        Settings.Default.UserId,
                        Settings.Default.UserEmail,
                        savedAccessToken,
                        savedRefreshToken,
                        "", "" // Tạm thời bỏ qua display name/avatar nếu chưa fetch
                    );

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi khôi phục Session: {ex.Message}");
                    // Token lỗi hoặc hết hạn -> Xóa sạch để bắt đăng nhập lại
                    LogoutAsync().Wait();
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Tải thông tin profile (display_name, avatar_url) từ bảng profiles sau khi RestoreSession
        /// </summary>
        public async Task LoadProfileInfoAsync()
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return;

            try
            {
                var profile = await _supabaseService.GetProfileAsync(CurrentUserId);
                if (profile != null)
                {
                    _userSession.UpdateUserInfo(profile.DisplayName ?? "", profile.AvatarUrl ?? "");
                    System.Diagnostics.Debug.WriteLine($"[AuthService] Profile loaded: {profile.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] Load profile error: {ex.Message}");
            }
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