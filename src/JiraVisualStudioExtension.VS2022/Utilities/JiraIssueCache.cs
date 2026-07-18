using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using JiraVisualStudioExtension.IssueReferences;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace JiraVisualStudioExtension.Utilities
{
    public class CachedJiraIssue
    {
        public string Key { get; set; }
        public string Summary { get; set; }
        public string Status { get; set; }
        public string StatusCategory { get; set; }
        public string Assignee { get; set; }
        public string IssueType { get; set; }
        public string Priority { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? Updated { get; set; }
        public DateTimeOffset? ResolutionDate { get; set; }
        public DateTimeOffset LoadedAt { get; set; }
    }

    public static class JiraIssueCache
    {
        private static readonly ConcurrentDictionary<string, (CachedJiraIssue Issue, DateTimeOffset ExpiresAt)> _cache
            = new ConcurrentDictionary<string, (CachedJiraIssue, DateTimeOffset)>();

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public static async Task<(CachedJiraIssue Issue, string Error)> GetIssueAsync(string value, LinkDefinition linkDefinition)
        {
            var cacheKey = $"{linkDefinition.MatchType}|{linkDefinition.Details}|{value}";
            if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.Now)
            {
                return (cached.Issue, null);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var (userName, apiToken, subdomain) = VS2022Package.Options.JiraCredentials;

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(apiToken) || string.IsNullOrEmpty(subdomain))
                return (null, "Not logged in. Go to Tool -> Options to setup Jira connection");


            string apiUrl;
            switch (linkDefinition.MatchType)
            {
                case LinkMatchType.ExactMatch:
                    apiUrl = $"rest/api/3/issue/{value}?";
                    break;
                case LinkMatchType.MatchField:
                    var fieldName = linkDefinition.Details;
                    apiUrl = $"rest/api/latest/search/jql?jql={Uri.EscapeDataString($"\"{fieldName}\" ~ \"{value}\"")}&";
                    break;
                default:
                    return (null, "No details available for Custom match type");
            }

            var issue = await Task.Run<(CachedJiraIssue,string)>(() =>
            {
                try
                {
                    using var client = new WebClient();
                    client.BaseAddress = $"https://{subdomain}.atlassian.net/";
                    client.Headers[HttpRequestHeader.Authorization] =
                        $"Basic {Convert.ToBase64String(Encoding.Default.GetBytes($"{userName}:{apiToken}"))}";
                    client.Encoding = Encoding.UTF8;

                    const string fields = "summary,status,assignee,issuetype,priority,created,updated,resolutiondate";

                    {
                        apiUrl += $"&fields={fields}&maxResults=2";
                        var json = client.DownloadString(apiUrl);
                        var obj= JObject.Parse(json);

                        if (obj["issues"] is JArray arr)
                        {
                            if (arr.Count == 0)
                                return (null, "No matching issues found");
                            if (arr.Count > 1)
                                return (null, "Multiple matching issues found");
                            return (ParseIssue(arr[0]), null);
                        }
                        return (ParseIssue(obj), null);
                    }
                }
                catch(Exception ex)
                {
                    return (null, "Error: " + ex.Message);
                }
            });

            if (issue.Item1 != null)
            {
                _cache[cacheKey] = (issue.Item1, DateTimeOffset.Now + CacheDuration);
            }

            return issue;
        }

        private static CachedJiraIssue ParseIssue(JToken obj)
        {
            var f = obj["fields"];

            return new CachedJiraIssue
            {
                Key = (string)obj["key"],
                Summary = (string)f["summary"],
                Status = (string)f["status"]?["name"],
                StatusCategory = (string)f["status"]?["statusCategory"]?["name"],
                Assignee = (string)f["assignee"]?["displayName"],
                IssueType = (string)f["issuetype"]?["name"],
                Priority = (string)f["priority"]?["name"],
                Created = (DateTimeOffset?)f["created"],
                Updated = (DateTimeOffset?)f["updated"],
                ResolutionDate = (DateTimeOffset?)f["resolutiondate"],
                LoadedAt = DateTimeOffset.Now
            };
        }
    }
}
