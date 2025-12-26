using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Converters;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Repositories;
using EasyFlips.Services;
using EasyFlips.ViewModels;
using EasyFlips.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using EasyFlips.Properties;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Newtonsoft.Json;

namespace EasyFlips
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public static string ProfileId { get; private set; } = "default";

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Parse tham số dòng lệnh trước tiên
            ParseCommandLineArgs(e.Args);

            // [FIX] KHÔNG gán MainWindow.Title ở đây vì MainWindow chưa được khởi tạo -> Gây Crash.
            // Việc gán Title sẽ thực hiện trong InitializeApp sau khi tạo Window.

            // 2. Cấu hình dịch vụ
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // 3. Khởi chạy ứng dụng
            InitializeApp();
        }

        private void ParseCommandLineArgs(string[] args)
        {
            // Parse: EasyFlips.exe --profile=1
            foreach (var arg in args)
            {
                if (arg.StartsWith("--profile="))
                {
                    ProfileId = arg.Replace("--profile=", "").Trim();
                    break;
                }
            }

            // Validate profile ID
            if (string.IsNullOrEmpty(ProfileId))
            {
                ProfileId = "default";
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            string dbPath = GetProfileDatabasePath();

            // Đăng ký DB Context
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}");
            });

            RegisterServices(services);
        }

        private async void InitializeApp()
        {
            // [BƯỚC 0]: Migrate Database SQLite Local
            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.Migrate();
                }
            }
            catch (Exception)
            {
                // Xử lý lỗi hỏng file DB (như code cũ của bạn)
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dbPath = Path.Combine(appData, "EasyFlips", $"EasyFlipsAppDB_Profile{ProfileId}.db");

                try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }

                MessageBox.Show($"Cơ sở dữ liệu profile '{ProfileId}' bị lỗi và đã được làm mới.\nVui lòng khởi động lại.",
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            // Kích hoạt NetworkService
            NetworkService.Instance.Initialize();

            // [BƯỚC 1]: Khởi tạo Supabase
            var supabaseService = ServiceProvider.GetRequiredService<SupabaseService>();
            await supabaseService.InitializeAsync();

            // [BƯỚC 2]: Khôi phục session
            var authService = ServiceProvider.GetRequiredService<IAuthService>();
            bool isLoggedIn = authService.RestoreSession();

            // [BƯỚC 2.1]: Tải thông tin profile nếu đã đăng nhập
            if (isLoggedIn)
            {
                await authService.LoadProfileInfoAsync();
            }

            // [BƯỚC 3]: Điều hướng và Hiển thị Window
            Window windowToShow;
            if (isLoggedIn)
            {
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();
                windowToShow = mainWindow;
            }
            else
            {
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                windowToShow = loginWindow;
            }

            // [FIX] Gán Title ở đây mới an toàn
            windowToShow.Title = $"EasyFlips - Profile: {ProfileId}";
            windowToShow.Show();
        }

        private void RegisterServices(IServiceCollection services)
        {
            // --- SUPABASE CLIENT VỚI CUSTOM SESSION HANDLER ---
            services.AddSingleton<Supabase.Client>(provider =>
            {
                var options = new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true,
                    // [QUAN TRỌNG] Sử dụng bộ lưu session riêng biệt cho từng Profile
                    SessionHandler = new ProfileSessionPersistence()
                };

                // Giả sử AppConfig đã có
                var client = new Supabase.Client(AppConfig.SupabaseUrl, AppConfig.SupabaseKey, options);
                return client;
            });

            // Repositories
            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<ICardRepository, CardRepository>();
            services.AddScoped<IClassroomRepository, ClassroomRepository>();

            // Services
            services.AddScoped<StudyService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<ExportService>();
            services.AddTransient<ImportService>();
            services.AddSingleton<AudioService>();
            services.AddSingleton<SupabaseService>();
            services.AddSingleton<IAuthService, SupabaseAuthService>();
            services.AddTransient<SyncService>();
            services.AddSingleton<RealtimeService>();
            services.AddSingleton<ComparisonService>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<StudyViewModel>();
            services.AddTransient<CreateDeckViewModel>();
            services.AddTransient<AddCardViewModel>();
            services.AddTransient<DeckChosenViewModel>();
            services.AddTransient<DeckRenameViewModel>();
            services.AddTransient<ChooseDeckViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();
            services.AddTransient<SyncViewModel>();
            services.AddTransient<LobbyViewModel>();
            services.AddTransient<JoinViewModel>();
            services.AddTransient<CreateRoomViewModel>();
            services.AddTransient<OtpViewModel>();
            services.AddTransient<ResetPasswordViewModel>();
            services.AddTransient<HostLobbyViewModel>();
            services.AddTransient<MemberLobbyViewModel>();
            services.AddTransient<HostGameViewModel>();
            services.AddTransient<MemberGameViewModel>();
            services.AddTransient<LeaderBoardViewModel>();
            services.AddTransient<LeaderBoardWindow>(); // hoặc LeaderBoardWindow nếu dùng WPF

            // Windows
            services.AddTransient<MainWindow>();
            services.AddTransient<StudyWindow>();
            services.AddTransient<CreateDeckWindow>();
            services.AddTransient<AddCardWindow>();
            services.AddTransient<DeckChosenWindow>();
            services.AddTransient<DeckRenameWindow>();
            services.AddTransient<ChooseDeckWindow>();
            services.AddTransient<RegisterWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<SyncWindow>();
            services.AddTransient<OtpWindow>();
            services.AddTransient<ResetPasswordWindow>();
            services.AddTransient<LobbyWindow>();
            services.AddTransient<JoinWindow>();
            services.AddTransient<CreateRoomWindow>();
            services.AddTransient<TestRealtimeWindow>();
            services.AddTransient<HostLobbyWindow>();
            services.AddTransient<MemberLobbyWindow>();
            services.AddTransient<HostGameWindow>();
            services.AddTransient<MemberGameWindow>();

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<UserSession>();
        }

        private string GetProfileDatabasePath()
        {
            string fileName = $"EasyFlipsAppDB_{ProfileId}.db";

#if DEBUG
            // Debug: Lưu ngay tại thư mục project để dễ tìm
            string baseDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../"));
            return Path.Combine(baseDirectory, fileName);
#else
            // Release: Lưu trong AppData
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "EasyFlips");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, fileName);
#endif
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Lỗi Crash UI: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Lỗi Nghiêm Trọng");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Lỗi Crash AppDomain: {ex?.Message}\n\n{ex?.StackTrace}", "Lỗi Nghiêm Trọng");
        }
    }

    // [CLASS QUAN TRỌNG] Tách biệt file session cho từng Profile
    public class ProfileSessionPersistence : IGotrueSessionPersistence<Session>
    {
        private readonly string _filePath;

        public ProfileSessionPersistence()
        {
            // Tạo tên file session dựa trên ProfileId (VD: .gotrue_profile1.cache)
            // Lưu file này cùng chỗ với file exe
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            _filePath = Path.Combine(directory, $".gotrue_{App.ProfileId}.cache");
        }

        public void SaveSession(Session session)
        {
            try
            {
                var json = JsonConvert.SerializeObject(session);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public Session LoadSession()
        {
            try
            {
                if (!File.Exists(_filePath)) return null;
                var json = File.ReadAllText(_filePath);
                return JsonConvert.DeserializeObject<Session>(json);
            }
            catch
            {
                return null;
            }
        }

        public void DestroySession()
        {
            try
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
            catch { }
        }
    }
}