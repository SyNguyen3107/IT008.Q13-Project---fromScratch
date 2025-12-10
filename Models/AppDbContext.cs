using Microsoft.EntityFrameworkCore;
namespace EasyFlips.Models
{
    public class AppDbContext : DbContext
    {
        public DbSet<Deck> Decks { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<CardProgress> CardProgresses { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Deck
            modelBuilder.Entity<Deck>(e =>
            {
                e.ToTable("Decks");
                e.HasKey(d => d.Id);
                e.Property(d => d.Name).IsRequired().HasMaxLength(200);
                e.Property(d => d.Description).HasMaxLength(1000);
                e.Property(d => d.UserId).HasMaxLength(128);
            });

            // Card
            modelBuilder.Entity<Card>(e =>
            {
                e.ToTable("Cards");
                e.HasKey(c => c.Id);

                e.Property(c => c.FrontText).IsRequired();
                e.Property(c => c.BackText).IsRequired();

                e.Property(c => c.FrontImagePath).HasMaxLength(512);
                e.Property(c => c.FrontAudioPath).HasMaxLength(512);
                e.Property(c => c.BackImagePath).HasMaxLength(512);
                e.Property(c => c.BackAudioPath).HasMaxLength(512);

                e.HasOne(c => c.Deck)
                    .WithMany(d => d.Cards)
                    .HasForeignKey(c => c.DeckId)
                    .OnDelete(DeleteBehavior.Cascade);
                // [QUAN TRỌNG - THÊM MỚI]: Cấu hình quan hệ 1-1 với CardProgress
                e.HasOne(c => c.Progress)
                    .WithOne(p => p.Card)
                    .HasForeignKey<CardProgress>(p => p.CardId) // Chỉ định rõ CardProgress giữ khóa ngoại
                    .OnDelete(DeleteBehavior.Cascade); // Xóa Card thì xóa luôn Progress
            });
            modelBuilder.Entity<CardProgress>(e =>
            {
                e.ToTable("CardProgresses");// Tên bảng mới
                e.HasKey(p => p.Id);

                e.Property(p => p.DueDate).IsRequired();
                e.Property(p => p.Interval).HasPrecision(18, 6);
                e.Property(p => p.EaseFactor).HasPrecision(18, 6);

                e.HasIndex(p => p.DueDate);
            });

        }
    }
}

