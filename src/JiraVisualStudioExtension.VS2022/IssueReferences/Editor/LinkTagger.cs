using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace JiraVisualStudioExtension.IssueReferences.Editor
{
    internal sealed class LinkTagger : RegexTagger<LinkTag, LinkDefinition>
    {
        internal LinkTagger(ITextBuffer buffer, IEnumerable<(Regex Expression, LinkDefinition Metadata)> matchExpressions, IClassifier classifier)
            : base(buffer, matchExpressions, classifier)
        {
        }

        protected override IEnumerable<(LinkTag, int, int)> TryCreateTagsForMatch(Match match, LinkDefinition metadata)
        {
            yield return (LinkTag.Create(match.Value, metadata), match.Index, match.Value.Length);
        }
    }
}