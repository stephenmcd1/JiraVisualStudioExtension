using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using JiraVisualStudioExtension.Properties;
using JiraVisualStudioExtension.Utilities;
using Microsoft.TeamFoundation.MVVM;

namespace JiraVisualStudioExtension.ViewModels
{
    /// <summary>
    /// Helper class to handle paging across a collection of tasks
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class PagedItemListViewModel : INotifyPropertyChanged
    {
        private readonly int _pageSize;
        private readonly string _jql;
        private readonly JiraHelper _jiraApi;
        private readonly Action<string> _onError;
        private readonly Dictionary<int, JiraHelper.QueryResult> _cachedPages = new Dictionary<int, JiraHelper.QueryResult>();

        public ObservableCollection<JiraIssueViewModel> Items { get; } = new ObservableCollection<JiraIssueViewModel>();

        private int _currentPage = 1;
        private string _pageInfo = "Loading...";
        private string _adHocJql;
        private bool _hasMorePages = false;

        public int CurrentPage
        {
            get { return _currentPage; }
            set
            {
                _currentPage = value;
                OnPropertyChanged();
                HandlePageChange();
            }
        }

        public string PageInfo
        {
            get { return _pageInfo; }
            set
            {
                if (value == _pageInfo) return;
                _pageInfo = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand MoveToPreviousPageCommand { get; }
        public RelayCommand MoveToNextPageCommand { get; }

        public PagedItemListViewModel(int pageSize, string jql, JiraHelper jiraApi, Action<string> onError)
        {
            _pageSize = pageSize;
            _jql = jql;
            _jiraApi = jiraApi;
            _onError = onError;

            MoveToNextPageCommand = new RelayCommand(_ => ChangePage(CurrentPage + 1), _ => _hasMorePages);
            MoveToPreviousPageCommand = new RelayCommand(_ => ChangePage(CurrentPage - 1), _ => CurrentPage > 1);
        }

        private void HandlePageChange()
        {
            MoveToPreviousPageCommand.RaiseCanExecuteChanged();
            MoveToNextPageCommand.RaiseCanExecuteChanged();

            var firstItem = (CurrentPage - 1) * _pageSize + 1;
            var lastItem = firstItem + Items.Count - 1;

            PageInfo = firstItem == lastItem ? $"Item {firstItem}" : $"Items {firstItem}-{lastItem}";
        }

        private async void ChangePage(int page)
        {
            try
            {
                await LoadPage(page);
            }
            catch (Exception e)
            {
                _onError("Error changing page: " + e.Message);
            }
        }

        public async Task<List<JiraIssueViewModel>> Refresh(string adHocJql)
        {
            _adHocJql = adHocJql;
            _cachedPages.Clear();
            return await LoadPage(1);
        }

        private async Task<List<JiraIssueViewModel>> LoadPage(int pageNumber)
        {
            JiraHelper.QueryResult cached;
            if (!_cachedPages.TryGetValue(pageNumber, out cached))
            {
                string nextPageToken = null;
                if (pageNumber != 1)
                {
                    nextPageToken = _cachedPages[pageNumber-1].NextPageToken;
                }
                var finalQuery = _adHocJql == null ? _jql : $"({_jql}) AND ({_adHocJql})";
                cached = await _jiraApi.GetIssues(finalQuery, _pageSize, "updated desc", nextPageToken);

                _cachedPages[pageNumber] = cached;
            }

            Items.Clear();
            foreach (var item in cached.Results)
            {
                Items.Add(item);
            }

            _hasMorePages = cached.NextPageToken != null;
            CurrentPage = pageNumber;

            return cached.Results;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}