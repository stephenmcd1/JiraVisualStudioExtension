using System.Runtime.InteropServices;
using System.Windows;
using JiraVisualStudioExtension.Utilities;
using Microsoft.VisualStudio.Shell;

namespace JiraVisualStudioExtension.Options
{
    [Guid("b7e3a1f0-2d4c-4e6a-9f8b-1c3d5e7f9a0b")]
    [ComVisible(true)]
    public class GeneralOptions : UIElementDialogPage
    {
        private CredentialOptionsControl _control;

        public string UserName { get; set; } = "";
        public string ApiToken { get; set; } = "";
        public string Subdomain { get; set; } = "";
        public string ChangesetUrl { get; set; } = Utilities.ChangesetUrl.Default;

        protected override UIElement Child
        {
            get
            {
                if (_control == null)
                {
                    _control = new CredentialOptionsControl();
                    _control.Initialize(UserName, ApiToken, Subdomain, ChangesetUrl);
                }

                return _control;
            }
        }

        protected override void OnActivate(System.ComponentModel.CancelEventArgs e)
        {
            base.OnActivate(e);
            _control?.Initialize(UserName, ApiToken, Subdomain, ChangesetUrl);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (e.ApplyBehavior == ApplyKind.Apply && _control != null)
            {
                UserName = _control.UserName;
                ApiToken = _control.ApiToken;
                Subdomain = _control.Subdomain;
                ChangesetUrl = _control.ChangesetUrl;
            }
            base.OnApply(e);
        }

        public override void SaveSettingsToStorage()
        {
            VS2022Package.Options.JiraCredentials = (UserName, ApiToken, Subdomain);
            VS2022Package.Options.ChangesetUrl = ChangesetUrl;
        }

        public override void LoadSettingsFromStorage()
        {
            (UserName, ApiToken, Subdomain) = VS2022Package.Options.JiraCredentials;
            ChangesetUrl = VS2022Package.Options.ChangesetUrl;
        }
    }
}
