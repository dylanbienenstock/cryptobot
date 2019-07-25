using CryptoBot.Scripting.Typings;

namespace CryptoBot.Scripting.Library
{
    [TypescriptDefine(name: "console", instanced: true)]
    public class JavascriptConsole : InstancedLibrary
    {
        public void Log(string text) =>
            JavascriptHost.OnLog.OnNext(new InstancedLibraryCall<string>(InstanceId, text));

        public void Warn(string text) =>
            JavascriptHost.OnWarn.OnNext(new InstancedLibraryCall<string>(InstanceId, text));

        public void Error(string text) =>
            JavascriptHost.OnError.OnNext(new InstancedLibraryCall<string>(InstanceId, text));
    }
}