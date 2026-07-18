using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace JiraVisualStudioExtension.IssueReferences.Editor
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(StandardContentTypeNames.Text)]
    [ContentType(StandardContentTypeNames.Projection)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class LinkAdornmentTaggerProvider : IViewTaggerProvider
    {
#pragma warning disable 649 // "field never assigned to" -- field is set by MEF.
        [Import]
#pragma warning disable SA1401 // Fields should be private
        internal IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService;

        internal IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService;

        internal IClassifierAggregatorService ClassifierAggregatorService;
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore 649

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer)
            where T : ITag
        {
            if (textView == null)
            {
                throw new ArgumentNullException(nameof(textView));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer != textView.TextBuffer)
            {
                return null;
            }

            return LinkAdornmentTagger.GetTagger(
                        (IWpfTextView)textView,
                        new Lazy<(ITagAggregator<LinkTag>, ITagAggregator<IClassificationTag>)>(
                            () =>
                            {
                                var componentModel = (IComponentModel)ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel));
                                var exportProvider = componentModel.DefaultExportProvider;

                                var viewTagAggregatorFactoryService = exportProvider.GetExportedValue<IViewTagAggregatorFactoryService>();

                                var tagAggregator = viewTagAggregatorFactoryService.CreateTagAggregator<IClassificationTag>(textView);

                                return (
                                    BufferTagAggregatorFactoryService.CreateTagAggregator<LinkTag>(
                                        textView.TextBuffer), tagAggregator
                                );
                            }))
                    as ITagger<T>
                ;
        }
    }
}