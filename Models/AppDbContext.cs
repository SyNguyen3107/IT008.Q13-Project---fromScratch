using Microsoft.EntityFrameworkCore;
using Supabase.Postgrest.Models;

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
            // =========================================================
            // IGNORE ALL BASEMODEL PROPERTIES FROM SUPABASE
            // =========================================================
            // These properties are used by Supabase at runtime but should not be persisted

            modelBuilder.Entity<Deck>(e =>
            {
                // Ignore BaseModel properties that Supabase uses internally
                e.Ignore(d => d.BaseUrl);
                e.Ignore(d => d.PrimaryKey);
                e.Ignore(d => d.RequestClientOptions);
                e.Ignore(d => d.TableName);

                e.ToTable("Decks");
                e.HasKey(d => d.Id);
                e.Property(d => d.Name).IsRequired().HasMaxLength(200);
                e.Property(d => d.Description).HasMaxLength(1000);
                e.Property(d => d.UserId).HasMaxLength(128);
            });

            modelBuilder.Entity<Card>(e =>
            {
                // Ignore BaseModel properties
                e.Ignore(c => c.BaseUrl);
                e.Ignore(c => c.PrimaryKey);
                e.Ignore(c => c.RequestClientOptions);
                e.Ignore(c => c.TableName);

                e.ToTable("Cards");
                e.HasKey(c => c.Id);

                e.Property(c => c.FrontText).IsRequired();
                e.Property(c => c.BackText).IsRequired();

                e.Property(c => c.FrontImagePath).HasMaxLength(512);
                e.Property(c => c.FrontAudioPath).HasMaxLength(512);
                e.Property(c => c.BackImagePath).HasMaxLength(512);
                e.Property(c => c.BackAudioPath).HasMaxLength(512);

                // Quan hệ 1-N với Deck
                e.HasOne(c => c.Deck)
                 .WithMany(d => d.Cards)
                 .HasForeignKey(c => c.DeckId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Quan hệ 1-1 với CardProgress
                e.HasOne(c => c.Progress)
                 .WithOne(p => p.Card)
                 .HasForeignKey<CardProgress>(p => p.CardId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CardProgress>(e =>
            {
                // Ignore BaseModel properties
                e.Ignore(p => p.BaseUrl);
                e.Ignore(p => p.PrimaryKey);
                e.Ignore(p => p.RequestClientOptions);
                e.Ignore(p => p.TableName);

                e.ToTable("CardProgresses");
                e.HasKey(p => p.Id);

                e.Property(p => p.DueDate).IsRequired();
                e.Property(p => p.Interval).HasPrecision(18, 6);
                e.Property(p => p.EaseFactor).HasPrecision(18, 6);

                e.HasIndex(p => p.DueDate);
            });
        }
    }
}