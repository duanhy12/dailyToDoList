using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DailyToDoList.Models;
using DailyToDoList.Services;
using DailyToDoList.ViewModels;

namespace DailyToDoList;

public partial class MainWindow : Window
{
    private const double DefaultExpandedHeightFactor = 0.78;
    private const double MinimumExpandedWidth = 260;
    private const double CollapsedWidth = 90;
    private const double CollapsedHeight = 170;
    private const double WindowMargin = 14;

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _collapseTimer;
    private TaskItem? _draggedTask;
    private Point _dragStartPoint;
    private bool _isCollapsed;
    private double _expandedWidth;
    private double _expandedHeight;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new TaskStorageService());
        DataContext = _viewModel;

        _collapseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(850)
        };
        _collapseTimer.Tick += CollapseTimer_OnTick;

        Loaded += OnLoaded;
        MouseEnter += (_, _) => ExpandWindow();
        MouseLeave += (_, _) => ScheduleCollapseIfNeeded();
        Deactivated += (_, _) => ScheduleCollapseIfNeeded();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureExpandedBounds();
        ApplyExpandedPosition();
        await _viewModel.InitializeAsync();
    }

    private void ConfigureExpandedBounds()
    {
        var workArea = SystemParameters.WorkArea;
        _expandedWidth = Math.Max(MinimumExpandedWidth, workArea.Width / 5);
        _expandedHeight = Math.Max(420, workArea.Height * DefaultExpandedHeightFactor);
        Width = _expandedWidth;
        Height = _expandedHeight;
    }

    private void ApplyExpandedPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - WindowMargin;
        Top = Math.Max(workArea.Top + WindowMargin, workArea.Bottom - Height - WindowMargin);
    }

    private void ApplyCollapsedPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - WindowMargin;
        Top = workArea.Bottom - Height - WindowMargin;
    }

    private void ScheduleCollapseIfNeeded()
    {
        if (!_isCollapsed)
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
    }

    private void CollapseTimer_OnTick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();

        var mousePosition = PointToScreen(Mouse.GetPosition(this));
        var bounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        if (!bounds.Contains(mousePosition))
        {
            CollapseWindow();
        }
    }

    private void CollapseWindow()
    {
        if (_isCollapsed)
        {
            return;
        }

        _expandedWidth = Width;
        _expandedHeight = Height;
        _isCollapsed = true;
        ExpandedPanel.Visibility = Visibility.Hidden;
        CollapsedPanel.Visibility = Visibility.Visible;
        AnimateWindowTo(CollapsedWidth, CollapsedHeight, SystemParameters.WorkArea.Right - CollapsedWidth - WindowMargin, SystemParameters.WorkArea.Bottom - CollapsedHeight - WindowMargin);
    }

    private void ExpandWindow()
    {
        _collapseTimer.Stop();
        if (!_isCollapsed)
        {
            return;
        }

        _isCollapsed = false;
        CollapsedPanel.Visibility = Visibility.Collapsed;
        ExpandedPanel.Visibility = Visibility.Visible;
        AnimateWindowTo(_expandedWidth, _expandedHeight, SystemParameters.WorkArea.Right - _expandedWidth - WindowMargin, Math.Max(SystemParameters.WorkArea.Top + WindowMargin, SystemParameters.WorkArea.Bottom - _expandedHeight - WindowMargin));
    }

    private void AnimateWindowTo(double width, double height, double left, double top)
    {
        var duration = TimeSpan.FromMilliseconds(220);
        BeginAnimation(WidthProperty, new DoubleAnimation(width, duration) { EasingFunction = new QuadraticEase() });
        BeginAnimation(HeightProperty, new DoubleAnimation(height, duration) { EasingFunction = new QuadraticEase() });
        BeginAnimation(LeftProperty, new DoubleAnimation(left, duration) { EasingFunction = new QuadraticEase() });
        BeginAnimation(TopProperty, new DoubleAnimation(top, duration) { EasingFunction = new QuadraticEase() });
    }

    private async void AddTaskButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.AddTaskAsync();
        TaskInput.Focus();
    }

    private async void TaskInput_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await _viewModel.AddTaskAsync();
    }

    private async void TaskCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: TaskItem item, IsChecked: bool isChecked })
        {
            await _viewModel.SetTaskCompletedAsync(item, isChecked);
        }
    }

    private async void DeleteTaskButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskItem item })
        {
            await _viewModel.DeleteTaskAsync(item);
        }
    }

    private void ActiveTasksList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _draggedTask = GetTaskFromDependencyObject((DependencyObject)e.OriginalSource);
    }

    private void ActiveTasksList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedTask is null)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(ActiveTasksList, _draggedTask, DragDropEffects.Move);
    }

    private async void ActiveTasksList_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TaskItem)))
        {
            return;
        }

        var target = GetTaskFromDependencyObject((DependencyObject)e.OriginalSource);
        var dragged = e.Data.GetData(typeof(TaskItem)) as TaskItem;
        if (dragged is not null && target is not null)
        {
            await _viewModel.MoveTaskAsync(dragged, target);
        }
    }

    private static TaskItem? GetTaskFromDependencyObject(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: TaskItem task })
            {
                return task;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private async void CompletedTasksExpander_OnChanged(object sender, RoutedEventArgs e)
    {
        await _viewModel.PersistAsync();
    }

    private void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isCollapsed)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var maxWidth = workArea.Width * 0.45;
        var candidateWidth = Math.Clamp(Width - e.HorizontalChange, MinimumExpandedWidth, maxWidth);
        Width = candidateWidth;
        Height = Math.Clamp(Height, 420, workArea.Height - (2 * WindowMargin));
        _expandedWidth = Width;
        _expandedHeight = Height;
        ApplyExpandedPosition();
    }

    private void CornerResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isCollapsed)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var maxWidth = workArea.Width * 0.45;
        var maxHeight = workArea.Height - (2 * WindowMargin);

        var candidateWidth = Math.Clamp(Width - e.HorizontalChange, MinimumExpandedWidth, maxWidth);
        var candidateHeight = Math.Clamp(Height - e.VerticalChange, 420, maxHeight);

        Width = candidateWidth;
        Height = candidateHeight;
        _expandedWidth = Width;
        _expandedHeight = Height;
        ApplyExpandedPosition();
    }

}
