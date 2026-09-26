using System.Threading;
using System.Threading.Tasks;

namespace TheLastWatch.Dialogue
{
    public interface IDialogueService
    {
        bool IsAvailable { get; }
        Task<string> GetResponseAsync(string playerText, CancellationToken cancellationToken);
    }
}
