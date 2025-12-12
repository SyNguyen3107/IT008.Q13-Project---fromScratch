using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Services;
using Supabase.Gotrue; // Cần thiết cho UserAttributes
using System.IO;       // Cần thiết để đọc file ảnh
using System.Windows;

namespace EasyFlips.ViewModels
{
    public record UserUpdatedMessage(string NewName, string NewAvatar);

    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly UserSession _userSession;

        // Cần Client để gọi lệnh Update lên Server
        private readonly Supabase.Client _supabaseClient;

        public Action CloseAction { get; set; }

        [ObservableProperty] private string userName;
        [ObservableProperty] private string email;
        [ObservableProperty] private string avatarURL;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;
        public bool IsNotBusy => !IsBusy;

        private string _selectedLocalImagePath; // Đường dẫn file trên máy tính (C:\...)

        // SỬA CONSTRUCTOR: Nhận thêm Supabase.Client
        public EditProfileViewModel(UserSession userSession, Supabase.Client client)
        {
            _userSession = userSession;
            _supabaseClient = client;

            LoadUserData();
        }

        private void LoadUserData()
        {
            if (_userSession != null)
            {
                UserName = _userSession.UserName;
                Email = _userSession.Email;
                AvatarURL = _userSession.AvatarURL;
            }
            _selectedLocalImagePath = null;
        }

        [RelayCommand]
        public void ChangeAvatar()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Lưu đường dẫn file local để lát nữa upload
                _selectedLocalImagePath = openFileDialog.FileName;

                // Hiển thị preview ngay
                AvatarURL = _selectedLocalImagePath;
            }
        }

        [RelayCommand]
        public async Task SaveProfile()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // [FIX LỖI NOT LOGGED IN]
                // Kiểm tra: Nếu Supabase Client đang "quên" session
                // ================================================================
                if (_supabaseClient.Auth.CurrentSession == null)
                {
                    // Kiểm tra xem có đủ cả 2 token không
                    if (!string.IsNullOrEmpty(_userSession.Token) && !string.IsNullOrEmpty(_userSession.RefreshToken))
                    {
                        // Truyền đủ 2 tham số vào đây
                        await _supabaseClient.Auth.SetSession(_userSession.Token, _userSession.RefreshToken);
                    }
                    else
                    {
                        throw new Exception("Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.");
                    }
                }
                // --- BƯỚC 1: UPLOAD ẢNH (Nếu có chọn ảnh mới) ---
                if (!string.IsNullOrEmpty(_selectedLocalImagePath))
                {
                    // Đặt tên file duy nhất: ID_Time.png
                    string fileName = $"{_userSession.UserId}_{DateTime.Now.Ticks}.png";

                    // Đọc file từ máy tính
                    byte[] fileBytes = File.ReadAllBytes(_selectedLocalImagePath);

                    // Upload lên Bucket "avatars"
                    await _supabaseClient.Storage
                        .From("avatars")
                        .Upload(fileBytes, fileName);

                    // Lấy đường dẫn Public (Link web)
                    string publicUrl = _supabaseClient.Storage
                        .From("avatars")
                        .GetPublicUrl(fileName);

                    // Gán link web vào AvatarURL để lưu vào DB
                    AvatarURL = publicUrl;
                }

                // --- BƯỚC 2: CẬP NHẬT THÔNG TIN LÊN SUPABASE AUTH ---
                var attrs = new UserAttributes
                {
                    Data = new Dictionary<string, object>
                    {
                        { "full_name", UserName },
                        { "avatar_url", AvatarURL } // Lưu link web, không lưu link ổ cứng C:\
                    }
                };

                // Gọi lệnh Update của Supabase
                await _supabaseClient.Auth.Update(attrs);

                // --- BƯỚC 3: CẬP NHẬT SESSION LOCAL & UI ---
                _userSession.UpdateUserInfo(UserName, AvatarURL);
                WeakReferenceMessenger.Default.Send(new UserUpdatedMessage(UserName, AvatarURL));

                MessageBox.Show($"Đã lưu thành công!", "Thông báo");

                _selectedLocalImagePath = null;
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu: " + ex.Message, "Lỗi");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            LoadUserData();
            CloseAction?.Invoke();
        }
    }
}