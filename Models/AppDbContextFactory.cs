using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace IT008.Q13_Project___fromScratch.Models
{
    // Lớp này chỉ được dùng bởi các công cụ design-time (như Add-Migration)
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // Đây là nơi bạn chỉ định CSDL sẽ dùng.
            
            string dbPath = Path.Combine(Directory.GetCurrentDirectory(), "AnkiAppDB.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}