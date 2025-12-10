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
            // Chỉ tạo 1 lần duy nhất, dùng chung cho cả Auth, Realtime, Database
            services.AddSingleton<Supabase.Client>(provider =>
            {
                var options = new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true
                };
                // Lấy URL và Key từ AppConfig
                var client = new Supabase.Client(AppConfig.SupabaseUrl, AppConfig.SupabaseKey, options);

                return client;
            });

            //Đăng ký Repositories
            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<ICardRepository, CardRepository>();

            //Đăng ký Services
            services.AddScoped<StudyService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<ExportService>();
            services.AddTransient<ImportService>();
            services.AddSingleton<AudioService>();
            services.AddSingleton<SupabaseService>();
            services.AddSingleton<IAuthService, SupabaseAuthService>();

            //Đăng ký ViewModels 
            services.AddTransient<MainViewModel>();
            services.AddTransient<StudyViewModel>();
            services.AddTransient<CreateDeckViewModel>();
            services.AddTransient<AddCardViewModel>();
            services.AddTransient<DeckChosenViewModel>();
            services.AddTransient<DeckRenameViewModel>();
            services.AddTransient<ChooseDeckViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();

            //Đăng ký Views
            services.AddTransient<MainWindow>();
            services.AddTransient<StudyWindow>();
            services.AddTransient<CreateDeckWindow>();
            services.AddTransient<AddCardWindow>();
            services.AddTransient<DeckChosenWindow>();
            services.AddTransient<DeckRenameWindow>();
            services.AddTransient<ChooseDeckWindow>();
            services.AddTransient<SyncWindow>();
            services.AddTransient<RegisterWindow>();
            services.AddTransient<LoginWindow>();

            //Đăng ký cửa sổ Test Realtime (sẽ xoá sau)
            services.AddTransient<TestRealtimeWindow>();

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<UserSession>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // MessageBox.Show("1. Bắt đầu OnStartup");
            base.OnStartup(e);

            // MessageBox.Show("2. Init NetworkService");
            var networkService = Services.NetworkService.Instance;
            networkService.Initialize(); // Đã sửa ở bước trước

            // MessageBox.Show("3. Init Database");
            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.MigrateAsync(); // Dùng Async cho mượt
                }
            }
            catch (Exception ex)
            {
                // ... (Logic reset DB giữ nguyên) ...
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dbPath = Path.Combine(appData, "EasyFlips", "EasyFlipsAppDB.db");
                if (File.Exists(dbPath)) { try { File.Delete(dbPath); } catch { } }
                MessageBox.Show("Lỗi Database. Đã reset.");
                Current.Shutdown(); return;
            }

            // --- [THÊM MỚI] KHỞI TẠO SUPABASE CLIENT AN TOÀN ---
            // MessageBox.Show("3.5 Init Supabase");
            try
            {
                var supabase = ServiceProvider.GetRequiredService<Supabase.Client>();
                await supabase.InitializeAsync(); // Await ở đây an toàn, không bị treo
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối Supabase: {ex.Message}");
            }

            // MessageBox.Show("4. Kiểm tra Login");
            string savedId = Settings.Default.UserId;
            string savedToken = Settings.Default.UserToken;
            string savedEmail = Settings.Default.UserEmail;

            if (!string.IsNullOrEmpty(savedId) && !string.IsNullOrEmpty(savedToken))
            {
                try
                {
                    var userSession = ServiceProvider.GetRequiredService<UserSession>();
                    userSession.SetUser(savedId, savedEmail, savedToken);

                    var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                    // MessageBox.Show("5. Hiện MainWindow");
                    mainWindow.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi DI Main: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                    // MessageBox.Show("5. Hiện LoginWindow");
                    loginWindow.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi DI Login: {ex.Message}");
                }
            }
        }
    }
}