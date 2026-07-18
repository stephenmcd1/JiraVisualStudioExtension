using System;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using JiraVisualStudioExtension.Options;
using JiraVisualStudioExtension.Utilities;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace JiraVisualStudioExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    [ProvideOptionPage(typeof(GeneralOptions), "Jira Extension", "Jira Connection", 0, 0, true)]
    [ProvideOptionPage(typeof(LinkDefinitionOptions), "Jira Extension", "Issue Link Patterns", 0, 0, true)]
    public sealed class VS2022Package : AsyncPackage
    {
        /// <summary>
        /// JiraVisualStudioExtension.VS2022Package GUID string.
        /// </summary>
        public const string PackageGuidString = "c4c8d19a-8394-4d1a-9d91-675b47d64cb0";

        public static VS2022Package Instance { get; private set; }
        public static DTE Dte { get; set; }

        public static OptionsHelper Options
        {
            get
            {
                EnsureLoaded();
                return _options;
            }
        }

        private static OptionsHelper _options;


        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Instance = this;
            Dte = (DTE)GetGlobalService(typeof(DTE));
            _options = new OptionsHelper(this);
        }

        public static void EnsureLoaded()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Instance != null)
                return;
            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsShell)) is IVsShell shell)
            {
                var packageToBeLoadedGuid = new Guid(PackageGuidString);
                shell.LoadPackage(ref packageToBeLoadedGuid, out _);
            }
        }
    }
}