using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Services;
using Microsoft.Win32;
using System.Windows;
using System.Diagnostics;

namespace EasyFlips.ViewModels
{
    public record UserUpdatedMessage(string NewName, string NewAvatar);

    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly UserSession _userSession;
        private readonly SupabaseService _supabaseService;
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

        private string _selectedLocalImagePath;
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
                Email = "demo@easyflips.com";
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
                _selectedLocalImagePath = openFileDialog.FileName;
                CanSave = true;
                AvatarURL = _selectedLocalImagePath;
            }
        }

        [RelayCommand]
        public async Task SaveProfileAsync()
        {
            Debug.WriteLine($"[EditProfile] SaveProfileAsync called. IsBusy={IsBusy}");

            if (IsBusy) return; IsBusy = true;

            try
            {
                Debug.WriteLine($"[EditProfile] Starting save. UserId={_userSession?.UserId}, UserName={UserName}");

                string finalAvatarUrl = AvatarURL;

                if (!string.IsNullOrEmpty(_selectedLocalImagePath) && System.IO.File.Exists(_selectedLocalImagePath))
                {
                    Debug.WriteLine($"[EditProfile] Uploading avatar from: {_selectedLocalImagePath}");

                    var uploadedUrl = await _supabaseService.ReplaceAvatarWithBackupAsync(_userSession.UserId, _selectedLocalImagePath);

                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        finalAvatarUrl = uploadedUrl;
                        Debug.WriteLine($"[EditProfile] Avatar uploaded successfully: {uploadedUrl}");
                    }
                    else
                    {
                        Debug.WriteLine("[EditProfile] Avatar upload failed");
                        MessageBox.Show("Failed to upload profile picture. Please try again.", "Error");
                        return;
                    }
                }
                else
                {
                    Debug.WriteLine($"[EditProfile] No new avatar selected. _selectedLocalImagePath={_selectedLocalImagePath}");
                }

                if (!string.IsNullOrEmpty(UserName))
                {
                    Debug.WriteLine($"[EditProfile] Updating profile: UserName={UserName}, AvatarUrl={finalAvatarUrl}");
                    await _supabaseService.UpdateProfileAsync(_userSession.UserId, UserName, finalAvatarUrl);
                }

                if (_userSession != null)
                {
                    _userSession.UpdateUserInfo(UserName, finalAvatarUrl);
                }
                else
                {
                    Debug.WriteLine("[EditProfile] _userSession is null, cannot update local session.");
                }


                Debug.WriteLine("[EditProfile] Save completed successfully!");
                MessageBox.Show("Saved successfully!", "Notification");

                _selectedLocalImagePath = null;
                CanSave = false;
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EditProfile] Save error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show("An error occurred: " + ex.Message, "Error");
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine($"[EditProfile] IsBusy reset to false");
            }
        }
        partial void OnUserNameChanged(string value)
        {
            if (_userSession != null)
            {
                CanSave = value != _userSession.UserName || !string.IsNullOrEmpty(_selectedLocalImagePath);
            }
            else
            {
                CanSave = true;
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