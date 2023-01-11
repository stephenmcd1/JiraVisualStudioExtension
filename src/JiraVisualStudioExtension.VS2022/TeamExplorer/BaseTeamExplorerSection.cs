using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using JiraVisualStudioExtension.Properties;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Threading;

namespace JiraVisualStudioExtension.TeamExplorer
{
    /// <summary>
    /// Base class for <see cref="ITeamExplorerSection"/>.
    /// </summary>
    /// <typeparam name="T">The type of the inner section content</typeparam>
    public abstract class BaseTeamExplorerSection<T> : ITeamExplorerSection
    {
        /// <summary>
        /// An additional string to show in our title when collapsed
        /// </summary>
        private string _additionalTitle;

        /// <summary>
        /// Our inner XAML content
        /// </summary>
        protected T Content { get; private set; }

        /// <summary>
        /// The Service Provider passed to our section
        /// </summary>
        protected IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// Creates the inner content that will be shown in this section
        /// </summary>
        protected abstract T CreateContent();

        /// <summary>
        /// The base title to use for this section when expanded (or used as the prefix when collapsed)
        /// </summary>
        protected abstract string BaseTitle { get; }

        /// <summary>
        /// Performs one-time initialization of this section
        /// </summary>
        protected virtual Task InitializeAsync()
        {
            return TplExtensions.CompletedTask;
        }

        /// <summary>
        /// Called when the user Refreshes this section
        /// </summary>
        protected virtual Task RefreshAsync()
        {
            return TplExtensions.CompletedTask;
        }

        #region Implementation of ITeamExplorerSection

        public async void Initialize(object sender, SectionInitializeEventArgs args)
        {
            ServiceProvider = args.ServiceProvider;

            SetAdditionalExpandedTitle(_additionalTitle);

            Content = CreateContent();
            try
            {
                IsBusy = true;
                await InitializeAsync();
            }
            catch (Exception e)
            {
                ShowNotification($"{BaseTitle} Error during Initialize: {e.Message}", NotificationType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async void Refresh()
        {
            try
            {
                IsBusy = true;
                await RefreshAsync();
            }
            catch (Exception e)
            {
                ShowNotification($"{BaseTitle} Error during Refresh: {e.Message}", NotificationType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Loaded(object sender, SectionLoadedEventArgs e)
        {
        }

        public void SaveContext(object sender, SectionSaveContextEventArgs e)
        {
        }

        public void Cancel()
        {
        }

        public object GetExtensibilityService(Type serviceType)
        {
            return null;
        }

        private string _title;
        private bool _isExpanded = true;
        private bool _isBusy;

        public string Title
        {
            get { return _title; }
            private set
            {
                if (value == _title) return;
                _title = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value == _isExpanded) return;
                _isExpanded = value;
                OnPropertyChanged();
                SetAdditionalExpandedTitle(_additionalTitle);
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (value == _isBusy) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public object SectionContent => Content;

        public bool IsVisible { get; set; } = true;

        #endregion

        #region Helper Methods

        public void SetAdditionalExpandedTitle(string text)
        {
            _additionalTitle = text;
            Title = BaseTitle + (IsExpanded || _additionalTitle == null ? "" : " - " + _additionalTitle);
        }

        protected TService GetService<TService>()
        {
            return (TService) ServiceProvider?.GetService(typeof(TService));
        }

        public void ShowNotification(string message, NotificationType type, ICommand command = null, Guid guid = default(Guid))
        {
            var teamExplorer = GetService<ITeamExplorer>();
            if (teamExplorer == null)
            {
                return;
            }

            guid = guid == default(Guid) ? Guid.NewGuid() : guid;
            teamExplorer.ShowNotification(message, type, NotificationFlags.None, command, guid);
        }

        #endregion

        #region Property Change

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable-related

        /// <summary>
        /// A list of actions to run as part of dispose
        /// </summary>
        private readonly List<Action> _disposeActions = new List<Action>();

        protected void AddDisposeAction(Action a)
        {
            _disposeActions.Add(a);
        }

        public void Dispose()
        {
            foreach (var a in _disposeActions)
            {
                a();
            }

            _disposeActions.Clear();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}