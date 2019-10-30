using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using CryptoBot.Exchanges;

namespace CryptoBot.Scripting
{
    public class Strategy : IDisposable
    {
        public bool Disposed { get; private set; }

        public PairSelectorScript  PairSelector;
        public SignalEmitterScript SignalEmitterBase;
        public OrderManagerScript  OrderManagerBase;

        public Dictionary<Market, SignalEmitterScript> SignalEmitters;
        public Dictionary<Market, OrderManagerScript> OrderManagers;

        // public IObservable<(Market Market, double Signal)> OnEmitSignal;

        private IDisposable _onEnableMarketSub;
        private IDisposable _onDisableMarketSub;
        private Dictionary<SignalEmitterScript, IDisposable> _onSignalSubs;

        public Strategy
        (
            PairSelectorScript  pairSelector,
            SignalEmitterScript signalEmitterBase,
            OrderManagerScript  orderManagerBase
        )
        {
            Disposed = false;

            PairSelector      = pairSelector;
            SignalEmitterBase = signalEmitterBase;
            OrderManagerBase  = orderManagerBase;

            SignalEmitters = new Dictionary<Market, SignalEmitterScript>();
            OrderManagers = new Dictionary<Market, OrderManagerScript>();

            _onEnableMarketSub = PairSelector.OnEnableMarket
                .Subscribe(market => OnEnableMarket(market));

            _onDisableMarketSub = PairSelector.OnDisableMarket
                .Subscribe(market => OnDisableMarket(market));

            _onSignalSubs = new Dictionary<SignalEmitterScript, IDisposable>();
        }

        public async void OnEnableMarket(Market market)
        {
            if (!OrderManagers.ContainsKey(market))
            {
                var orderManager = OrderManagerBase.Clone(market);
                OrderManagers[market] = orderManager;
                await orderManager.Execute();
            }

            if (!SignalEmitters.ContainsKey(market))
            {
                var signalEmitter = SignalEmitterBase.Clone(market);
                SignalEmitters[market] = signalEmitter;
                _onSignalSubs[signalEmitter] = signalEmitter.OnSignal
                    .Subscribe(signal => OnEmitSignal(market, signal));
                await signalEmitter.Execute();
            }
        }

        public void OnDisableMarket(Market market)
        {
            if (!SignalEmitters.ContainsKey(market)) 
                throw new Exception("Null signal emitter");

            var signalEmitter = SignalEmitters[market];
            _onSignalSubs[signalEmitter].Dispose();
            _onSignalSubs.Remove(signalEmitter);
            signalEmitter.Dispose();
            SignalEmitters.Remove(market);

            // TODO: Dispose order manager when all active trades close
        }

        public void OnEmitSignal(Market market, double signal)
        {
            if (!OrderManagers.ContainsKey(market))
                throw new Exception("Null order manager");

            OrderManagers[market].OrderManager.OnSignal(signal);
        }

        public void Dispose()
        {
            if (Disposed) return;

            _onEnableMarketSub.Dispose();
            _onDisableMarketSub.Dispose();
            
            foreach (var onSignalSub in _onSignalSubs.Values)
                onSignalSub.Dispose();
        }
    }
}