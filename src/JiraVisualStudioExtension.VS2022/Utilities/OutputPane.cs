using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace JiraVisualStudioExtension.Utilities
{
    public class OutputPane
    {
        private static Guid _dsPaneGuid = new("61839752-500E-4B8C-AA1E-0F62BD71C741");
        private static OutputPane _instance;
        private readonly IVsOutputWindowPane _pane;

        private OutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow
                && (ErrorHandler.Failed(outWindow.GetPane(ref _dsPaneGuid, out _pane)) || _pane == null))
            {
                if (ErrorHandler.Failed(outWindow.CreatePane(ref _dsPaneGuid, "Jira", 1, 0)))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create output pane.");
                    return;
                }

                if (ErrorHandler.Failed(outWindow.GetPane(ref _dsPaneGuid, out _pane)) || (_pane == null))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get output pane.");
                }
            }
        }

        public static OutputPane Instance => _instance ??= new OutputPane();

        public async Task ActivateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            _pane?.Activate();
        }

        public async Task WriteAsync(Exception exception, [CallerMemberName] string caller = "*unknown method*")
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            await WriteAsync(string.Empty);
            await WriteAsync(DateTimeOffset.Now.ToString());
            await WriteAsync($"Error in {caller}");
            await WriteAsync(exception.Message);
            await WriteAsync(exception.Source);
            await WriteAsync(exception.StackTrace);
        }

        public async Task WriteAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            _pane?.OutputStringThreadSafe($"{message}{Environment.NewLine}");
        }

        public void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.Activate();
        }

        public void WriteLine(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _pane?.OutputStringThreadSafe($"{message}{Environment.NewLine}");
        }
    }
}