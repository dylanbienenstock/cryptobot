namespace CryptoBot.Scripting.Library
{
    public class InstancedLibraryCall<T>
    {
        public readonly string InstanceId;
        public readonly T Arguments;

        public InstancedLibraryCall(string instanceId, T arguments)
        {
            InstanceId = instanceId;
            Arguments = arguments;
        }
    }
}