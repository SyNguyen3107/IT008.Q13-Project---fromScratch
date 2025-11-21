using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Repositories;
using IT008.Q13_Project___fromScratch.Services;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.ViewModels;
using IT008.Q13_Project___fromScratch.Views;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;

namespace IT008.Q13_Project___fromScratch
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public App()
        {
            var servicers = new ServiceCollection();

            ConfigureServices(servicers);

            ServiceProvider = servicers.BuildServiceProvider();

        }
        private void ConfigureServices(IServiceCollection services)
        {
            // === ĐĂNG KÝ CƠ SỞ DỮ LIỆU (ĐÃ SỬA LỖI) ===

            // 1. Lấy thư mục chạy (ví dụ: .../bin/Debug/net6.0)
            string exePath = AppDomain.CurrentDomain.BaseDirectory;

            // 2. Đi lùi 3 cấp để tìm về thư mục gốc của dự án
            //    (Lưu ý: số lượng '../' có thể cần điều chỉnh nếu cấu trúc dự án của bạn khác)
            string projectRoot = Path.GetFullPath(Path.Combine(exePath, "../../../"));

            // 3. Kết hợp để có đường dẫn tuyệt đối đến CSDL ở thư mục gốc
            string dbPath = Path.Combine(projectRoot, "AnkiAppDB.db");

            services.AddDbContext<AppDbContext>(options =>
            {
                // Sử dụng đường dẫn tuyệt đối dbPath
                options.UseSqlite($"Data Source={dbPath}");
            });
            

            // === ĐĂNG KÝ REPOSITORIES ===
            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<ICardRepository, CardRepository>();

            // === ĐĂNG KÝ SERVICES ===
            services.AddScoped<StudyService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<ExportService>();
            services.AddTransient<ImportService>();

            // === ĐĂNG KÝ VIEWMODELS ===
            services.AddTransient<MainAnkiViewModel>();
            services.AddTransient<StudyViewModel>();
            services.AddTransient<CreateDeckViewModel>();
            services.AddTransient<AddCardViewModel>();
            services.AddTransient<DeckChosenViewModel>();
            services.AddTransient<DeckRenameViewModel>();
            services.AddTransient<DeckRenameWindow>(); // Nếu chưa có

            // === ĐĂNG KÝ CÁC CỬA SỔ (VIEWS) ===
            services.AddTransient<MainAnkiWindow>();
            services.AddTransient<StudyWindow>();
            services.AddTransient<CreateDeckWindow>();
            services.AddTransient<AddCardWindow>();
            services.AddTransient<DeckChosenWindow>();
          


          

            // Đăng ký Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            var mainWindow = ServiceProvider.GetRequiredService<MainAnkiWindow>();
            mainWindow.Show();
            base.OnStartup(e);
        }
    }
}