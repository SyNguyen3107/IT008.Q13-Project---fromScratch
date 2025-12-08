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

            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<ICardRepository, CardRepository>();
            services.AddScoped<StudyService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<ExportService>();
            services.AddTransient<ImportService>();
            services.AddSingleton<AudioService>();
            services.AddSingleton<IAuthService, FirebaseAuthService>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<StudyViewModel>();
            services.AddTransient<CreateDeckViewModel>();
            services.AddTransient<AddCardViewModel>();
            services.AddTransient<DeckChosenViewModel>();
            services.AddTransient<DeckRenameViewModel>();
            services.AddTransient<ChooseDeckViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();

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

            services.AddTransient<TestRealtimeWindow>();

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<UserSession>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var networkService = Services.NetworkService.Instance;
            // --- 1. KHÔI PHỤC LOGIC MIGRATE DATABASE (ĐỂ TRÁNH CRASH) ---
            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                // Nếu file DB bị hỏng quá nặng (lỗi cấu trúc cũ) -> Xóa đi làm lại
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dbPath = Path.Combine(appData, "EasyFlips", "EasyFlipsAppDB.db");

                if (File.Exists(dbPath))
                {
                    try { File.Delete(dbPath); } catch { }
                }

                MessageBox.Show($"Cơ sở dữ liệu đã được làm mới do phiên bản cũ không tương thích.\nVui lòng khởi động lại ứng dụng.",
                                "Thông báo cập nhật", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            // --- 2. CHẾ ĐỘ TEST REALTIME ---
            // Đã comment logic Login/Main cũ để vào thẳng Test
            
            string savedId = Settings.Default.UserId;
            string savedToken = Settings.Default.UserToken; 
            string savedEmail = Settings.Default.UserEmail;

            if (!string.IsNullOrEmpty(savedId) && !string.IsNullOrEmpty(savedToken))
            {
                var userSession = ServiceProvider.GetRequiredService<UserSession>();
                userSession.SetUser(savedId, savedEmail, savedToken);
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            else
            {
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
            

            //// --- 3. MỞ CỬA SỔ TEST ---
            //var testWindow = new Views.TestRealtimeWindow();
            //testWindow.Show();
        }
    }
}