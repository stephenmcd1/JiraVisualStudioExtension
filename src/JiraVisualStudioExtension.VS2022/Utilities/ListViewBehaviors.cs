using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;
using System.Windows;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace JiraVisualStudioExtension.Utilities
{
    // Class implementing handy behaviors for the ListView control
    public static class ListViewBehaviors
    {
        // Technique for updating column widths of a ListView's GridView manually
        public static void UpdateColumnWidths(GridView gridView)
        {
            // For each column...
            foreach (var column in gridView.Columns)
            {
                // If this is an "auto width" column...
                if (double.IsNaN(column.Width))
                {
                    // Set its Width back to NaN to auto-size again
                    column.Width = 0;
                    column.Width = double.NaN;
                }
            }
        }

        // Definition of the IsAutoUpdatingColumnWidthsProperty attached DependencyProperty
        public static readonly DependencyProperty IsAutoUpdatingColumnWidthsProperty =
            DependencyProperty.RegisterAttached(
                "IsAutoUpdatingColumnWidths",
                typeof(bool),
                typeof(ListViewBehaviors),
                new UIPropertyMetadata(false, OnIsAutoUpdatingColumnWidthsChanged));

        // Get/set methods for the attached DependencyProperty
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters",
            Justification = "Only applies to ListView instances.")]
        public static bool GetIsAutoUpdatingColumnWidths(ListView listView)
        {
            return (bool)listView.GetValue(IsAutoUpdatingColumnWidthsProperty);
        }
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters",
            Justification = "Only applies to ListView instances.")]
        public static void SetIsAutoUpdatingColumnWidths(ListView listView, bool value)
        {
            listView.SetValue(IsAutoUpdatingColumnWidthsProperty, value);
        }

        // Change handler for the attached DependencyProperty
        private static void OnIsAutoUpdatingColumnWidthsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            // Get the ListView instance and new bool value
            var listView = o as ListView;
            if ((null != listView) && (e.NewValue is bool))
            {
                // Get a descriptor for the ListView's ItemsSource property
                var descriptor = DependencyPropertyDescriptor.FromProperty(ListView.ItemsSourceProperty, typeof(ListView));
                if ((bool)e.NewValue)
                {
                    // Enabling the feature, so add the change handler
                    descriptor.AddValueChanged(listView, OnListViewItemsSourceValueChanged);
                }
                else
                {
                    // Disabling the feature, so remove the change handler
                    descriptor.RemoveValueChanged(listView, OnListViewItemsSourceValueChanged);
                }
            }
        }

        // Handler for changes to the ListView's ItemsSource updates the column widths
        private static void OnListViewItemsSourceValueChanged(object sender, EventArgs e)
        {
            // Get a reference to the ListView's GridView...
            var listView = sender as ListView;
            if (null != listView)
            {
                var oc = listView.ItemsSource as INotifyCollectionChanged;
                
                var gridView = listView.View as GridView;
                if (null != gridView)
                {
                    if (oc != null)
                    {
                        NotifyCollectionChangedEventHandler handler = null;
                        handler = (s, a) =>
                        {
                            if(listView.ItemsSource != oc)
                            {
                                oc.CollectionChanged -= handler;
                                return;
                            }
                            UpdateColumnWidths(gridView);
                        };
                        //TODO: Should unsubscribe I guess
                        oc.CollectionChanged += handler;
                    }
                    // And update its column widths
                    UpdateColumnWidths(gridView);
                }
            }
        }
    }
}
