using System.Threading;
using System.Threading.Tasks;

namespace TheLastWatch.Voice
{
    public interface IVoiceProvider
    {
        bool IsAvailable { get; }
        Task SpeakAsync(string text, CancellationToken cancellationToken);
    }
}
