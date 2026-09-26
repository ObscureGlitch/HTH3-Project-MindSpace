using System.Threading;
using System.Threading.Tasks;

namespace TheLastWatch.Dialogue
{
    public sealed class NullDialogueService : IDialogueService
    {
        public bool IsAvailable => false;

        public Task<string> GetResponseAsync(string playerText, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
