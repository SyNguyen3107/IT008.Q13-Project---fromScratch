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

namespace EasyFlips
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
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

        private void ConfigureServices(IServiceCollection services)
        {
            string dbPath;
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

#if DEBUG
            string projectRoot = Path.GetFullPath(Path.Combine(baseDirectory, "../../../"));
            dbPath = Path.Combine(projectRoot, "EasyFlipsAppDB.db");
#else
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "EasyFlips");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            dbPath = Path.Combine(appFolder, "EasyFlipsAppDB.db");
#endif
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}");
            });

            // --- ĐĂNG KÝ SUPABASE CLIENT (SINGLETON) ---
            services.AddSingleton<Supabase.Client>(provider =>
            {
                var options = new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true,
                    // [Gợi ý cho Lỗi 2]: Đảm bảo SessionPersistanceEnabled mặc định là true
                };
                var client = new Supabase.Client(AppConfig.SupabaseUrl, AppConfig.SupabaseKey, options);
                return client;
            });

            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<ICardRepository, CardRepository>();

            services.AddScoped<StudyService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<ExportService>();
            services.AddTransient<ImportService>();
            services.AddSingleton<AudioService>();
            services.AddSingleton<SupabaseService>();
            services.AddSingleton<IAuthService, SupabaseAuthService>();
            services.AddTransient<SyncService>();

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

            services.AddTransient<TestRealtimeWindow>();

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<UserSession>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Kích hoạt NetworkService để bắt đầu theo dõi mạng
            NetworkService.Instance.Initialize();
            // [BƯỚC 1]: Khởi tạo Supabase
            var supabaseService = ServiceProvider.GetRequiredService<SupabaseService>();
            await supabaseService.InitializeAsync();

            // [BƯỚC 2]: Khôi phục session thông qua Interface
            var authService = ServiceProvider.GetRequiredService<IAuthService>();
            bool isLoggedIn = authService.RestoreSession(); // Gọi trực tiếp từ Interface

            // [BƯỚC 3]: Điều hướng
            if (isLoggedIn)
            {
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();
                mainWindow.Show();
            }
            else
            {
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
        }
    }
}