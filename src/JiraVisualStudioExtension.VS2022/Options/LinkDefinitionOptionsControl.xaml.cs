using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JiraVisualStudioExtension.IssueReferences;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace JiraVisualStudioExtension.Options
{
    public partial class LinkDefinitionOptionsControl : UserControl
    {
        private ObservableCollection<LinkDefinition> _items = new();

        public LinkDefinitionOptionsControl()
        {
            InitializeComponent();
        }

        public void Initialize(List<LinkDefinition> linkDefinitions)
        {
            _items = new ObservableCollection<LinkDefinition>(
                linkDefinitions.Select(d => new LinkDefinition
                {
                    Name = d.Name,
                    RegexPattern = d.RegexPattern,
                    MatchType = d.MatchType,
                    Details = d.Details
                }));
            LinkDefinitionsGrid.ItemsSource = _items;
        }

        public List<LinkDefinition> GetLinkDefinitions()
        {
            return _items
                .Where(d => !string.IsNullOrWhiteSpace(d.RegexPattern))
                .ToList();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var json = JsonConvert.SerializeObject(GetLinkDefinitions(), Formatting.Indented, new StringEnumConverter());
            Clipboard.SetText(json);
            MessageBox.Show("Link definitions copied to clipboard.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var json = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(json))
                {
                    MessageBox.Show("Clipboard is empty.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var imported = JsonConvert.DeserializeObject<List<LinkDefinition>>(json);
                if (imported == null || imported.Count == 0)
                {
                    MessageBox.Show("No valid link definitions found in clipboard.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Import {imported.Count} link definition(s)?\nThis will replace the current entries.",
                    "Import",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.OK)
                {
                    _items = new ObservableCollection<LinkDefinition>(imported);
                    LinkDefinitionsGrid.ItemsSource = _items;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
