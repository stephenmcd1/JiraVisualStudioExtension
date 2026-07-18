using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using JiraVisualStudioExtension.Options;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace JiraVisualStudioExtension.IssueReferences.Editor
{
    [Export(typeof(ITaggerProvider))]
    [ContentType(StandardContentTypeNames.Text)]
    [ContentType(StandardContentTypeNames.Projection)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    [TagType(typeof(LinkTag))]
    internal sealed class LinkTaggerProvider : ITaggerProvider
    {
        [Import]
        internal IClassifierAggregatorService AggregatorService;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
            where T : ITag
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            VS2022Package.EnsureLoaded();

            var options = LinkDefinitionOptions.GetInstance();
            var linkDefinitions = options?.LinkDefinitions;
            if (linkDefinitions == null || linkDefinitions.Count == 0)
                return null;

            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new LinkTagger(buffer, linkDefinitions.Select(d => (new Regex(d.RegexPattern), d)), AggregatorService.GetClassifier(buffer))) as ITagger<T>;
        }
    }
}
