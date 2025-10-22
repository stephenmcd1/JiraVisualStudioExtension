# Jira Visual Studio Extension
This extension augments the existing Team Explorer UI when Visual Studio's source control plug-in is configured to "Visual Studio Team Foundation Server (TFS)". It injects Jira-related features into the Pending Changes view (automatic creation of Comments on Issues at check-in and basic Issue editing). 

Important notes:
- Required source control plug-in: Visual Studio Team Foundation Server (TFS).
- Not supported when the source control plug-in is set to Git — Git-based workflows are not supported.


## Installing

1. In Visual Studio go to Tools → Options → Source Control → Plug-in Selection.
2. Select "Visual Studio Team Foundation Server".
3. Restart Visual Studio if needed.
Visual Studio extension that integrates Jira with Visual Studio including automatic creation of Comments on Issues when code is checked-in and the ability to perform basic editing of Issues - all within the Pending Changes tab of Team Explorer

or you can install it from the Visual Studio Marketplace [here](https://marketplace.visualstudio.com/items?itemName=StephenMcDaniel.JiraExtension-VS2022)

## Usage

- Open Visual Studio and show Team Explorer (View → Team Explorer).
- In Team Explorer select "Pending Changes".
- Under the  "Jira Issues" tab, follow the on-screen instructions to configure your Jira connection: enter your Jira instance URL, your username/email, and the API token (use the API token as the password).
- Click Connect/Save to persist the credentials and enable Jira features in Pending Changes.

Note: When logging in, you will need to use an API Key for your password. See [Jira Documentation](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/) for details on how to create an API Key.

## License

[MIT](./LICENSE)
