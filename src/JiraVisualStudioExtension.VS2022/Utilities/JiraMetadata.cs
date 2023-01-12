using System.Collections.Generic;

namespace JiraVisualStudioExtension.Utilities
{
    public class JiraMetadata
    {
        public string SprintFieldName { get; set; }

        public List<IssueType> IssueTypes { get; set; }

        public class IssueType
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }
    }
}