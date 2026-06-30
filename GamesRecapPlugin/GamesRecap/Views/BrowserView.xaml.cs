using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GamesRecap.ViewModels;
using Playnite.SDK;

namespace GamesRecap.Views
{
    public partial class BrowserView : UserControl
    {
        private DispatcherTimer progressTimer;
        private double progressValue;
        private bool isFirstTick;
        private static readonly Random rng = new Random();

        public BrowserView()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
            DataContextChanged += OnDataContextChanged;
            Loaded += OnViewLoaded;
            Unloaded += OnViewUnloaded;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is BrowserViewModel vm && vm.IsLoading)
                StartProgress();
        }

        private void OnViewUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is BrowserViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is BrowserViewModel oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;

            if (DataContext is BrowserViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                if (vm.IsLoading)
                    StartProgress();
            }
        }

        private static readonly ILogger progressLogger = LogManager.GetLogger();

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BrowserViewModel.IsLoading))
                return;

            var isLoading = ((BrowserViewModel)sender).IsLoading;
            progressLogger.Debug($"Progress: PropertyChanged IsLoading={isLoading}");
            if (isLoading)
                StartProgress();
            else
                CompleteProgress();
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
            progressLogger.Debug("Progress: StartProgress");
            progressTimer?.Stop();
            progressTimer = null;
            ProgressBar.BeginAnimation(UIElement.OpacityProperty, null);
            ProgressBar.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);

            progressValue = 0.08;
            ((ScaleTransform)ProgressBar.RenderTransform).ScaleX = 0.08;
            ProgressBar.Opacity = 1;
            isFirstTick = true;

            ScheduleNextProgressTick();
        }

        private void ScheduleNextProgressTick()
        {
            if (progressValue >= 0.75) return;

            progressTimer = new DispatcherTimer();
            progressTimer.Interval = isFirstTick
                ? TimeSpan.FromMilliseconds(rng.Next(100, 300))
                : TimeSpan.FromMilliseconds(rng.Next(400, 1800));
            isFirstTick = false;
            progressTimer.Tick += OnProgressTick;
            progressTimer.Start();
        }

        private void OnProgressTick(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;
            if (timer == null || timer != progressTimer) return;

            timer.Stop();
            timer.Tick -= OnProgressTick;

            double increment = 0.06 + rng.NextDouble() * 0.14;
            progressValue = Math.Min(progressValue + increment, 0.75);

            var anim = new DoubleAnimation(progressValue, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBar.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);

            if (progressValue < 0.75)
                ScheduleNextProgressTick();
        }

        private void CompleteProgress()
        {
            progressTimer?.Stop();
            progressTimer = null;

            Dispatcher.CurrentDispatcher.BeginInvoke(
                new Action(AnimateCompletion),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void AnimateCompletion()
        {
            if (DataContext is BrowserViewModel vm && vm.IsLoading)
                return;

            var currentX = ((ScaleTransform)ProgressBar.RenderTransform).ScaleX;
            ProgressBar.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ((ScaleTransform)ProgressBar.RenderTransform).ScaleX = currentX;

            var fill = new DoubleAnimation(currentX, 1, TimeSpan.FromSeconds(0.6))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBar.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, fill);

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4))
            {
                BeginTime = TimeSpan.FromSeconds(0.6)
            };
            ProgressBar.BeginAnimation(UIElement.OpacityProperty, fade);
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

        private void MainSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateMainSearchPlaceholder();
        private void MainSearch_GotFocus(object sender, RoutedEventArgs e) => UpdateMainSearchPlaceholder();
        private void MainSearch_LostFocus(object sender, RoutedEventArgs e) => UpdateMainSearchPlaceholder();

        private void UpdateMainSearchPlaceholder()
        {
            MainSearchPlaceholder.Visibility =
                string.IsNullOrEmpty(MainSearch.Text) && !MainSearch.IsKeyboardFocused
                ? Visibility.Visible : Visibility.Collapsed;
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
