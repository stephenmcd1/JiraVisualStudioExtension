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
using Newtonsoft.Json.Linq;

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
            if (VS2022Package.UseFakeData)
            {
                _cachedPages[pageNumber] = GenerateFakeIssues(pageNumber);
            }
            if (!_cachedPages.TryGetValue(pageNumber, out var cached))
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

        private JiraHelper.QueryResult GenerateFakeIssues(int pageNumber)
        {
            var fakeIssues = new List<(string status, string statusCategory, string colorName, string summary)>
            {
                ("To Do", "To Do", "blue-gray", "Coffee machine is plotting world domination"),
                ("In Progress", "In Progress", "yellow", "Teach rubber duck to actually debug"),
                ("Done", "Done", "green", "Remove all bugs by deleting code"),
                ("To Do", "To Do", "blue-gray", "Implement 'it works on my machine' as a service"),
                ("In Progress", "In Progress", "yellow", "Convert all errors to warnings (they're just suggestions)"),
                ("Code Review", "In Progress", "yellow", "Replace entire codebase with AI"),
                ("Done", "Done", "green", "Successfully procrastinated for 3 sprints"),
                ("To Do", "To Do", "blue-gray", "Fix bug that only appears when demo-ing to client"),
                ("In Progress", "In Progress", "yellow", "Optimize algorithm by hoping really hard"),
                ("Testing", "In Progress", "yellow", "Test if QA is paying attention"),
                ("Done", "Done", "green", "Rename variables to be more confusing"),
                ("Blocked", "To Do", "blue-gray", "Waiting for Stack Overflow to answer my question"),
                ("To Do", "To Do", "blue-gray", "Make loading spinner more hypnotic"),
                ("In Progress", "In Progress", "yellow", "Debug why debugger won't debug"),
                ("Done", "Done", "green", "Successfully convinced PM feature is working as intended")
            };

            var startIndex = (pageNumber - 1) * _pageSize;
            var results = new List<JiraIssueViewModel>();

            var rng = new Random();
            for (int i = 0; i < _pageSize && startIndex + i < fakeIssues.Count; i++)
            {
                var (status, statusCategory, colorName, summary) = fakeIssues[startIndex + i];
                var issueNumber = rng.Next(1000, 9999);

                var fakeJson = JObject.Parse($@"{{
                    ""key"": ""DEV-{issueNumber}"",
                    ""self"": ""https://fakecompany.atlassian.net/rest/api/3/issue/{issueNumber}"",
                    ""fields"": {{
                        ""summary"": ""{summary}"",
                        ""status"": {{
                            ""name"": ""{status}"",
                            ""statusCategory"": {{
                                ""name"": ""{statusCategory}"",
                                ""colorName"": ""{colorName}""
                            }}
                        }},
                        ""assignee"": {{
                            ""displayName"": ""Demo User""
                        }},
                        ""sprint"": [{{""name"": ""App-Sprint-93""}}],
                        ""fixVersions"": [{{""description"": ""Q3 2027""}}],
                        ""parent"": null
                    }}
                }}");

                fakeJson["fields"]["parent"] = JObject.FromObject(new
                    { key = "APP-456", fields = new { summary = "ss", issuetype = new { name = "Epic" } } });

                var fakeMetadata = new JiraMetadata { SprintFieldName = "sprint" };
                results.Add(JiraIssueViewModel.FromApi(fakeJson, fakeMetadata, _ => { }));
            }

            return new JiraHelper.QueryResult
            {
                Results = results,
                NextPageToken = startIndex + _pageSize < fakeIssues.Count ? $"page-{pageNumber + 1}" : null
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}