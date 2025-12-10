using EasyFlips.Interfaces;
using EasyFlips.Models;
using Supabase.Gotrue;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace EasyFlips.Services
{
    public class SupabaseAuthService : IAuthService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly UserSession _userSession;

        // Inject Supabase.Client trực tiếp
        // Client này đã được khởi tạo Singleton ở App.xaml.cs
        public SupabaseAuthService(Supabase.Client supabaseClient, UserSession userSession)
        {
            _supabaseClient = supabaseClient;
            _userSession = userSession;
        }

        public string CurrentUserId => _supabaseClient.Auth.CurrentUser?.Id;
        public bool IsLoggedIn => _supabaseClient.Auth.CurrentUser != null;

        public async Task<string> LoginAsync(string email, string password)
        {
            try
            {
                var session = await _supabaseClient.Auth.SignIn(email, password);

                if (session?.User == null) throw new Exception("Login failed. No user returned.");

                string userId = session.User.Id;
                string accessToken = session.AccessToken;
                string userEmail = session.User.Email;

                _userSession.SetUser(userId, userEmail, accessToken);

                return userId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Login Faliure: {ex.Message}");
            }
        }

        public async Task<string> RegisterAsync(string email, string password, string username)
        {
            try
            {
                var signUpOptions = new SignUpOptions
                {
                    Data = new Dictionary<string, object>
                    {
                        { "full_name", username }
                    }
                };

                var session = await _supabaseClient.Auth.SignUp(email, password, signUpOptions);

                if (session?.User == null)
                {
                    if (session == null) throw new Exception("Register failed.");
                    return "CHECK_EMAIL";
                }

                string userId = session.User.Id;
                string accessToken = session.AccessToken;
                string userEmail = session.User.Email;

                _userSession.SetUser(userId, userEmail, accessToken);

                return userId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Đăng ký thất bại: {ex.Message}");
            }
        }

        public void Logout()
        {
            try
            {
                _supabaseClient.Auth.SignOut();
            }
            catch { }

            _userSession.Clear();
            EasyFlips.Properties.Settings.Default.UserId = string.Empty;
            EasyFlips.Properties.Settings.Default.UserToken = string.Empty;
            EasyFlips.Properties.Settings.Default.UserEmail = string.Empty;
            EasyFlips.Properties.Settings.Default.Save();
        }
    }
}