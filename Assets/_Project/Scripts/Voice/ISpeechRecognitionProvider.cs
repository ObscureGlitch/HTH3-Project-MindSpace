using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheLastWatch.Voice
{
    public interface ISpeechRecognitionProvider
    {
        event Action<string> TranscriptReceived;

        bool IsAvailable { get; }
        bool IsListening { get; }
        Task StartListeningAsync(CancellationToken cancellationToken);
        Task StopListeningAsync(CancellationToken cancellationToken);
    }
}
