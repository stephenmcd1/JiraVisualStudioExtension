using JiraVisualStudioExtension.TeamExplorer;
using JiraVisualStudioExtension.Utilities;
using JiraVisualStudioExtension.ViewModels;
using System.Windows.Controls;

namespace JiraVisualStudioExtension.UI
{
    /// <summary>
    /// Interaction logic for SectionContent.xaml
    /// </summary>
    public partial class SectionContent
    {
        public SectionContent(JiraWorkItemSection parentSection)
        {
            DataContext = new SectionContentViewModel(parentSection);

            InitializeComponent();
        }

        public SectionContentViewModel ViewModel => ((SectionContentViewModel) DataContext);
    }
}