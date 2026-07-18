using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace JiraVisualStudioExtension.Options
{
    [Guid("cf813765-16c9-4587-bcb6-726808d564d2")]
    [ComVisible(true)]
    public class LinkDefinitionOptions : UIElementDialogPage
    {
        private List<IssueReferences.LinkDefinition> _linkDefinitions = new();
        private LinkDefinitionOptionsControl _control;

        public List<IssueReferences.LinkDefinition> LinkDefinitions
        {
            get => _linkDefinitions;
            set => _linkDefinitions = value ?? new List<IssueReferences.LinkDefinition>();
        }

        protected override UIElement Child => _control ??= new LinkDefinitionOptionsControl();

        protected override void OnActivate(System.ComponentModel.CancelEventArgs e)
        {
            base.OnActivate(e);
            _control?.Initialize(LinkDefinitions);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (e.ApplyBehavior == ApplyKind.Apply && _control != null)
            {
                LinkDefinitions = _control.GetLinkDefinitions();
            }
            base.OnApply(e);
        }

        public override void SaveSettingsToStorage()
        {
            var json = JsonConvert.SerializeObject(LinkDefinitions);
            using var root = VSRegistry.RegistryRoot(ServiceProvider.GlobalProvider, __VsLocalRegistryType.RegType_UserSettings, true);
            using var key = root.CreateSubKey("JiraVSExtension");
            key?.SetValue("LinkDefinitions", json, RegistryValueKind.String);
        }

        public override void LoadSettingsFromStorage()
        {
            try
            {
                using var root = VSRegistry.RegistryRoot(ServiceProvider.GlobalProvider, __VsLocalRegistryType.RegType_UserSettings, true);
                using var key = root.CreateSubKey("JiraVSExtension");
                var json = key?.GetValue("LinkDefinitions") as string;
                if (!string.IsNullOrEmpty(json))
                {
                    _linkDefinitions = JsonConvert.DeserializeObject<List<IssueReferences.LinkDefinition>>(json)
                                       ?? new List<IssueReferences.LinkDefinition>();
                }
            }
            catch
            {
                _linkDefinitions = new List<IssueReferences.LinkDefinition>();
            }
        }

        public static LinkDefinitionOptions GetInstance()
        {
            return (LinkDefinitionOptions)VS2022Package.Instance?.GetDialogPage(typeof(LinkDefinitionOptions));
        }
    }
}
