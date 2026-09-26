using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheLastWatch.Voice
{
    public sealed class NullSpeechRecognitionProvider : ISpeechRecognitionProvider
    {
        public event Action<string> TranscriptReceived
        {
            add { }
            remove { }
        }

        public bool IsAvailable => false;
        public bool IsListening => false;

        public Task StartListeningAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopListeningAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
