using System;
using System.Timers;
using CryptoBot.Scripting.Typings;
using Jint.Native;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "__interval", instanced: true)]
    public class IntervalLibrary : InstancedLibrary
    {
        public IntervalLibrary(ScriptContext context) : base(context) { }

        public void createInterval(long millis, JsValue callback)
        {
            Timer timer = new Timer(millis);
            timer.AutoReset = true;
            timer.Elapsed += (_, __) =>
            {
                if (Context.Disposed)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                callback.Invoke(Context.ModuleObject, new JsValue[0]);
            };
            timer.Start();
        }
    }
}