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
            // === ĐĂNG KÝ CƠ SỞ DỮ LIỆU ===
            // Dạy cách tạo AppDbContext
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnkiAppDB.db");
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}");
            });

            // === ĐĂNG KÝ REPOSITORIES ===
            // Khi ai đó hỏi IDeckRepository, hãy đưa cho họ DeckRepository
            services.AddScoped<IDeckRepository, DeckRepository>();
            services.AddScoped<ICardRepository, CardRepository>();

            // === ĐĂNG KÝ SERVICES ===
            // Khi ai đó hỏi StudyService, hãy tạo một StudyService
            services.AddScoped<StudyService>();
            // "Khi ai đó hỏi INavigationService, hãy đưa cho họ NavigationService"
            // Dùng Singleton để chỉ có 1 dịch vụ điều hướng duy nhất
            services.AddSingleton<INavigationService, NavigationService>();

            // === ĐĂNG KÝ VIEWMODELS ===
            // ViewModel thường là Transient (tạo mới mỗi lần)
            services.AddTransient<MainAnkiViewModel>();
            services.AddTransient<StudyViewModel>();
            services.AddTransient<CreateDeckViewModel>(); // (Bạn sẽ tạo file này)

            // === ĐĂNG KÝ CÁC CỬA SỔ (VIEWS) ===
            services.AddTransient<MainAnkiWindow>();
            services.AddTransient<StudyWindow>();
            services.AddTransient<CreateDeckWindow>();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            // Tạo ra cửa sổ chính
            var mainWindow = ServiceProvider.GetRequiredService<MainAnkiWindow>();

            // Hiển thị cửa sổ
            mainWindow.Show();

            base.OnStartup(e);
        }
    }

}
