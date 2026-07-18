using Microsoft.VisualStudio.Text.Tagging;

namespace JiraVisualStudioExtension.IssueReferences
{
    public class LinkTag : ITag
    {
        private LinkTag()
        {
        }

        public static LinkTag Create(string link, LinkDefinition linkDefinition)
        {
            var result = new LinkTag
            {
                Value = link,
                LinkDefinition = linkDefinition
            };

            return result;
        }

        /// <summary>
        /// The value that was found as a match in the editor
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// The <see cref="LinkDefinition"/> that was matched in the editor
        /// </summary>
        public LinkDefinition LinkDefinition { get; private set; }
    }
}