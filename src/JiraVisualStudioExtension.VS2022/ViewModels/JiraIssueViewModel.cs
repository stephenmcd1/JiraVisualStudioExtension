using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using JiraVisualStudioExtension.Properties;
using JiraVisualStudioExtension.Utilities;
using Microsoft.TeamFoundation.MVVM;
using Newtonsoft.Json.Linq;
using Brushes = System.Windows.Media.Brushes;

namespace JiraVisualStudioExtension.ViewModels
{
    public class JiraIssueViewModel : INotifyPropertyChanged
    {
        private string _basePath;


        public ICommand OpenItemCommand { get; }
        public ICommand OpenParentCommand { get; }

        public RelayCommand SaveChangesCommand { get; }
        public RelayCommand DiscardChangesCommand { get; }

        private JiraIssueViewModel(JToken originalApiObject, Action<JiraIssueViewModel> saveIssue)
        {
            _originalApiObject = originalApiObject;
            _saveIssue = saveIssue;
            OpenItemCommand = new RelayCommand(_ => Process.Start(DetailUrl));
            OpenParentCommand = new RelayCommand(_ => Process.Start(ParentDetailUrl));
            SaveChangesCommand = new RelayCommand(SaveChanges, _ => IsDirty);
            DiscardChangesCommand = new RelayCommand(DiscardChanges, _ => IsDirty);
        }


        private void DiscardChanges(object args)
        {
        }

        private void CheckDirty()
        {
            //Pretend like it's always dirty so we don't allow saving (which isn't coded)
            IsDirty = false;
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                if (value == _isDirty) return;
                _isDirty = value;
                OnPropertyChanged();
                SaveChangesCommand.RaiseCanExecuteChanged();
                DiscardChangesCommand.RaiseCanExecuteChanged();
            }
        }

        private void SaveChanges(object args)
        {
            _saveIssue(this);
        }

        private bool _selected;
        private string _status;
        private bool _isDirty;
        private readonly JToken _originalApiObject;
        private readonly Action<JiraIssueViewModel> _saveIssue;
        
        private JiraMetadata _metadata;

        public JiraIssueViewModel Clone()
        {
            return FromApi(_originalApiObject, _metadata, _saveIssue);
        }

        public static JiraIssueViewModel FromApi(JToken rawValue, JiraMetadata metadata, Action<JiraIssueViewModel> saveIssue)
        {
            dynamic obj = rawValue["fields"];

            var parent = obj.parent;
            var fixVersions = (JArray)obj.fixVersions;
            var sprints = metadata.SprintFieldName == null ? null : (JArray)obj[metadata.SprintFieldName];
            var t = new JiraIssueViewModel(rawValue, saveIssue)
            {
                Key = (string)rawValue["key"],
                Summary = obj.summary,
                Status = obj.status.name,
                StatusCategory = obj.status.statusCategory.name,
                StatusColor = obj.status.statusCategory.colorName,
                Parent = parent?.fields.summary,
                ParentKey = parent?.key,
                ParentType = parent?.fields.issuetype.name,
                FixVersions = fixVersions == null || fixVersions.Count == 0
                    ? "None"
                    : string.Join(", ", fixVersions.Select(o => o["description"])),
                Sprint = sprints == null || sprints.Count == 0
                    ? "None"
                    : string.Join(", ", sprints.Select(s => s["name"])),
                Assignee = obj.assignee?.displayName,
                _basePath = new Uri((string)rawValue["self"]).GetLeftPart(UriPartial.Authority) + "/",
                _metadata = metadata,
                IsDirty = false
            };

            return t;
        }

        public string Key { get; set; }
        public string Summary { get; set; }

        public string Status
        {
            get { return _status; }
            set
            {
                if (value == _status) return;
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBrush));
                CheckDirty();
            }
        }

        public string Sprint { get; set; }
        public string FixVersions { get; set; }
        public string Assignee { get; set; }
        public string Parent { get; set; }
        public string ParentKey { get; set; }
        public string ParentType { get; set; }

        public string StatusCategory { get; set; }
        public string StatusColor { get; set; }

        public string Project { get; set; }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (value == _selected) return;
                _selected = value;
                OnPropertyChanged();
            }
        }

        public Brush StatusBrush => StatusCategory == "Done" ? Brushes.Green : StatusCategory == "In Progress" ? Brushes.SkyBlue : Brushes.Gray;

        public string DetailUrl => GetDetailUrl(Key);
        public string ParentDetailUrl => GetDetailUrl(ParentKey);

        private string GetDetailUrl(string key) => $"{_basePath}browse/{key}";

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}