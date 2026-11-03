using System.Text.RegularExpressions;

namespace JiraVisualStudioExtension.Utilities
{
    public static class ChangesetUrl
    {
        public const string Default =
            "https://vsrssinttools01/DevTools/cs.axd?cs={id}&pcguid=b0d4c367-d182-40b5-b954-be1fd38bd623";

        public static string Format(string template, int changesetId)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                template = Default;
            }

            return Regex.Replace(template, @"\{id\}", changesetId.ToString(), RegexOptions.IgnoreCase);
        }
    }
}
