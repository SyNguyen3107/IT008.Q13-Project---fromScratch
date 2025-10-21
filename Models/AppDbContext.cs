using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
namespace IT008.Q13_Project___fromScratch.Models
{
    public class AppDbContext : DbContext
    {
        public DbSet<Deck> Decks { get; set; }
        public DbSet<Card> Cards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Deck
            modelBuilder.Entity<Deck>(e =>
            {
                e.ToTable("Decks");
                e.HasKey(d => d.ID);
                e.Property(d => d.Name).IsRequired().HasMaxLength(200);
                e.Property(d => d.Description).HasMaxLength(1000);
            }
            );

            // Card
            modelBuilder.Entity<Card>(e =>
            {
                e.ToTable("Cards");
                e.Hashkey(c => c.Id);

                e.Property(c => c.FrontText).IsRequired();
                e.Property(c => c.BackText).IsRequired();

                e.Property(c => FromImagePath).HasMaxLength(512);
                e.Property(c => FrontAudioPath).HasMaxLength(512);
                e.Property(c => BackImagePath).HasMaxLength(512);
                e.Property(c => BackAudioPath).HasMaxLength(512);

                e.Property(c => c.DueDate).IsRequired();
                e.Property(c => c.Interval).HasPrecision(18, 6);
                e.Property(c => c.EaseFactor).HasPrecision(18, 6);

                e.HasOne(c => c.Deck)
                 .WithMany(d => d.Cards)
                 .HasForeignKey(c => c.DeckId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HashIndex(c => new { c.DeckId, c.DueDate });
            });

        }
    }
}
      
