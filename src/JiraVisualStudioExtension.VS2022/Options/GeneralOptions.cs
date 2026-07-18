using System.Runtime.InteropServices;
using System.Windows;
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

        protected override UIElement Child => _control ??= new CredentialOptionsControl();

        protected override void OnActivate(System.ComponentModel.CancelEventArgs e)
        {
            base.OnActivate(e);
            _control?.Initialize(UserName, ApiToken, Subdomain);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (e.ApplyBehavior == ApplyKind.Apply && _control != null)
            {
                UserName = _control.UserName;
                ApiToken = _control.ApiToken;
                Subdomain = _control.Subdomain;
            }
            base.OnApply(e);
        }

        public override void SaveSettingsToStorage()
        {
            VS2022Package.Options.JiraCredentials = (UserName, ApiToken, Subdomain);
        }

        public override void LoadSettingsFromStorage()
        {
            (UserName, ApiToken, Subdomain) = VS2022Package.Options.JiraCredentials;
        }
    }
}
