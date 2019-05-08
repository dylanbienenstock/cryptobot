using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CryptoBot.Exchanges;
using CryptoBot.Indicators;

namespace CryptoBot.Storage
{
    public class ExchangeHistory
    {
        [Key]
        public int Id { get; set; }
        public string ExchangeName { get; set; }
        public List<PairHistory> PairHistories { get; set; }

        public ExchangeHistory() { }

        public ExchangeHistory(Exchange exchange)
        {
            ExchangeName = exchange.Name;
            PairHistories = new List<PairHistory>();
        }
    }
}