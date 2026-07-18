using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace JiraVisualStudioExtension.IssueReferences.Editor
{
    internal sealed class LinkAdornmentTagger
        : IntraTextAdornmentTagger<LinkTag, LinkAdornment>, IDisposable
    {
        private readonly ITagAggregator<LinkTag> _tagger;
        private readonly ITagAggregator<IClassificationTag> _classificationTagger;

        private LinkAdornmentTagger(IWpfTextView view, ITagAggregator<LinkTag> tagger, ITagAggregator<IClassificationTag> classificationTagger)
            : base(view)
        {
            _tagger = tagger;
            _classificationTagger = classificationTagger;
        }

        public void Dispose()
        {
            _tagger.Dispose();

            view.Properties.RemoveProperty(typeof(LinkAdornmentTagger));
        }

        internal static ITagger<IntraTextAdornmentTag> GetTagger(IWpfTextView view, Lazy<(ITagAggregator<LinkTag>, ITagAggregator<IClassificationTag>)> tagger)
        {
            return view.Properties.GetOrCreateSingletonProperty(
                () =>
                {
                    var taggerValue = tagger.Value;
                    return new LinkAdornmentTagger(view, taggerValue.Item1, taggerValue.Item2);
                });
        }

        // To produce adornments that don't obscure the text, the adornment tags
        // should have zero length spans. Overriding this method allows control
        // over the tag spans.
        protected override IEnumerable<Tuple<SnapshotSpan, PositionAffinity?, LinkTag>> GetAdornmentData(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            var snapshot = spans[0].Snapshot;

            var clTags = _tagger.GetTags(spans);

            foreach (var dataTagSpan in clTags)
            {
                var linkTagSpans = dataTagSpan.Span.GetSpans(snapshot);

                // Ignore data tags that are split by projection.
                // This is theoretically possible but unlikely in current scenarios.
                if (linkTagSpans.Count != 1)
                {
                    continue;
                }

                var classificationTags = _classificationTagger.GetTags(linkTagSpans).Select(c => c?.Tag?.ClassificationType?.Classification).ToList();
                if (classificationTags.All(c => c != "comment" && c != "xml doc comment - text"))
                    continue;

                var adornmentSpan = new SnapshotSpan(linkTagSpans[0].Start, 0);


                yield return Tuple.Create(adornmentSpan, (PositionAffinity?)PositionAffinity.Successor, dataTagSpan.Tag);
            }
        }

        protected override LinkAdornment CreateAdornment(LinkTag dataTag, SnapshotSpan span)
        {
            return new LinkAdornment(dataTag);
        }

        protected override bool UpdateAdornment(LinkAdornment adornment, LinkTag dataTag)
        {
            adornment.Update(dataTag);
            return true;
        }
    }
}