using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using JiraVisualStudioExtension.ViewModels;
using Newtonsoft.Json.Linq;

namespace JiraVisualStudioExtension.Utilities
{
    public class JiraHelper
    {
        private static readonly Dictionary<string, JiraMetadata> _cachedMetadata = new Dictionary<string, JiraMetadata>();

        private class JiraWebClient : WebClient
        {
            public JiraWebClient(string credentials, string basePath)
            {
                BaseAddress = basePath;
                Headers[HttpRequestHeader.Authorization] =
                    $"Basic {Convert.ToBase64String(Encoding.Default.GetBytes(credentials))}";
                Encoding = Encoding.UTF8;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                LastUri = address;
                var r = (HttpWebRequest)base.GetWebRequest(address);
                r.AllowAutoRedirect = AllowAutoRedirect;
                return r;
            }


            public Uri LastUri { get; private set; }

            public bool AllowAutoRedirect { get; set; } = true;
        }

        private readonly Action<string> _onError;
        private readonly Action<JiraIssueViewModel> _onIssueUpdated;

        public JiraHelper(Action<string> onError, Action<JiraIssueViewModel> onIssueUpdated)
        {
            _onError = onError;
            _onIssueUpdated = onIssueUpdated;
        }

        public class QueryResult
        {
            public List<JiraIssueViewModel> Results { get; set; }
            public int TotalResultCount { get; set; }
        }

        private JiraWebClient _apiClient;
        public JiraMetadata Metadata { get; private set; }

        public bool IsLoggedOn { get; private set; }

        public async Task<QueryResult> GetIssues(string jql, int pageSize, string order = "Key", int start = 0)
        {
            var str = await Task.Run(() => _apiClient.DownloadString(
                "rest/api/latest/search" +
                $"?jql={jql} order by {order}" +
                //TODO: Use JiraIssueViewModel.FetchFields
                //"&fields=assignee,summary,status,issuetype,project,created,updated,description,comment" +
                $"&maxResults={pageSize}" +
                $"&startAt={start}"));

            var res = JObject.Parse(str);

            return new QueryResult
            {
                Results = ((JArray)res["issues"]).Select(o => JiraIssueViewModel.FromApi(o, Metadata, SaveIssue)).ToList(),
                TotalResultCount = (int)res["total"]
            };
        }

        public string TryLogOn(string userName, string password, string subDomain)
        {
            _apiClient = new JiraWebClient($"{userName}:{password}", $"https://{subDomain}.atlassian.net/");
            try
            {
                var resp = JToken.Parse(_apiClient.DownloadString("rest/api/3/myself"));
                if (!_cachedMetadata.ContainsKey(subDomain))
                {
                    var fields = JArray.Parse(_apiClient.DownloadString("rest/api/3/field"));
                    var sprintField = (string)fields.SingleOrDefault(f => (string)f["name"] == "Sprint")?["key"];
                    var issueTypes = JArray.Parse(_apiClient.DownloadString("rest/api/3/issuetype"));

                    _cachedMetadata[subDomain] = new JiraMetadata
                    {
                        SprintFieldName = sprintField,
                        IssueTypes = issueTypes
                            .Select(f => new JiraMetadata.IssueType
                            {
                                Name = (string)f["name"],
                                Description = (string)f["description"]
                            }).GroupBy(i => i.Name)
                            .Select(g => g.Count() == 1 ? g.First() : new JiraMetadata.IssueType{Name = g.Key, Description = "(Issue Type exists in multiple projects)"})
                            .OrderBy(i => i.Name)
                            .ToList()
                    };
                }
                Metadata = _cachedMetadata[subDomain];
                IsLoggedOn = true;
                return (string)resp["displayName"];
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SaveIssue(JiraIssueViewModel item)
        {
            
        }

        public bool AddCommentToIssue(JiraIssueViewModel issue, string commentBody)
        {
            try
            {
                _apiClient.Headers[HttpRequestHeader.ContentType] = "application/json";
                _apiClient.UploadString("rest/api/3/issue/" + issue.Key + "/comment", "POST", commentBody);
                return true;
            }
            catch (Exception e)
            {
                _onError("Error creating Jira Comment: " + e.Message);
                return false;
            }
        }
    }
}