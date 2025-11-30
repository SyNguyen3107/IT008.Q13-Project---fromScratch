using EasyFlips.Interfaces;
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
                _userSession.SetUser(userId, userEmail, token);

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

        public async Task<string> RegisterAsync(string email, string password)
        {
            try
            {
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password);

                string userId = userCredential.User.Uid;
                string token = await userCredential.User.GetIdTokenAsync();
                string userEmail = userCredential.User.Info.Email;
                _userSession.SetUser(userId, userEmail, token);

                return userId;
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
            _authClient.SignOut();
            _userSession.Clear();
        }
    }
}