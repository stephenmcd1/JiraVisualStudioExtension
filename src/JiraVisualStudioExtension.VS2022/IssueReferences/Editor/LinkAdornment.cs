using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using JiraVisualStudioExtension.Utilities;
using Microsoft.VisualStudio.Shell;
using Brushes = System.Windows.Media.Brushes;

namespace JiraVisualStudioExtension.IssueReferences.Editor
{
    internal sealed class LinkAdornment : Button
    {
        internal LinkAdornment(LinkTag tag)
        {
            //Set up a button with no extra styling for the user to click on. From: https://stackoverflow.com/a/79397900/385996
            var buttonStyle = new Style(typeof(Button));

            // Create ControlTemplate without triggers
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));

            // Minimum binding for the button to work
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));

            // Create the content presenter for button text
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            template.VisualTree = border;
            buttonStyle.Setters.Add(new Setter(TemplateProperty, template));

            Style = buttonStyle;

            Content = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/JiraVisualStudioExtension.VS2022;component/Resources/JiraIcon.png")),
                Height = 16
            };

            BorderBrush = null;
            Padding = new Thickness(0);
            Margin = new Thickness(0, 0, 2, 0);
            Background = Brushes.Transparent;

            Cursor = Cursors.Hand;
            _linkTag = tag;

            ToolTip = new ToolTip { Content = CreateLoadingContent() };
            ToolTipOpening += OnToolTipOpening;
        }

        private LinkTag _linkTag;

        internal void Update(LinkTag dataTag)
        {
            _linkTag = dataTag;
        }

#pragma warning disable VSTHRD100
        private async void OnToolTipOpening(object sender, ToolTipEventArgs e)
#pragma warning restore VSTHRD100
        {
            try
            {
                var tt = (ToolTip)ToolTip;
                tt.Content = CreateLoadingContent();

                var result = await JiraIssueCache.GetIssueAsync(_linkTag.Value, _linkTag.LinkDefinition);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                tt.Content = result.Issue != null ? CreateIssueContent(result.Issue) : CreateErrorContent(result.Error);
            }
            catch (Exception exc)
            {
                await OutputPane.Instance.WriteAsync(exc);
            }
        }

        private UIElement CreateLoadingContent()
        {
            return new TextBlock { Text = "Loading...", FontStyle = FontStyles.Italic, Foreground = Brushes.Gray };
        }

        private UIElement CreateIssueContent(CachedJiraIssue issue)
        {
            var panel = new StackPanel { MinWidth = 250 };

            var header = new TextBlock
            {
                Text = $"{issue.Key}: {issue.Summary}",
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            };
            panel.Children.Add(header);

            panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

            AddRow(panel, "Type", issue.IssueType);
            AddRow(panel, "Status", issue.Status);
            AddRow(panel, "Assignee", issue.Assignee ?? "Unassigned");
            AddRow(panel, "Priority", issue.Priority);
            if (issue.Created.HasValue)
                AddRow(panel, "Created", issue.Created.Value.LocalDateTime.ToString("g"));
            if (issue.Updated.HasValue)
                AddRow(panel, "Updated", issue.Updated.Value.LocalDateTime.ToString("g"));
            if (issue.ResolutionDate.HasValue)
                AddRow(panel, "Resolved", issue.ResolutionDate.Value.LocalDateTime.ToString("g"));

            panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

            var loadedText = new TextBlock
            {
                Text = FormatRelativeTime(issue.LoadedAt),
                FontSize = 10,
                Foreground = Brushes.Gray
            };
            panel.Children.Add(loadedText);

            return panel;
        }

        private UIElement CreateErrorContent(string error)
        {
            return new TextBlock
            {
                Text = "Unable to load issue. Details\n" + error,
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic
            };
        }

        private void AddRow(StackPanel panel, string label, string value)
        {
            if (value == null) return;

            var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            var labelBlock = new TextBlock
            {
                Text = label + ": ",
                FontWeight = FontWeights.SemiBold,
                Width = 70
            };
            DockPanel.SetDock(labelBlock, Dock.Left);
            row.Children.Add(labelBlock);

            row.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 });
            panel.Children.Add(row);
        }

        private static string FormatRelativeTime(DateTimeOffset loadedAt)
        {
            var elapsed = DateTimeOffset.Now - loadedAt;
            if (elapsed.TotalSeconds < 5)
                return "Loaded just now";
            if (elapsed.TotalSeconds < 60)
                return $"Loaded {(int)elapsed.TotalSeconds}s ago";
            return $"Loaded {(int)elapsed.TotalMinutes}m {elapsed.Seconds}s ago";
        }

        private async Task HandleClickAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var (_, _, subdomain) = VS2022Package.Options.JiraCredentials;
            var baseUrl = $"https://{subdomain}.atlassian.net";

            await StatusBarHelper.ShowMessageAsync("Opening " + _linkTag.Value);

            string url;
            switch (_linkTag.LinkDefinition.MatchType)
            {
                case LinkMatchType.ExactMatch:
                    url = $"{baseUrl}/browse/{_linkTag.Value}";
                    break;
                case LinkMatchType.MatchField:
                    var match = await JiraIssueCache.GetIssueAsync(_linkTag.Value, _linkTag.LinkDefinition);
                    if (match.Issue != null)
                    {
                        url = $"{baseUrl}/browse/{match.Issue.Key}";
                    }
                    else
                    {
                        var fieldName = _linkTag.LinkDefinition.Details;
                        url = $"{baseUrl}/issues/?jql={Uri.EscapeDataString($"\"{fieldName}\" ~ \"{_linkTag.Value}\"")}";
                    }

                    break;
                default:
                    url = string.Format(_linkTag.LinkDefinition.Details, _linkTag.Value);
                    break;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        protected override async void OnClick()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                base.OnClick();

                await HandleClickAsync();
            }
            catch (Exception exc)
            {
                await OutputPane.Instance.WriteAsync(exc);
            }
        }
    }
}
