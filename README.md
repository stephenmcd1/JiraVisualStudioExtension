# Jira Visual Studio Extension
This extension integrates Jira with Visual Studio 2022, providing two main features:

1. **Clickable Jira References in Editor** — Automatically detects Jira issue keys (e.g., `//This workaround can be removed once we finish APP-123`) in your code and converts them to clickable links that open directly in your browser.

2. **Team Explorer Integration (TFS only)** — Augments the Pending Changes view with automatic creation of Comments on Issues at check-in and basic exploring of Issues.

Important notes for Team Explorer features:
- Required source control plug-in: Visual Studio Team Foundation Server (TFS).
- Not supported when the source control plug-in is set to Git — Git-based workflows are not supported.


## Installing

Install from the Visual Studio Marketplace [here](https://marketplace.visualstudio.com/items?itemName=StephenMcDaniel.JiraExtension-VS2022)

## Usage


### Clickable Jira References

The extension automatically detects Jira issue references in your code editor and converts them into clickable links with rich hover tooltips. Clicking a link opens the issue directly in your browser.

Out of the box, the extension doesn't know what kind of links to detect. You must configure this in Tools → Options → Jira Extension → Link Definitions to add or modify regex patterns and link types. 
**Link Definition Types:**

1. **Exact Match** — The matched text is the exact Jira issue key (e.g., `PROJ-123`). Opens the issue directly: `https://yourinstance.atlassian.net/browse/PROJ-123`

2. **Match Field** — The matched text is a value in a custom Jira field (e.g., a legacy issue ID or external reference). The extension searches Jira for issues where the specified field contains the matched value. If found, opens the issue; otherwise, opens a JQL search. In this mode, you must specify the name of the field.

3. **Custom** — The matched text is used in a custom URL format string. Use `{0}` as a placeholder for the matched value (e.g., `https://example.com/issue/{0}`).

### Team Explorer Integration

- Open Visual Studio and show Team Explorer (View → Team Explorer).
- In Team Explorer select "Pending Changes".
- Under the  "Jira Issues" tab, follow the on-screen instructions to configure your Jira connection: enter your Jira instance URL, your username/email, and the API token (use the API token as the password).
- Click Connect/Save to persist the credentials and enable Jira features in Pending Changes.

Note: When logging in, you will need to use an API Key for your password. See [Jira Documentation](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/) for details on how to create an API Key.


## License

[MIT](./LICENSE)
