# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution (requires Visual Studio 2022 or MSBuild with VS2022 workload)
msbuild JiraVisualStudioExtension.sln /p:Configuration=Debug /p:Platform="Any CPU"

# Build from Developer Command Prompt for VS 2022
devenv JiraVisualStudioExtension.sln /Build Debug
```

There are no tests in this project. There is no linter configured.

## Debugging

The project is configured to launch the VS Experimental Instance (`devenv.exe /rootsuffix Exp`) on F5. If MEF composition errors occur, delete `%localappdata%\Microsoft\VisualStudio\17.0Exp\ComponentModelCache`.

## Architecture

This is a Visual Studio 2022 VSIX extension that integrates Jira into the Team Explorer Pending Changes view (TFS source control only, not Git).

### Core Extension Points

**Package (`VS2022Package`)** — AsyncPackage entry point. Auto-loads without a solution. Exposes a static `Options` helper and `DTE` instance used throughout.

**Team Explorer Section (`JiraWorkItemSection`)** — Registered on `PendingChanges` page via `[TeamExplorerSection]`. Uses reflection to hook into internal TFS `PendingChangesPageViewModel`:
- Replaces the Check In command to inject issue key prefixes and trigger post-checkin Jira comments
- Replaces the Toggle Shelveset command to auto-set shelveset name from current issue
- Subscribes to the internal `CheckinCompleted` event to detect success/failure

**Editor Adornments (`IssueReferences/`)** — MEF-exported taggers that detect Jira issue key patterns (e.g., `PROJ-123`) in the editor using regex and render clickable links. Built on a generic `RegexTagger<T, TMatchMetadata>` base class.

### MVVM Layer

- `SectionContentViewModel` — Main UI logic: login, JQL query construction, issue list pagination, comment generation in Atlassian Document Format (ADF)
- `JiraIssueViewModel` — Single issue representation, parsed from Jira REST API v3 JSON via `FromApi()` factory
- `PagedItemListViewModel` — Generic paginated query wrapper using `nextPageToken`

### Jira API (`Utilities/JiraHelper`)

Talks to Jira Cloud REST API v3 (`https://{subdomain}.atlassian.net/`). Uses `WebClient` with Basic Auth (email + API token). Caches field metadata and issue types per subdomain.

### Settings (`Utilities/OptionsHelper`)

Credentials (API token, subdomain) are encrypted with DPAPI and stored in the VS registry hive. Link definitions (regex patterns for editor detection) are stored as multi-string registry values.

### Key Constraints

- Target framework: .NET Framework 4.8.1, C# 9
- TeamFoundation DLLs are vendored in `Dependencies/VS2022/` (not NuGet)
- Heavy use of reflection to access internal TFS types — changes in VS updates can break these hooks
- The `Microsoft.Xaml.Behaviors` assembly is resolved manually in a static constructor due to VS loading issues
