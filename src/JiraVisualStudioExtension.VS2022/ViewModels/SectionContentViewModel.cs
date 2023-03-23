using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using JiraVisualStudioExtension.Properties;
using JiraVisualStudioExtension.TeamExplorer;
using JiraVisualStudioExtension.Utilities;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.VersionControl.Client;
using Newtonsoft.Json;

namespace JiraVisualStudioExtension.ViewModels
{
    [SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates")]
    public class SectionContentViewModel : INotifyPropertyChanged
    {
        public class IssueTypeViewModel : INotifyPropertyChanged
        {
            public JiraMetadata.IssueType IssueType { get; set; }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (value == _isChecked) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
            private bool _isChecked;

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private readonly JiraWorkItemSection _parentSection;
        private JiraHelper _jiraApi;

        public SectionContentViewModel(JiraWorkItemSection parentSection)
        {
            _parentSection = parentSection;

            //Setup the commands
            LogOnCommand = new RelayCommand(async _ =>
            {
                try
                {
                    //Defer to the helper method and then see how it went
                    var res = await TryLogOn();
                    switch (res)
                    {
                        case true:
                            //On log on, save the values we used for next time and clear out any errors that may have been there from the logon process
                            _parentSection.Options.SetStringOption("UserName", UserNameEntry);
                            _parentSection.Options.SetEncryptedOption("Password", PasswordEntry);
                            _parentSection.Options.SetEncryptedOption("Subdomain", SubdomainEntry);
                            IsErrorActive = false;
                            await Refresh();
                            break;
                        case false:
                            //On failure, show an error
                            ShowError("Could not connect to Jira.  Check credentials.");
                            break;
                        case null:
                            //Otherwise, not enough info to log in - show a different error
                            ShowError("Email and Password required");
                            break;
                    }
                }
                catch (Exception e)
                {
                    ShowError("Unexpected error logging on: " + e.Message);
                }
            }, _ => true);

            LogOutCommand = new RelayCommand(arg => IsLoggedOn = false, _ => true);

            SearchByKeyCommand = new RelayCommand(val =>
            {
                //There are a few ways this command may be called so look around to find the bool parameter
                IsSearchingByKey = Convert.ToBoolean(val as string ?? ((DropDownLink.DropDownMenuCommandParameters) val).Parameter);
                SearchByKeyCommand.RaiseCanExecuteChanged();
            }, val => IsLoggedOn && (val == null || IsSearchingByKey != Convert.ToBoolean(val)));

            AddByKeyCommand = new RelayCommand(async arg =>
            {
                //Make sure the entered some text
                var text = arg as string;
                if (string.IsNullOrWhiteSpace(text))
                {
                    ShowError("Please enter the Issue Key");
                    return;
                }

                try
                {
                    //Defer to the helper method and show a message if the item isn't found
                    var res = await AddByKey(text);
                    if (!res)
                    {
                        ShowError("Could not find Issue with Key: " + text);
                    }
                }
                catch (Exception e)
                {
                    ShowError("Fatal error searching by Key: " + e.Message);
                }
            });

            DismissErrorsCommand = new RelayCommand(_ => IsErrorActive = false);

            ClearCurrentItemCommand = new RelayCommand(_ => CurrentItem = null, _ => CurrentItem != null);

            SelectItemCommand = new RelayCommand(parm =>
            {
                var args = parm as ItemDoubleClickEventArgs;
                var model = args?.SelectedItem as JiraIssueViewModel;
                if (model != null)
                {
                    CurrentItem = model.Clone();
                }
            });

            ToggleFilterCommand = new RelayCommand(() => IsFilterActive = !IsFilterActive);

            RefreshCommand = new RelayCommand(() => HandleAsyncErrors(Refresh, "Error when executing refresh command"));

            ApplyIssueTypesCommand = new RelayCommand(() => HandleAsyncErrors(async () =>
            {
                var selectedTypes = IssueTypes.Where(i => i.IsChecked).Select(i => i.IssueType.Name).ToList();
                _parentSection.Options.SetMultiStringOption("SelectedIssueTypes-" + SubdomainEntry, selectedTypes);
                IssueTypeText = selectedTypes.Count == 0
                    ? "Issue Types (all)"
                    : "Issue Types (" + selectedTypes.Count + ")";
                await Refresh();

            }, "Updating Issue Type Filters"));

            //Setup some initial variables - especially to trigger the property change logic to get everything in a good state
            Initializing = true;
            IsLoggedOn = false;
            CurrentItem = null;
        }

        private async Task<bool> AddByKey(string id)
        {
            var issues = await _jiraApi.GetIssues($"Key IN ({id})",  1);

            if (issues.Results.Count == 0)
            {
                return false;
            }

            IsSearchingByKey = false;
            SearchByKeyCommand.RaiseCanExecuteChanged();
            CurrentItem = issues.Results[0];
            if (CurrentItem.Assignee != CurrentUserDisplayName)
            {
                ShowError("Warning: You are not the Assignee of the current Issue");
            }

            return true;
        }

        private async Task<bool?> TryLogOn()
        {
            if (string.IsNullOrWhiteSpace(UserNameEntry) || string.IsNullOrWhiteSpace(PasswordEntry) || string.IsNullOrWhiteSpace(SubdomainEntry))
            {
                return null;
            }

            var res = await Task.Run(() => _jiraApi.TryLogOn(UserNameEntry, PasswordEntry, SubdomainEntry));
            if (res == null)
            {
                return false;
            }

            CurrentUserDisplayName = res;

            CurrentList = new PagedItemListViewModel(5, "Assignee IN (currentUser())", _jiraApi, ShowError);

            var selectedIssueTypes = _parentSection.Options.GetMultiStringOption("SelectedIssueTypes-" + SubdomainEntry);
            if (selectedIssueTypes == null)
            {
                selectedIssueTypes = new[] { "Sub-task" };
            }
            IssueTypes.Reset(_jiraApi.Metadata.IssueTypes.Select(i => new IssueTypeViewModel{IssueType = i, IsChecked = selectedIssueTypes.Contains(i.Name)}));
            var selectedCount = IssueTypes.Count(i => i.IsChecked);
            IssueTypeText = selectedCount == 0
                ? "Issue Types (all)"
                : "Issue Types (" + selectedCount + ")";

            IsLoggedOn = true;

            return true;
        }

        private string BuildAdHocFilter()
        {
            var filters = new List<string>();
            

            var selectedIssueTypes = IssueTypes.Where(i => i.IsChecked).Select(i => i.IssueType.Name).ToList();
            if (selectedIssueTypes.Any())
            {
                filters.Add("IssueType IN (\"" + string.Join("\",\"", selectedIssueTypes) + "\")");
            }

            if (IsFilterActive)
            {
                if (ExcludeDoneIssues)
                {
                    filters.Add("StatusCategory != Done");
                }

                if (!string.IsNullOrWhiteSpace(SummaryFilter))
                {
                    filters.Add($"Summary ~ \"{SummaryFilter.Replace("\"", "\\\"")}\"");
                }
            }

            return filters.Count == 0 ? null : $"({string.Join(") AND (", filters)})";
        }

        public async Task Refresh()
        {
            _parentSection.IsBusy = true;

            if (!_jiraApi.IsLoggedOn)
            {
                _parentSection.IsBusy = false;
                return;
            }

            var items = await CurrentList.Refresh(BuildAdHocFilter());

            var updatedCurrent = items.SingleOrDefault(item => item.Key == CurrentItem?.Key);
            if (CurrentItem != null && updatedCurrent == null)
            {
                if (!await AddByKey(CurrentItem.Key))
                {
                    ShowError("Previously selected Issue (" + CurrentItem.Key + ") could not be found");
                    CurrentItem = null;
                }
            }

            _parentSection.IsBusy = false;
        }

        public async Task Initialize()
        {
            UserNameEntry = _parentSection.Options.GetStringOption("UserName");
            PasswordEntry = _parentSection.Options.GetEncryptedOption("Password");
            SubdomainEntry = _parentSection.Options.GetEncryptedOption("Subdomain");

            _jiraApi = new JiraHelper(ShowError, updatedIssue =>
            {
                CurrentItem = updatedIssue;
                HandleAsyncErrors(Refresh, "Error refreshing after save");
            });

            var res = await TryLogOn();
            if (res == false)
            {
                ShowError("Could not connect to Jira with saved credentials");
            }

            await Refresh();

            Initializing = false;
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            IsErrorActive = true;
        }

        private async void HandleAsyncErrors(Func<Task> t, string prefix)
        {
            try
            {
                await t();
            }
            catch (Exception e)
            {
                ShowError(prefix + ": " + e.Message);
            }
        }


        #region Properties

        private string _passwordEntry;
        private string _subdomainEntry;
        private bool _isLoggedOn;
        private string _currentUserDisplayName;
        private bool _initializing;
        private string _userNameEntry;
        private bool _isSearchingByKey;
        private bool _isFilterActive;
        private bool _excludeDoneIssues;
        private string _summaryFilter;
        private JiraIssueViewModel _currentItem;
        private bool _isErrorActive;
        private string _errorMessage;
        private PagedItemListViewModel _currentList;
        private string _issueTypeText;

        public BatchedObservableCollection<IssueTypeViewModel> IssueTypes { get; } =
            new BatchedObservableCollection<IssueTypeViewModel>();

        public RelayCommand LogOnCommand { get; set; }
        public RelayCommand LogOutCommand { get; set; }
        public RelayCommand SearchByKeyCommand { get; set; }
        public RelayCommand AddByKeyCommand { get; set; }
        public RelayCommand DismissErrorsCommand { get; set; }
        public RelayCommand ClearCurrentItemCommand { get; set; }
        public RelayCommand SelectItemCommand { get; set; }
        public RelayCommand ToggleFilterCommand { get; set; }

        public RelayCommand RefreshCommand { get; set; }

        public RelayCommand ApplyIssueTypesCommand { get; set; }

        public PagedItemListViewModel CurrentList
        {
            get { return _currentList; }
            set
            {
                if (value == _currentList) return;
                _currentList = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoggedOn
        {
            get { return _isLoggedOn; }
            set
            {
                if (value == _isLoggedOn) return;
                _isLoggedOn = value;
                OnPropertyChanged();
                SearchByKeyCommand.RaiseCanExecuteChanged();
                if (!value)
                {
                    IsSearchingByKey = false;
                    CurrentUserDisplayName = "Not Logged On";
                }
            }
        }

        public string CurrentUserDisplayName
        {
            get { return _currentUserDisplayName; }
            set
            {
                if (value == _currentUserDisplayName) return;
                _currentUserDisplayName = value;
                OnPropertyChanged();
            }
        }

        public bool Initializing
        {
            get { return _initializing; }
            set
            {
                if (value == _initializing) return;
                _initializing = value;
                OnPropertyChanged();
            }
        }

        public string UserNameEntry
        {
            get { return _userNameEntry; }
            set
            {
                if (value == _userNameEntry) return;
                _userNameEntry = value;
                OnPropertyChanged();
            }
        }


        public bool IsSearchingByKey
        {
            get { return _isSearchingByKey; }
            set
            {
                if (value == _isSearchingByKey) return;
                _isSearchingByKey = value;
                OnPropertyChanged();
            }
        }

        public bool IsFilterActive
        {
            get { return _isFilterActive; }
            set
            {
                if (value == _isFilterActive) return;
                _isFilterActive = value;
                OnPropertyChanged();
                HandleAsyncErrors(Refresh, "Error refreshing as part of filter change");
            }
        }

        public bool ExcludeDoneIssues
        {
            get { return _excludeDoneIssues; }
            set
            {
                if (value == _excludeDoneIssues) return;
                _excludeDoneIssues = value;
                OnPropertyChanged();
                HandleAsyncErrors(Refresh, "Error refreshing as part of filter change");
            }
        }

        public string SummaryFilter
        {
            get { return _summaryFilter; }
            set
            {
                if (value == _summaryFilter) return;
                _summaryFilter = value;
                OnPropertyChanged();
            }
        }

        public string PasswordEntry
        {
            get { return _passwordEntry; }
            set
            {
                if (value == _passwordEntry) return;
                _passwordEntry = value;
                OnPropertyChanged();
            }
        }
        
        public string SubdomainEntry
        {
            get { return _subdomainEntry; }
            set
            {
                if (value == _subdomainEntry) return;
                _subdomainEntry = value;
                OnPropertyChanged();
            }
        }


        public JiraIssueViewModel CurrentItem
        {
            get { return _currentItem; }
            set
            {
                if (Equals(value, _currentItem)) return;
                _currentItem = value;
                OnPropertyChanged();
                ClearCurrentItemCommand.RaiseCanExecuteChanged();
                _parentSection.SetAdditionalExpandedTitle(value == null ? "Nothing Selected" : value.Key + ": " + value.Summary);
                foreach (var i in CurrentList.Items)
                {
                    i.Selected = i.Key == CurrentItem?.Key;
                }

                //If the current shelvset name is the same as we set last time (meaning the user didn't enter their own name)
                //  then we will update it for the new item
                if (_parentSection.ShelvesetName == _lastShelvesetName)
                {
                    _lastShelvesetName = CurrentItem.Key + ": " + CurrentItem.Summary;
                    _parentSection.ShelvesetName = _lastShelvesetName;
                }
            }
        }

        public bool IsErrorActive
        {
            get { return _isErrorActive; }
            set
            {
                if (value == _isErrorActive) return;
                _isErrorActive = value;
                OnPropertyChanged();
            }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set
            {
                if (value == _errorMessage) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string IssueTypeText
        {
            get { return _issueTypeText; }
            set
            {
                if(value == _issueTypeText) return;
                _issueTypeText = value;
                OnPropertyChanged();
            }
        }

        #endregion


        private string _lastShelvesetName;
        public void OnToggleShelveset()
        {
            //When the shelveset area gets toggled, provide a default name based on the current item (if there is one)
            if (CurrentItem == null)
            {
                _lastShelvesetName = null;
                return;
            }

            _lastShelvesetName = CurrentItem.Key + ": " + CurrentItem.Summary;
            _parentSection.ShelvesetName = _lastShelvesetName;
        }

        public void OnBeforeCheckIn(out bool shouldContinue)
        {
            if (string.IsNullOrWhiteSpace(_parentSection.CheckinComment))
            {
                UIHost.ShowMessageBox("You must enter a check-in comment before you can checkin these changes", null, "Check-in Comment Required", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                shouldContinue = false;
            }
            else if (CurrentItem == null)
            {
                var res = UIHost.ShowMessageBox(
                    "This checkin is not associated with a Jira Issue.  Nearly all TFS check-ins should be associated with a Jira Issue.  Are you sure you want to continue?", null,
                    "No Jira Issue Selected", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                shouldContinue = (res == DialogResult.Yes);
            }
            else
            {
                var separator = _parentSection.CheckinComment != null && _parentSection.CheckinComment.Contains("\r\n")
                    ? "\r\n"
                    : " ";
                _parentSection.CheckinComment = $"{CurrentItem.ParentKey}-{CurrentItem.Key}:{separator}{_parentSection.CheckinComment}";
                shouldContinue = true;
            }
        }

        public string RemoveIssuePrefix(string comment)
        {
            if (CurrentItem == null || comment == null)
            {
                return comment;
            }

            var prefix = $"^{CurrentItem.ParentKey}-{CurrentItem.Key}:( |\r\n)";
            return Regex.Replace(comment, prefix, "");
        }

        public async void OnAfterCheckin(int changesetId, Workspace workspace)
        {
            if (CurrentItem == null)
            {
                return;
            }

            if (AddComment(changesetId, workspace))
            {
                var itemCopy = CurrentItem;
                _parentSection.ShowNotification($"Jira Comment added to [{CurrentItem.Key}](Click to open Issue in Jira)", NotificationType.Information,
                    new RelayCommand(_ => itemCopy.OpenItemCommand.Execute(null)));
            }

            try
            {
                await CurrentList.Refresh(BuildAdHocFilter());
            }
            catch (Exception e)
            {
                ShowError("Error refreshing Issues: " + e.Message);
            }

            CurrentItem = null;
        }

        private bool AddComment(int changesetId, Workspace workspace)
        {
            var collectionGuid = workspace.VersionControlServer.TeamProjectCollection.InstanceId;
            var cs = workspace.VersionControlServer.GetChangeset(changesetId, true, true);
            var changesetComment = RemoveIssuePrefix(cs.Comment);
            
            string ownerName = cs.CommitterDisplayName;

            ChangeType CleanChangeType(ChangeType e)
            {
                if (e == ChangeType.Encoding)
                    return e;
                e &= ~ChangeType.Encoding;
                if (e.HasFlag(ChangeType.Add))
                {
                    e &= ~ChangeType.Edit;
                }

                return e;
            }

            var changes = cs.Changes.Select(c =>
            {
                var lastSlashPos = c.Item.ServerItem.LastIndexOf("/", StringComparison.Ordinal) + 1;
                var fileName = c.Item.ServerItem.Substring(lastSlashPos);
                var folder = c.Item.ServerItem.Substring(0, lastSlashPos);


                return new
                {
                    ChangeType = CleanChangeType(c.ChangeType).ToString(),
                    FileName = fileName,
                    Folder = folder
                };
            }).ToList();

            var contents = new List<object>();

            var changesetUrl = $"https://vsrssinttools01/DevTools/cs.axd?cs={changesetId}&pcguid={collectionGuid:D}";

            contents.Add(new
            {
                type = "paragraph",
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Changeset Number: ",
                        marks = new[]{new {type = "strong"}}
                    },
                    new
                    {
                        type = "text",
                        text = changesetId.ToString(),
                        marks = new[]{new {type = "link", attrs = new{href = changesetUrl}}}
                    },
                    new{type = "hardBreak"},
                    new
                    {
                        type = "text",
                        text = "Changeset Owner: ",
                        marks = new[]{new {type = "strong"}}
                    },
                    new
                    {
                        type = "text",
                        text = ownerName
                    },
                    new{type = "hardBreak"},
                    new
                    {
                        type = "text",
                        text = "Comment: ",
                        marks = new[]{new {type = "strong"}}
                    },
                }
            });

            var comment = string.IsNullOrWhiteSpace(changesetComment)
                ? "No comment provided"
                : changesetComment.TrimTo(1000, "...");

            contents.Add(new
            {
                type = "panel",
                attrs = new { panelType = "info" },
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[]
                        {
                            new {type = "text", text = comment}
                        }
                    }
                }
            });

            var prefix = changes.Select(c => c.Folder).ToList().LongestCommonPrefix(caseSensitive: false);
            prefix = prefix.Substring(0, prefix.LastIndexOf("/") + 1);

            contents.Add(new
            {
                type = "paragraph",
                content = new object[]
                {
                    new {type = "text", text = "Files Changed", marks = new[] {new {type = "strong"}}},
                    new {type = "text", text = $" (Under {prefix})", marks = new[] {new {type = "textColor", attrs = new {color = "#97a0af"}}}}
                }
            });


            const int maxChanges = 50;

            if (changes.Count <= maxChanges)
            {
                var listItems = new List<object>();

                foreach (var change in changes)
                {
                    var folder = "./" + change.Folder.Substring(prefix.Length);
                    listItems.Add(new
                    {
                        type = "listItem",
                        content = new[]
                        {
                            new
                            {
                                type = "paragraph", content = new object[]
                                {
                                    new {type = "text", text = folder},
                                    new {type = "text", text = change.FileName,marks = new[] {new {type = "strong"}}},
                                    new {type = "text", text = $" ({change.ChangeType})", marks = new[] {new {type = "textColor", attrs = new {color = "#97a0af"}}}}
                                }
                            }
                        }
                    });
                }


                contents.Add(new { type = "bulletList", content = listItems });
            }
            else
            {
                contents.Add(new
                {
                    type = "panel",
                    attrs = new { panelType = "warning" },
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new object[]
                            {
                                new {type = "text", text = $"{changes.Count:N0} files", marks = new[]{new {type = "strong"},}},
                                new {type = "text", text = " changed.  View full Changeset information "},
                                new {type = "text", text = "here", marks = new object[] {new {type = "strong"}, new {type = "link", attrs = new{href = changesetUrl}}}},
                                new {type = "text", text = "."}
                            }
                        }
                    }
                });

            }

            var doc = new
            {
                version = 1,
                type = "doc",
                content = contents
            };

            var json = JsonConvert.SerializeObject(new { body = doc }, Formatting.Indented);
            
            return _jiraApi.AddCommentToIssue(CurrentItem, json);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}