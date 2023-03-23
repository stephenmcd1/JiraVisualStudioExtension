using System;
using System.Reflection;
using System.Threading.Tasks;
using JiraVisualStudioExtension.UI;
using JiraVisualStudioExtension.Utilities;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.VersionControl.Controls.Extensibility;

namespace JiraVisualStudioExtension.TeamExplorer
{
    [TeamExplorerSection("dd330e16-f8ee-454e-b8f9-1580cbcd18e2", TeamExplorerPageIds.PendingChanges, 33)]
    public class JiraWorkItemSection : BaseTeamExplorerSection<SectionContent>
    {
        static JiraWorkItemSection()
        {
            //For some reason, Visual Studio can have trouble finding the Xaml Behaviors assembly.  By explicitly handling the
            //resolution of that assembly, we are able to get around that problem
            var myAssembly = typeof(Microsoft.Xaml.Behaviors.EventTrigger).Assembly.FullName;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                args.Name == myAssembly ? typeof(Microsoft.Xaml.Behaviors.EventTrigger).Assembly : null;
        }
        public OptionsHelper Options { get; private set; }

        protected override SectionContent CreateContent()
        {
            return new SectionContent(this);
        }

        protected override string BaseTitle { get; } = "Jira Issues";

        private Type _tfsViewModelType;
        private object _tfsViewModelInstance;
        private PropertyInfo _shelvesetNameProperty;

        protected override async Task InitializeAsync()
        {
            Options = new OptionsHelper(ServiceProvider);

            //Hook deep into the TFS Pending Changes page
            _tfsViewModelType = Type.GetType("Microsoft.TeamFoundation.VersionControl.Controls.PendingChanges.PendingChangesPageViewModel, Microsoft.TeamFoundation.VersionControl.Controls");
            _tfsViewModelInstance = ServiceProvider.GetService(_tfsViewModelType);
            
            _shelvesetNameProperty = _tfsViewModelType.GetProperty("ShelvesetName");
            if (_shelvesetNameProperty == null)
            {
                ShowNotification("Jira Extension Fatal error: Could not find Shelveset name property", NotificationType.Error);
            }
            
            OverwriteCheckinButton();
            WatchForCheckIns();
            OverwriteToggleShelvesetButton();

            await Content.ViewModel.Initialize();
        }

        /// <summary>
        /// Gets or Sets the current Shelveset Name from the Pending Changes panel
        /// </summary>
        public string ShelvesetName
        {
            get => (string)_shelvesetNameProperty.GetValue(_tfsViewModelInstance);
            set => _shelvesetNameProperty.SetValue(_tfsViewModelInstance, value);
        }

        protected override async Task RefreshAsync()
        {
            await Content.ViewModel.Refresh();
        }

        private void OverwriteToggleShelvesetButton()
        {
            //Hook into the command that toggles the shelveset option so we can let the view model know after it happens
            OverwriteRelayCommand("ToggleShelveOptionsCommand", (existingCommand, o) =>
            {
                existingCommand.Execute(o);
                Content.ViewModel.OnToggleShelveset();
            });
        }

        private void OverwriteCheckinButton()
        {
            OverwriteRelayCommand("CheckInCommand", (existingCommand, o) =>
            {
                //Remember the original comment so we can easily restore it if something goes wrong
                var origComment = CheckinComment;

                try
                {
                    //Defer to the view model to do any pre-checkin logic - including deciding whether we should continue
                    bool shouldContinue;
                    Content.ViewModel.OnBeforeCheckIn(out shouldContinue);

                    //If we are done, restore the checkin comment and be done
                    if (!shouldContinue)
                    {
                        CheckinComment = origComment;
                        return;
                    }

                    //Start the checkin - that is, what normally happens when you click the Check In button
                    existingCommand.Execute(o);

                    //If the command is immediately enabled, something basic must have gone wrong (or the user cancelled) so restore the comment
                    if (existingCommand.CanExecute(null))
                    {
                        CheckinComment = origComment;
                    }
                }
                catch (Exception)
                {
                    //If anything goes wrong executing the command, restore the comment.  Note, most ways that Check In can fail will not be throwing an
                    //   exception but might as well be careful
                    CheckinComment = origComment;
                }
            });
        }

        /// <summary>
        /// Helper method to replace/extend an existing command
        /// </summary>
        private void OverwriteRelayCommand(string propertyName, Action<RelayCommand, object> newAction)
        {
            var commandProp = _tfsViewModelType.GetProperty(propertyName);
            if (commandProp == null)
            {
                ShowNotification($"Jira Extension Fatal error: Could not find {propertyName} command", NotificationType.Error);
                return;
            }

            var existingCommand = (RelayCommand)commandProp.GetValue(_tfsViewModelInstance);
            var newCommand = new RelayCommand(o =>
            {
                newAction(existingCommand, o);
            }, o => existingCommand.CanExecute(o));
            commandProp.SetValue(_tfsViewModelInstance, newCommand);
        }

        private void WatchForCheckIns()
        {
            //We want to know when the Check In process finishes.  Since it is an async process, we can't just wait for the command to finish.  We'll
            //  hook into an event that is raised when the checkin completes that conveniently provides info about how the process went
            var pc = GetService<IPendingChangesExt>();
            var checkinCompleteEvent = _tfsViewModelType.GetEvent("CheckinCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
            var lastChangesetProp = _tfsViewModelType.GetProperty("LastCheckinChangesetId");
            if (checkinCompleteEvent == null || lastChangesetProp == null)
            {
                ShowNotification("Jira Extension Fatal error: Could not find checkin-related event/properties", NotificationType.Error);
                return;
            }

            EventHandler checkinComplete = (s, e) =>
            {
                var lastId = (int) lastChangesetProp.GetValue(_tfsViewModelInstance);
                //If this property has a positive number, it means the Check In completely successfully - hand over to the View Model to do it's processing
                if (lastId > 0)
                {
                    Content.ViewModel.OnAfterCheckin(lastId, pc.Workspace);
                }
                else
                {
                    //Otherwise, something went wrong so just revert the comment
                    CheckinComment = Content.ViewModel.RemoveIssuePrefix(CheckinComment);
                }
            };

            checkinCompleteEvent.GetAddMethod(true).Invoke(_tfsViewModelInstance, new object[] {checkinComplete});
            AddDisposeAction(() => checkinCompleteEvent.GetRemoveMethod(true).Invoke(_tfsViewModelInstance, new object[] {checkinComplete}));
        }

        public string CheckinComment
        {
            get
            {
                //Getting the current value is easy as it is exposed publicly
                return GetService<IPendingChangesExt>().CheckinComment;
            }
            set
            {
                //Don't bother doing anything if the value is already what we want
                if (CheckinComment == value)
                {
                    return;
                }

                //Setting is more complicated.  We have to get at the underlying model and set the value through it
                var pc = GetService<IPendingChangesExt>();
                var modelField = pc.GetType().GetField("m_pendingChangesModel", BindingFlags.Instance | BindingFlags.NonPublic);
                var model = modelField?.GetValue(pc);
                if (model == null)
                {
                    return;
                }

                var commentProp = model.GetType().GetProperty("CheckinComment", BindingFlags.Instance | BindingFlags.NonPublic);
                if (commentProp == null)
                {
                    ShowNotification("Jira Extension Error: Could not set Check In Comment", NotificationType.Error);
                }
                else
                {
                    commentProp.SetValue(model, value);
                }
            }
        }
    }
}