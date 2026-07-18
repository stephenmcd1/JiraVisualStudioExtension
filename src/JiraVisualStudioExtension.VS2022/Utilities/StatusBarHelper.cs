using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace JiraVisualStudioExtension.Utilities
{
    public static class StatusBarHelper
    {
        public static async Task ShowMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                VS2022Package.Dte.StatusBar.Text = message;
            }
            catch (Exception exc)
            {
                await OutputPane.Instance.WriteAsync(exc);
            }
        }
    }
}