namespace JiraVisualStudioExtension.IssueReferences
{
    public enum LinkMatchType
    {
        ExactMatch,
        MatchField,
        Custom
    }

    public class LinkDefinition
    {
        public string Name { get; set; }

        public string RegexPattern { get; set; }

        public LinkMatchType MatchType { get; set; }

        public string Details { get; set; }
    }
}