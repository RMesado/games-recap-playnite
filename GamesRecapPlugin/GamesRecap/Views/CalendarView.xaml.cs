using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GamesRecap.Views
{
    public partial class CalendarView : UserControl
    {
        public CalendarView()
        {
            InitializeComponent();
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindParent<ScrollViewer>((DependencyObject)sender);
            if (scrollViewer != null)
            {
                e.Handled = true;
                scrollViewer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                { RoutedEvent = MouseWheelEvent });
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }
    }
}
