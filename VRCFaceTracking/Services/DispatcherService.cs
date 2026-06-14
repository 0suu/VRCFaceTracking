using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Services;

// Simple service to invoke actions on the UI thread from the Core project.
public class DispatcherService : IDispatcherService
{
    public void Run(Action action)
    {
        var dispatcher = App.MainWindow.DispatcherQueue;
        if (dispatcher == null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        if (!dispatcher.TryEnqueue(action.Invoke))
        {
            action();
        }
    }
}
