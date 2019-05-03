namespace CryptoBot.Indicators
{
    public class IndicatorFactory
    {
        private IndicatorManifold _manifold;

        public IndicatorFactory(IndicatorManifold manifold)
        {
            _manifold = manifold;
        }

        public IndicatorMultiInstance<T>
            CreateRaw<T>(params dynamic[] settings) where T : Indicator =>
                _manifold.CreateRaw<T>(settings);
    }
}