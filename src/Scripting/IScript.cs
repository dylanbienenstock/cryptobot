using System.Threading.Tasks;
using CryptoBot.Exchanges;
using CryptoBot.Scripting.Modules;

namespace CryptoBot.Scripting
{
    public interface IScript
    {
        bool Disposed { get; }

        void Dispose();
        Task Execute();
        void ListenForMessages();
        void OnDisposed();
        void OnExecuted();
    }
}