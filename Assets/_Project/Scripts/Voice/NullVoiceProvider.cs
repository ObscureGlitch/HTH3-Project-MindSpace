using System.Threading;
using System.Threading.Tasks;

namespace TheLastWatch.Voice
{
    public sealed class NullVoiceProvider : IVoiceProvider
    {
        public bool IsAvailable => false;

        public Task SpeakAsync(string text, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
