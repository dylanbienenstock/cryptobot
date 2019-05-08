using System;
using System.IO;
using CryptoBot.Exchanges;
using CryptoBot.Indicators;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Storage
{
    public class TradingPeriodHistoryContext : DbContext
    {
        public DbSet<ExchangeHistory> ExchangeHistories { get; set; }
        public DbSet<PairHistory>     PairHistories     { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Join(Environment.CurrentDirectory, "..");
            var dbFile = Path.Join(dbPath, "ExchangeHistory.db");
            // var dbFile = "/home/db/usb/ExchangeHistory.db";

            optionsBuilder.UseSqlite($"Data Source={dbFile}");
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PairHistory>()
                .HasOne(p => p.ExchangeHistory)
                .WithMany(e => e.PairHistories);

            modelBuilder.Entity<HistoricalTradingPeriod>()
                .HasKey(h => new { h.Minute, h.PairHistoryId });

            modelBuilder.Entity<HistoricalTradingPeriod>()
                .HasIndex(h => h.PairHistoryId);            
        }
    }
}