using EasyFlips.Interfaces;
using EasyFlips.Properties;
using Firebase.Auth;
using Firebase.Auth.Providers;
using System;
using System.Threading.Tasks;

namespace EasyFlips.Services
{
    public class FirebaseAuthService : IAuthService
    {
        private const string ApiKey = "AIzaSyDUxy-8er5pLq8LgMaDbF6pvSJII4WChGM"; // API Key của bạn
        private readonly FirebaseAuthClient _authClient;
        private readonly UserSession _userSession;

        public FirebaseAuthService(UserSession userSession)
        {
            _userSession = userSession;
            var config = new FirebaseAuthConfig
            {
                ApiKey = ApiKey,
                AuthDomain = "easyflips.firebaseapp.com",
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);
        }

        public string CurrentUserId => _userSession.UserId;
        public bool IsLoggedIn => _userSession.IsLoggedIn;

        public async Task<string> LoginAsync(string email, string password)
        {
            try
            {
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(email, password);

                // Lưu session
                string userId = userCredential.User.Uid;
                string token = await userCredential.User.GetIdTokenAsync();
                string userEmail = userCredential.User.Info.Email;

                // Lấy DisplayName và PhotoUrl để hiển thị
                string displayName = userCredential.User.Info.DisplayName;
                string photoUrl = userCredential.User.Info.PhotoUrl;

                _userSession.SetUser(userId, userEmail, token, displayName, photoUrl);

                return userId;
            }
            catch (FirebaseAuthException ex)
            {
                // [QUAN TRỌNG] Lấy lý do lỗi cụ thể từ Firebase (VD: INVALID_PASSWORD)
                throw new Exception($"Lỗi Firebase ({ex.Reason}): {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi hệ thống: {ex.Message}", ex);
            }
        }
        public async Task<string> RegisterAsync(string email, string password, string username, string photoUrl = "")
        {
            try
            {
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password);
                var user = userCredential.User;

                // Cập nhật DisplayName (UserName) cho user vừa tạo
                if (!string.IsNullOrEmpty(username))
                {
                    await user.ChangeDisplayNameAsync(username);
                }
                /* // Cập nhật Ảnh (NẾU CÓ) => đang lỗi tạm chưa fix
                if (!string.IsNullOrEmpty(photoUrl))
                {
                    // Hàm này nhận vào một đường link ảnh (URL string)
                    await user.ChangePhotoUrlAsync(photoUrl);
                } */

                // 3. Lưu session
                string userId = user.Uid;
                string token = await user.GetIdTokenAsync();
                string userEmail = user.Info.Email;

                // Lưu username vào session luôn để hiển thị ngay lập tức
                _userSession.SetUser(userId, userEmail, token, username, "");

                return userId;
                /* string userId = userCredential.User.Uid;
                string token = await userCredential.User.GetIdTokenAsync();
                string userEmail = userCredential.User.Info.Email;
                _userSession.SetUser(userId, userEmail, token);

                return userId; */
            }
            catch (FirebaseAuthException ex)
            {
                // [QUAN TRỌNG] Bắt lỗi như Email đã tồn tại (EMAIL_EXISTS)
                throw new Exception($"Lỗi Firebase ({ex.Reason}): {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi hệ thống: {ex.Message}", ex);
            }
        }

        public void Logout()
        {
            // BƯỚC 1: Cố gắng đăng xuất khỏi Firebase Client
            // Chúng ta dùng try-catch để nếu thư viện có lỗi nội bộ thì app vẫn không bị sập
            try
            {
                if (_authClient.User != null)
                {
                    _authClient.SignOut();
                }
            }
            catch (Exception)
            {
                // Nếu lỗi xảy ra (ví dụ User đã null sẵn), ta cứ lờ đi.
                // Mục đích chính là xóa dữ liệu cục bộ bên dưới.
            }

            // BƯỚC 2: Xóa Session trong RAM (Quan trọng nhất với App của ta)
            if (_userSession != null)
            {
                _userSession.Clear();
            }

            // BƯỚC 3: Xóa dữ liệu "Remember Me" trong ổ cứng
            Settings.Default.UserId = string.Empty;
            Settings.Default.UserToken = string.Empty;
            Settings.Default.UserEmail = string.Empty;
            Settings.Default.Save();
        }

        public Task<string> RegisterAsync(string email, string password, string username)
        {
            throw new NotImplementedException();
        }
    }
}