using CommunityToolkit.Mvvm.ComponentModel; // ✅ THÊM DÒNG NÀY
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
using EasyFlips.Properties; // Để dùng Settings

namespace EasyFlips
{
    public partial class App : Application
    {
        // --- QUAN TRỌNG: Biến này giúp các ViewModel truy cập DI ---
        public static IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            // 1. Đăng ký bắt lỗi toàn cục
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }
        // 2. Hàm xử lý lỗi UI
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Lỗi Crash UI: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Lỗi Nghiêm Trọng");
            e.Handled = true; // Thử giữ app không bị tắt
        }

        // 3. Hàm xử lý lỗi hệ thống/Domain
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Lỗi Crash AppDomain: {ex?.Message}\n\n{ex?.StackTrace}", "Lỗi Nghiêm Trọng");
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // === 1. CẤU HÌNH ĐƯỜNG DẪN DATABASE THÔNG MINH ===
            string dbPath;

            // Lấy đường dẫn thư mục chứa file .exe đang chạy
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Kiểm tra xem có đang chạy trong môi trường phát triển (Visual Studio) không?
            // Cách đơn giản: Kiểm tra xem có thư mục con "bin" hay không, hoặc dùng chỉ thị tiền xử lý #if DEBUG

#if DEBUG
            // TRƯỜNG HỢP DEBUG: Database nằm ở thư mục gốc dự án (đi lùi 3 cấp)
            // .../bin/Debug/net8.0-windows/ -> .../
            string projectRoot = Path.GetFullPath(Path.Combine(baseDirectory, "../../../"));
            dbPath = Path.Combine(projectRoot, "EasyFlipsAppDB.db");
#else
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "EasyFlips");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            dbPath = Path.Combine(appFolder, "EasyFlipsAppDB.db");

            // [SỬA LỖI CRASH KHI PUBLISH]
            // Đã comment đoạn copy file mẫu vì khi đóng gói SingleFile, file mẫu có thể không tìm thấy gây lỗi.
            // Chúng ta sẽ dùng db.Database.Migrate() ở OnStartup để tự tạo file mới an toàn hơn.
            /*
            string sourceDb = Path.Combine(baseDirectory, "EasyFlipsAppDB.db");
            if (!File.Exists(dbPath) && File.Exists(sourceDb))
            {
                File.Copy(sourceDb, dbPath);
            }
            */
#endif
            // Đăng ký DbContext
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}");
            });

            // 2. Đăng ký Repositories
            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<ICardRepository, CardRepository>();

            // 3. Đăng ký Services
            services.AddScoped<StudyService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<ExportService>();
            services.AddTransient<ImportService>();
            services.AddSingleton<AudioService>();
            // Đăng ký FirebaseAuthService làm IAuthService
            // Dùng Singleton vì chúng ta muốn giữ trạng thái đăng nhập trong suốt vòng đời ứng dụng
            services.AddSingleton<IAuthService, FirebaseAuthService>();

            // 4. Đăng ký ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<StudyViewModel>();
            services.AddTransient<CreateDeckViewModel>();
            services.AddTransient<AddCardViewModel>();
            services.AddTransient<DeckChosenViewModel>();
            services.AddTransient<DeckRenameViewModel>();
            services.AddTransient<ChooseDeckViewModel>();
            services.AddTransient<LoginViewModel>(); // Đăng ký LoginViewModel
            services.AddTransient<RegisterViewModel>();

            // 5. Đăng ký Views (Cửa sổ)
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


            // 6. Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // 7. Đăng ký phiên người dùng
            services.AddSingleton<UserSession>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // --- [QUAN TRỌNG] TỰ ĐỘNG KHỞI TẠO VÀ SỬA LỖI DATABASE ---
            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    // Tự động tạo bảng hoặc thêm cột mới nếu thiếu (hoặc tạo file mới nếu chưa có)
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
            // ---------------------------------------------------------

            // 1. Kiểm tra xem có dữ liệu đã lưu không (Remember Me)
            string savedId = Settings.Default.UserId;
            string savedToken = Settings.Default.UserToken; // (Token Firebase)
            string savedEmail = Settings.Default.UserEmail;

            if (!string.IsNullOrEmpty(savedId) && !string.IsNullOrEmpty(savedToken))
            {
                // 2. NẾU CÓ: Khôi phục Session
                var userSession = ServiceProvider.GetRequiredService<UserSession>();
                userSession.SetUser(savedId, savedEmail, savedToken);

                // 3. Mở thẳng MainWindow
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            else
            {
                // 4. NẾU KHÔNG: Mở LoginWindow như bình thường
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
        }
    }
}