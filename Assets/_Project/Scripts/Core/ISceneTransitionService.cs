using System.Threading;
using System.Threading.Tasks;

namespace TheLastWatch.Core
{
    public interface ISceneTransitionService
    {
        bool IsLoading { get; }
        Task LoadAsync(string sceneName, CancellationToken cancellationToken);
    }
}
