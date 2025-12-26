using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Services; 
using Microsoft.Win32;    // Dùng cho OpenFileDialog của WPF
using System.Windows;
using System.Diagnostics;

namespace EasyFlips.ViewModels
{
    public record UserUpdatedMessage(string NewName, string NewAvatar);

    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly UserSession _userSession;
        private readonly SupabaseService _supabaseService;
        // Hành động để báo cho View biết là cần đóng cửa sổ (dùng cho nút Cancel)
        public Action CloseAction { get; set; }

        [ObservableProperty]
        private bool _canSave;
        [ObservableProperty] private string userName;
        [ObservableProperty] private string email;
        [ObservableProperty] private string avatarURL;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;
        public bool IsNotBusy => !IsBusy;

        // Biến này để lưu đường dẫn file ảnh trên máy tính (để upload sau này)
        private string _selectedLocalImagePath;
        // Constructor: Nhận vào UserSession và SupabaseService để lấy dữ liệu hiện tại
        public EditProfileViewModel(UserSession userSession, SupabaseService supabaseService)
        {
            _userSession = userSession;
            _supabaseService = supabaseService;

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
            else
            {
                // Nếu đang test giao diện mà chưa có UserSession thật
                // Hãy gán dữ liệu giả để không bị trống
                Email = "demo@easyflips.com";
            }
            _selectedLocalImagePath = null; // Reset ảnh chọn tạm
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
                CanSave = true;
                // 2. Hiển thị ngay lên giao diện (Preview)
                // WPF tự động hiển thị ảnh từ đường dẫn file cục bộ
                AvatarURL = _selectedLocalImagePath;
            }
        }

        [RelayCommand]
        public async Task SaveProfileAsync()
        {
            Debug.WriteLine($"[EditProfile] SaveProfileAsync called. IsBusy={IsBusy}");
            
            if (IsBusy) return; // Chặn bấm liên tục
            IsBusy = true;

            try
            {
                Debug.WriteLine($"[EditProfile] Starting save. UserId={_userSession?.UserId}, UserName={UserName}");
                
                string finalAvatarUrl = AvatarURL;

                // 1. Nếu có chọn ảnh mới từ máy tính -> Upload lên Supabase
                if (!string.IsNullOrEmpty(_selectedLocalImagePath) && System.IO.File.Exists(_selectedLocalImagePath))
                {
                    Debug.WriteLine($"[EditProfile] Uploading avatar from: {_selectedLocalImagePath}");
                    
                    // Upload và giữ lại avatar cũ làm backup
                    var uploadedUrl = await _supabaseService.ReplaceAvatarWithBackupAsync(_userSession.UserId, _selectedLocalImagePath);
                    
                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        finalAvatarUrl = uploadedUrl;
                        Debug.WriteLine($"[EditProfile] Avatar uploaded successfully: {uploadedUrl}");
                    }
                    else
                    {
                        Debug.WriteLine("[EditProfile] Avatar upload failed");
                        MessageBox.Show("Không thể upload ảnh đại diện. Vui lòng thử lại.", "Lỗi");
                        return;
                    }
                }
                else
                {
                    Debug.WriteLine($"[EditProfile] No new avatar selected. _selectedLocalImagePath={_selectedLocalImagePath}");
                }

                //2.Cập nhật thông tin profile lên Supabase(display_name)
                if (!string.IsNullOrEmpty(UserName))
                {
                    Debug.WriteLine($"[EditProfile] Updating profile: UserName={UserName}, AvatarUrl={finalAvatarUrl}");
                    await _supabaseService.UpdateProfileAsync(_userSession.UserId, UserName, finalAvatarUrl);
                }

                // 3. Cập nhật lại Session cục bộ để hiển thị ngay
                if (_userSession != null)
                {
                    _userSession.UpdateUserInfo(UserName, finalAvatarUrl);
                }
                else
                {
                    Debug.WriteLine("[EditProfile] _userSession is null, cannot update local session.");
                }


                Debug.WriteLine("[EditProfile] Save completed successfully!");
                MessageBox.Show("Đã lưu thành công!", "Thông báo");

                _selectedLocalImagePath = null;
                CanSave = false;
                // Đóng cửa sổ sau khi lưu
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EditProfile] Save error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message, "Lỗi");
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine($"[EditProfile] IsBusy reset to false");
            }
        }


        [RelayCommand]
        public void Cancel()
        {
            LoadUserData();

            // 2. Gọi hành động đóng cửa sổ (Code-behind sẽ xử lý việc này)
            CloseAction?.Invoke();
        }
    }
}