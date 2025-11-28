using EasyFlips.Interfaces;
using Firebase.Auth;
using Firebase.Auth.Providers; // QUAN TRỌNG: Cần thêm cái này cho bản 4.x
using System;
using System.Threading.Tasks;

namespace EasyFlips.Services
{
    public class FirebaseAuthService : IAuthService
    {
        // --- Dán API Key của bạn vào đây ---
        private const string ApiKey = "AIzaSyDUxy-8er5pLq8LgMaDbF6pvSJII4WChGM";

        private readonly FirebaseAuthClient _authClient;// Cập nhật cho bản 4.x

        public string CurrentUserId { get; private set; }

        // Trong bản mới, User nằm trong _authClient.User
        public bool IsLoggedIn => _authClient.User != null;

        public FirebaseAuthService()
        {
            // Cấu hình cho bản 4.x
            var config = new FirebaseAuthConfig
            {
                ApiKey = ApiKey,
                AuthDomain = "easyflips.firebaseapp.com", // (Tùy chọn) Thay bằng domain project của bạn
                Providers = new FirebaseAuthProvider[]
                {
                    // Cần khai báo provider Email/Password
                    new EmailProvider()
                }
            };

            _authClient = new FirebaseAuthClient(config);
        }

        public async Task<string> LoginAsync(string email, string password)
        {
            try
            {
                // Cú pháp mới: SignInWithEmailAndPasswordAsync
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(email, password);

                CurrentUserId = userCredential.User.Uid; // Lấy UID
                return CurrentUserId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Đăng nhập thất bại: {ex.Message}");
            }
        }

        public async Task<string> RegisterAsync(string email, string password)
        {
            try
            {
                // Cú pháp mới: CreateUserWithEmailAndPasswordAsync
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password);

                CurrentUserId = userCredential.User.Uid;
                return CurrentUserId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Đăng ký thất bại: {ex.Message}");
            }
        }

        public void Logout()
        {
            _authClient.SignOut();
            CurrentUserId = null;
        }
    }
}