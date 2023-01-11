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
    [TeamExplorerSection("d7526377-03bd-4ee0-97ff-4e75e83cc947", TeamExplorerPageIds.PendingChanges, 34)]
    public class JiraWorkItemSection : BaseTeamExplorerSection<SectionContent>
    {
        public OptionsHelper Options { get; private set; }

        protected override SectionContent CreateContent()
        {
            return new SectionContent(this);
        }

        protected override string BaseTitle { get; } = "Jira Issues";

        protected override async Task InitializeAsync()
        {
            Options = new OptionsHelper(ServiceProvider);

            //Hook deep into the TFS Pending Changes page
            var type = Type.GetType("Microsoft.TeamFoundation.VersionControl.Controls.PendingChanges.PendingChangesPageViewModel, Microsoft.TeamFoundation.VersionControl.Controls");
            var pageModel = ServiceProvider.GetService(type);

            OverwriteCheckinButton(type, pageModel);
            WatchForCheckIns(type, pageModel);

            await Content.ViewModel.Initialize();
        }

        protected override async Task RefreshAsync()
        {
            await Content.ViewModel.Refresh();
        }

        private void OverwriteCheckinButton(Type viewModelType, object pageViewModel)
        {
            //Find the CheckInCommand that powers the Check In button - we want to replace / wrap it with our custom behavior
            var checkinCommandProp = viewModelType.GetProperty("CheckInCommand");
            if (checkinCommandProp == null)
            {
                ShowNotification("Jira Extension Fatal error: Could not find check-in command", NotificationType.Error);
                return;
            }

            var existingCommand = (RelayCommand) checkinCommandProp.GetValue(pageViewModel);
            var newCommand = new RelayCommand(o =>
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
            }, o => existingCommand.CanExecute(o));
            checkinCommandProp.SetValue(pageViewModel, newCommand);
        }

        private void WatchForCheckIns(Type viewModelType, object pageViewModel)
        {
            //We want to know when the Check In process finishes.  Since it is an async process, we can't just wait for the command to finish.  We'll
            //  hook into an event that is raised when the checkin completes that conveniently provides info about how the process went
            var pc = GetService<IPendingChangesExt>();
            var checkinCompleteEvent = viewModelType.GetEvent("CheckinCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
            var lastChangesetProp = viewModelType.GetProperty("LastCheckinChangesetId");
            if (checkinCompleteEvent == null || lastChangesetProp == null)
            {
                ShowNotification("Jira Extension Fatal error: Could not find checkin-related event/properties", NotificationType.Error);
                return;
            }

            EventHandler checkinComplete = (s, e) =>
            {
                var lastId = (int) lastChangesetProp.GetValue(pageViewModel);
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

            checkinCompleteEvent.GetAddMethod(true).Invoke(pageViewModel, new object[] {checkinComplete});
            AddDisposeAction(() => checkinCompleteEvent.GetRemoveMethod(true).Invoke(pageViewModel, new object[] {checkinComplete}));
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