using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Indicators;
using CryptoBot.Series;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Storage
{
    using DbContext = TradingPeriodHistoryContext;

    public static class BacktestDatabase
    {
        private static Dictionary<Market, CancellationTokenSource> _dataCollectionCancellers = 
            new Dictionary<Market, CancellationTokenSource>();

        private static Dictionary<Market, CompletionTuple> _cachedDataCompletion = 
            new Dictionary<Market, CompletionTuple>();

        private static SemaphoreSlim _dbWriteSemaphore = new SemaphoreSlim(1, 1);

        public static Subject<HistoricalTradingPeriod> Playback(Market market)
        {
            var subject = new Subject<HistoricalTradingPeriod>();

            Task.Run(() => {
                using (var context = new DbContext())
                {
                    Console.WriteLine("\n\n\nReading...");
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    var query = GetTradingPeriodQuery(context, market)
                        .AsNoTracking();

                    var startTime = query.First().Time;
                    var endTime   = query.Last().Time;
                    var seekTime  = startTime;
                    var pageSize  = 10000;
                    var lastTime = startTime;

                    var lastMinute = query.First().Minute - 1;
                    var allPeriods = new List<HistoricalTradingPeriod>();

                    for (int i = 0; startTime.AddMinutes(i) < endTime; i += pageSize)
                    {
                        var periods = query.Skip(i).Take(pageSize);
                        periods.Load();
                        allPeriods.AddRange(periods);
                    }

                    // stopwatch.Stop();
                    // Console.WriteLine($"Read time: {stopwatch.ElapsedMilliseconds}ms");
                    // Console.WriteLine($"Records: {allPeriods.Count}");
                    // stopwatch.Restart();

                    allPeriods = allPeriods
                        .OrderBy(p => p.Minute)
                        .ToList();

                    // stopwatch.Stop();
                    // Console.WriteLine($"Sort time: {stopwatch.ElapsedMilliseconds}ms");
                    // stopwatch.Restart();

                    // int notSorted = 0;
                    // int duplicate = 0;

                    // for (int i = 1; i < allPeriods.Count; i++) {
                    //     if (allPeriods[i].Minute < allPeriods[i - 1].Minute)
                    //         notSorted++;
                    //     else if (allPeriods[i].Minute == allPeriods[i - 1].Minute)
                    //         duplicate++;
                    // }

                    // stopwatch.Stop();
                    // Console.WriteLine($"Ensure sorted time: {stopwatch.ElapsedMilliseconds}ms");
                    // Console.WriteLine($"Unsorted entries: {notSorted}");
                    // Console.WriteLine($"Duplicate entries: {duplicate}");
                }
            });

            return subject;
        }

        // public static Subject<HistoricalTradingPeriod> Playback(Market market)
        // {
        //     var subject = new Subject<HistoricalTradingPeriod>();

        //     Task.Run(() => {
        //         using (var context = new DbContext())
        //         {
        //             var stopwatch = new Stopwatch();
        //             stopwatch.Start();

        //             var query = GetTradingPeriodQuery(context, market)
        //                 .AsNoTracking()
        //                 .OrderBy(p => p.Minute);

        //             var startTime = query.First().Time;
        //             var endTime   = query.Last().Time;
        //             var seekTime  = startTime;
        //             var pageSize  = 1440; // One day

        //             var count = 0;
        //             var lastTime = startTime;

        //             var lastMinute = query.First().Minute - 1;

        //             for (int i = 0; startTime.AddMinutes(i) < endTime; i += pageSize)
        //             {
        //                 var periods = query.Skip(i).Take(pageSize);
        //                 periods.Load();

        //                 foreach (var period in periods)
        //                 {
        //                     if (period.Minute < lastMinute) {
        //                         Console.WriteLine("BAD SORT!!!");
        //                         // throw new Exception("BAD SORT");
        //                     }

        //                     lastMinute = period.Minute;

        //                     count++;
        //                     lastTime = period.Time;
        //                     subject.OnNext(period);
        //                 }
        //             }
        //             stopwatch.Stop();
        //             Console.WriteLine($"Last time: {lastTime} | Read time: {stopwatch.ElapsedMilliseconds}ms | Records: {count}");
        //         }
        //     });

        //     return subject;
        // }

        public static Subject<CompletionTuple> StartCollectingData(Market market)
        {
            if (_dataCollectionCancellers.ContainsKey(market))
                throw new Exception("Attempted to open multiple streams for the same market");

            _dataCollectionCancellers[market] = new CancellationTokenSource();
            _cachedDataCompletion.Remove(market);
            
            var cancellationToken = _dataCollectionCancellers[market].Token;
            var subject = new Subject<CompletionTuple>();

            Task.Run(async () =>
            {
                var symbol     = market.Pair.ToString("");
                var startTime  = DateTime.UtcNow;
                var cursorTime = DateTime.MaxValue;

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using (var context = new DbContext())
                        {
                            var query = GetTradingPeriodQuery(context, market);

                            if (cursorTime == DateTime.MaxValue)
                                cursorTime = await GetCursorTime(market, context);

                            var pairHistory  = GetPairHistory(context, market);
                            var cursorMillis = (cursorTime - DateTime.UnixEpoch).TotalMilliseconds;

                            var historicalPeriods = await market.Exchange.FetchTradingPeriods
                            (
                                symbol: symbol,
                                startTime: cursorMillis,
                                timeFrame: 60000,
                                count: 1000,
                                priority: 0
                            );

                            if (historicalPeriods.Count == 0) break;

                            await _dbWriteSemaphore.WaitAsync();

                            try
                            {
                                var distinctPeriods = historicalPeriods
                                    .Where(p => !query.Any(h => h.Minute == p.Minute))
                                    .Distinct()
                                    .ToList();
                                    
                                pairHistory.TradingPeriods.AddRange(distinctPeriods);
                                pairHistory.CursorTime = historicalPeriods.Last().Time.AddMinutes(1);
                                context.SaveChanges();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(ex);
                                Console.ResetColor();
                                Environment.Exit(1);
                            }
                            finally
                            {
                                _dbWriteSemaphore.Release();
                            }

                            cursorTime = (DateTime)pairHistory.CursorTime;

                            var firstPeriod  = await GetFirstHistoricalTradingPeriod(market, query);
                            var listingTime  = firstPeriod.Time.Quantize(60000);
                            var totalDays    = (DateTime.UtcNow - listingTime).TotalDays;
                            var completeDays = totalDays - (DateTime.UtcNow - cursorTime).TotalDays;
                            var completion   = new CompletionTuple(completeDays, totalDays, true);

                            subject.OnNext(completion);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(ex);
                        Environment.Exit(1);
                        subject.OnError(ex);
                    }
                }

                subject.OnCompleted();

            }, cancellationToken);

            return subject;
        }

        public static void StopCollectingData(Market market)
        {
            if (!_dataCollectionCancellers.ContainsKey(market))
                throw new Exception("Attempted to cancel a non-existent stream");

            _dataCollectionCancellers[market].Cancel();
            _dataCollectionCancellers[market].Dispose();
            _dataCollectionCancellers[market] = null;
            _dataCollectionCancellers.Remove(market);
        }

        private static ExchangeHistory GetExchangeHistory
        (
            DbContext context,
            Exchange exchange
        )
        {
            _dbWriteSemaphore.Wait();

            ExchangeHistory exchangeHistory = null;

            try
            {
                exchangeHistory = context.ExchangeHistories
                    .SingleOrDefault(e => e.ExchangeName == exchange.Name);

                if (exchangeHistory == null)
                {
                    exchangeHistory = new ExchangeHistory(exchange);
                    context.ExchangeHistories.Add(exchangeHistory);

                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _dbWriteSemaphore.Release();
            }

            return exchangeHistory;
        }

        private static PairHistory GetPairHistory(DbContext context, Market market)
        {
            var exchangeHistory = GetExchangeHistory(context, market.Exchange);
            PairHistory pairHistory = null;

            _dbWriteSemaphore.Wait();

            try
            {
                context.Entry(exchangeHistory)
                    .Collection(e => e.PairHistories)
                    .Load();
                
                pairHistory = exchangeHistory.PairHistories
                    .FirstOrDefault(p => p.Symbol == market.Pair.ToGenericSymbol());

                if (pairHistory == null)
                {
                    pairHistory = new PairHistory(market.Pair, exchangeHistory);
                    exchangeHistory.PairHistories.Add(pairHistory);

                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _dbWriteSemaphore.Release();
            }

            return pairHistory;
        }

        private static IQueryable<HistoricalTradingPeriod> GetTradingPeriodQuery(DbContext context, Market market)
        {
            var pairHistory = GetPairHistory(context, market);

            return context.Entry(pairHistory)
                .Collection(p => p.TradingPeriods)
                .Query();
        }

        private static Task<DateTime> GetCursorTime(Market market, DbContext context = null)
        {
            async Task<DateTime> _GetCursorTime(DbContext _context)
            {
                var pairHistory = GetPairHistory(context, market);
                DateTime? cursorTime = null;

                if (pairHistory.CursorTime != null)
                    return (DateTime)pairHistory.CursorTime;

                await _dbWriteSemaphore.WaitAsync();

                var query = context.Entry(pairHistory)
                    .Collection(p => p.TradingPeriods)
                    .Query();

                cursorTime = query.Any()
                    ? query.Last().Time.AddMinutes(1)
                    : (await market.GetFirstHistoricalTradingPeriod()).Time.Quantize(60000);

                if (cursorTime == null)
                    throw new Exception("Unable to determine cursor time");

                try
                {
                    pairHistory.CursorTime = cursorTime;
                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    _dbWriteSemaphore.Release();
                }

                return (DateTime)cursorTime;
            }

            if (context != null)
                return _GetCursorTime(context);

            using (var _context = new DbContext())
            {
                return _GetCursorTime(_context);
            }
        }

        private static Task<HistoricalTradingPeriod> GetFirstHistoricalTradingPeriod
        (
            Market market,
            IQueryable<HistoricalTradingPeriod> query
        )
        {
            async Task<HistoricalTradingPeriod> _Fetch(IQueryable<HistoricalTradingPeriod> _query)
            {
                if (query.FirstOrDefault() != null)
                    return query.First();

                return await market.GetFirstHistoricalTradingPeriod();
            }

            if (query == null)
            {
                using (var context = new DbContext())
                {
                    var _query = GetTradingPeriodQuery(context, market);
                    return _Fetch(_query);
                }
            }

            return _Fetch(query);
        }

        public static bool IsCollectingData(Market market) =>
            _dataCollectionCancellers.ContainsKey(market);

        public static async Task<CompletionTuple> GetDataCompletion(Market market)
        {
            if (_cachedDataCompletion.ContainsKey(market))
            {
                var cached = _cachedDataCompletion[market];
                cached.Collecting = IsCollectingData(market);
                return cached;
            }
            
            using (var context = new DbContext())
            {
                var query        = GetTradingPeriodQuery(context, market);
                var cursorTime   = (await GetCursorTime(market, context)).AddMinutes(-1);
                var firstPeriod  = await GetFirstHistoricalTradingPeriod(market, query);
                var listingTime  = firstPeriod.Time;
                var totalDays    = (DateTime.UtcNow - listingTime).TotalDays;
                var completeDays = totalDays - (DateTime.UtcNow - cursorTime).TotalDays;
                var isCollecting = IsCollectingData(market);    
                var completion   = new CompletionTuple(completeDays, totalDays, isCollecting);

                _cachedDataCompletion[market] = completion;

                // If we don't have any historical trading periods saved,
                // save the first one. Keeps us from having to query the 
                // exchanges API every time.
                if (query.Any()) return completion;
                
                var pairHistory = GetPairHistory(context, market);

                _dbWriteSemaphore.Wait();
                pairHistory.TradingPeriods.Add(firstPeriod);
                context.SaveChanges();
                _dbWriteSemaphore.Release();

                return completion;
            }
        }

        // public static List<HistoricalTradingPeriod> GetTradingPeriods(Market market, int startMinute, int endMinute)
        // {
        //     using (var context = new DbContext())
        //     {
        //         return GetTradingPeriodQuery(context, market)
        //             .Where(t => t.Minute >= startMinute && t.Minute <= endMinute)
        //             .OrderBy(t => t.Minute)
        //             .ToList();
        //     }
        // }
    }
}