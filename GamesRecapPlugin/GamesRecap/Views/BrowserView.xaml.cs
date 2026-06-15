using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GamesRecap.ViewModels;

namespace GamesRecap.Views
{
    public partial class BrowserView : UserControl
    {
        public BrowserView()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
            Loaded += (_, _) =>
            {
                if (DataContext is BrowserViewModel vm)
                {
                    vm.OnLoadingChanged = OnLoadingChanged;
                    if (vm.IsLoading)
                        StartProgress();
                }
            };
        }

        private void OnLoadingChanged(bool isLoading)
        {
            if (isLoading) StartProgress();
            else CompleteProgress();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                ResetAllCards();
        }

        private void CardRoot_OnLoaded(object sender, RoutedEventArgs e)
        {
            var root = (Grid)sender;
            root.RenderTransformOrigin = new Point(0.5, 0.5);
            root.RenderTransform = new ScaleTransform(1, 1);

            if (root.Children.Count > 1)
            {
                var backFace = (UIElement)root.Children[1];
                backFace.RenderTransformOrigin = new Point(0.5, 0.5);
                backFace.RenderTransform = new ScaleTransform(-1, 1);
            }

            var border = FindVisualParent<Border>(root, b => b.CornerRadius.TopLeft > 0);
            if (border != null)
            {
                Action updateClip = () =>
                {
                    if (border.ActualWidth > 0 && border.ActualHeight > 0)
                    {
                        var r = border.CornerRadius.TopLeft;
                        border.Clip = new RectangleGeometry(
                            new Rect(0, 0, border.ActualWidth, border.ActualHeight), r, r);
                    }
                };
                border.Dispatcher.BeginInvoke(new Action(() => updateClip()),
                    System.Windows.Threading.DispatcherPriority.Loaded);
                border.SizeChanged += (_, _) => updateClip();
            }
        }

        private void StartProgress()
        {
            ProgressBar.BeginAnimation(UIElement.OpacityProperty, null);
            ProgressBar.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ((ScaleTransform)ProgressBar.RenderTransform).ScaleX = 0;
            ProgressBar.Opacity = 1;

            var anim = new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(4))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBar.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim, HandoffBehavior.SnapshotAndReplace);
        }

        private void CompleteProgress()
        {
            var currentX = ((ScaleTransform)ProgressBar.RenderTransform).ScaleX;
            ProgressBar.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);

            var sb = new Storyboard();

            var fill = new DoubleAnimation(currentX, 1, TimeSpan.FromSeconds(0.25));
            fill.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            Storyboard.SetTarget(fill, ProgressBar);
            Storyboard.SetTargetProperty(fill, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(fill);

            var fade = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3))
                { BeginTime = TimeSpan.FromSeconds(0.25) };
            Storyboard.SetTarget(fade, ProgressBar);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fade);

            sb.Begin();
        }

        private static T FindVisualParent<T>(DependencyObject child, Func<T, bool> predicate = null)
            where T : DependencyObject
        {
            while (child != null)
            {
                child = VisualTreeHelper.GetParent(child);
                if (child is T typed && (predicate == null || predicate(typed)))
                    return typed;
            }
            return null;
        }

        private void CardRoot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInsideButton(e.OriginalSource as DependencyObject))
                return;
            e.Handled = true;
            var root = (Grid)sender;
            FlipCard(root);
        }

        private void FlipBackButton_Click(object sender, RoutedEventArgs e)
        {
            var child = sender as DependencyObject;
            while (child != null)
            {
                if (child is Grid grid && grid.Name == "CardRoot")
                {
                    FlipCard(grid);
                    return;
                }
                child = VisualTreeHelper.GetParent(child);
            }
        }

        private void FlipCard(Grid root)
        {
            var scale = root.RenderTransform as ScaleTransform;
            if (scale == null) return;

            var front = (UIElement)root.Children[0];
            var back = (UIElement)root.Children[1];

            bool isFlipped = back.Visibility == Visibility.Visible;
            double from = scale.ScaleX;
            double to = isFlipped ? 1 : -1;

            var sb = new Storyboard();
            var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(0.4));
            anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
            Storyboard.SetTarget(anim, root);
            Storyboard.SetTargetProperty(anim, new PropertyPath(
                "(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            sb.Children.Add(anim);

            bool swapped = false;
            sb.CurrentTimeInvalidated += (s, _) =>
            {
                if (swapped) return;
                if (Math.Abs(scale.ScaleX) < 0.1)
                {
                    swapped = true;
                    if (isFlipped)
                    {
                        back.Visibility = Visibility.Hidden;
                        front.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        front.Visibility = Visibility.Hidden;
                        back.Visibility = Visibility.Visible;
                    }
                }
            };

            sb.Begin();
        }

        private static bool IsInsideButton(DependencyObject element)
        {
            while (element != null)
            {
                if (element is Button) return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private void ResetAllCards()
        {
            for (int i = 0; i < CardList.Items.Count; i++)
            {
                var container = CardList.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;

                var root = FindCardRoot(container);
                if (root == null) continue;

                if (root.RenderTransform is ScaleTransform scale)
                    scale.ScaleX = 1;

                if (root.Children.Count > 1)
                {
                    root.Children[0].Visibility = Visibility.Visible;
                    root.Children[1].Visibility = Visibility.Hidden;
                }
            }
        }

        private static Grid FindCardRoot(DependencyObject element)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                if (child is Grid grid && grid.Name == "CardRoot")
                    return grid;
                var result = FindCardRoot(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
