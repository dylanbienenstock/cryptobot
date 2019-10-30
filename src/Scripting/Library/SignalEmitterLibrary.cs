using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "__signalEmitter", instanced: true)]
    public class SignalEmitterLibrary : InstancedLibrary
    {
        private SignalEmitterScript _signalEmitter;

        public SignalEmitterLibrary(ScriptContext context) : base(context)
        {
            _signalEmitter = context.Script as SignalEmitterScript;
        }

        [TypescriptDocumentation("Emits a signal to the Order Manager module.")]
        [TypescriptDocumentation.Parameter("signal", 
@"A number ranging from -1 to 1.
* n < -0.5: `Signal.StrongSell`
* 0 > n >= -0.5: `Signal.Sell`
* -0.5 < n < 0.5: `Signal.Neutral`
* 0 < n <= 0.5: `Signal.Buy`
* n > 0.5: `Signal.StrongBuy`")]
        public void EmitSignal(double signal)
        {
            _signalEmitter.OnSignal.OnNext(signal);
        }
    }
}