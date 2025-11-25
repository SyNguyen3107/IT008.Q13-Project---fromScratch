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
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Converters;

namespace EasyFlips
{
    public partial class App : Application
    {
        // --- QUAN TRỌNG: Biến này giúp các ViewModel truy cập DI ---
        public static IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
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

            // Tự động copy file DB mẫu nếu chưa có (lần chạy đầu tiên)
            string sourceDb = Path.Combine(baseDirectory, "EasyFlipsAppDB.db");
            if (!File.Exists(dbPath) && File.Exists(sourceDb))
            {
                File.Copy(sourceDb, dbPath);
            }
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

            // 4. Đăng ký ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<StudyViewModel>();
            services.AddTransient<CreateDeckViewModel>();
            services.AddTransient<AddCardViewModel>();
            services.AddTransient<DeckChosenViewModel>();
            services.AddTransient<DeckRenameViewModel>();
            services.AddTransient<ChooseDeckViewModel>();

            // 5. Đăng ký Views (Cửa sổ)
            services.AddTransient<MainWindow>();
            services.AddTransient<StudyWindow>();
            services.AddTransient<CreateDeckWindow>();
            services.AddTransient<AddCardWindow>();
            services.AddTransient<DeckChosenWindow>();
            services.AddTransient<DeckRenameWindow>();
            services.AddTransient<ChooseDeckWindow>();
            services.AddTransient<SyncWindow>();

            // 6. Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Mở cửa sổ chính khi khởi động
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            base.OnStartup(e);
        }
    }
}