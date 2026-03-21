using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DailyToDoList.Models;
using DailyToDoList.Services;
using DailyToDoList.ViewModels;

namespace DailyToDoList;

public partial class MainWindow : Window
{
    private const double DefaultExpandedHeightFactor = 0.82;
    private const double MinimumExpandedWidth = 300;
    private const double CollapsedWidth = 88;
    private const double CollapsedHeight = 88;
    private const double WindowMargin = 10;

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _expandTimer;
    private TaskItem? _draggedTask;
    private ListBoxItem? _draggedTaskContainer;
    private Point _dragStartPoint;
    private bool _isCollapsed;
    private bool _isDraggingCollapsed;
    private bool _collapsedPositionLocked;
    private double _expandedWidth;
    private double _expandedHeight;
    private double _collapsedLeft;
    private double _collapsedTop;
    private Point _collapsedDragOffset;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new TaskStorageService());
        DataContext = _viewModel;

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _collapseTimer.Tick += CollapseTimer_OnTick;

        _expandTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _expandTimer.Tick += ExpandTimer_OnTick;

        Loaded += OnLoaded;
        MouseLeave += OnWindowMouseLeave;
        Deactivated += (_, _) => ScheduleCollapseIfNeeded();
        ExpandedPanel.MouseEnter += (_, _) => _collapseTimer.Stop();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureExpandedBounds();
        SetDefaultCollapsedPosition();
        ApplyExpandedPosition();
        await _viewModel.InitializeAsync();
    }

    private void ConfigureExpandedBounds()
    {
        var workArea = SystemParameters.WorkArea;
        _expandedWidth = Math.Max(MinimumExpandedWidth, workArea.Width / 5);
        _expandedHeight = Math.Max(520, workArea.Height * DefaultExpandedHeightFactor);
        Width = _expandedWidth;
        Height = _expandedHeight;
    }

    private void ApplyExpandedPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - WindowMargin;
        Top = Math.Max(workArea.Top + WindowMargin, workArea.Bottom - Height - WindowMargin);
    }

    private void SetDefaultCollapsedPosition()
    {
        var workArea = SystemParameters.WorkArea;
        _collapsedLeft = workArea.Right - CollapsedWidth - WindowMargin;
        _collapsedTop = workArea.Bottom - CollapsedHeight - WindowMargin;
    }

    private void EnsureCollapsedPositionInBounds()
    {
        var workArea = SystemParameters.WorkArea;
        _collapsedLeft = Math.Clamp(_collapsedLeft, workArea.Left, workArea.Right - CollapsedWidth);
        _collapsedTop = Math.Clamp(_collapsedTop, workArea.Top, workArea.Bottom - CollapsedHeight);
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isCollapsed)
        {
            ScheduleCollapseIfNeeded();
        }
    }

    private void ScheduleCollapseIfNeeded()
    {
        if (_isCollapsed || _isDraggingCollapsed)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void CollapseTimer_OnTick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (_isCollapsed)
        {
            return;
        }

        var mousePosition = PointToScreen(Mouse.GetPosition(this));
        var bounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        if (!bounds.Contains(mousePosition))
        {
            CollapseWindow();
        }
    }

    private void ExpandTimer_OnTick(object? sender, EventArgs e)
    {
        _expandTimer.Stop();
        if (_isCollapsed && !_isDraggingCollapsed)
        {
            ExpandWindow();
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
        ResizeThumb.Visibility = Visibility.Collapsed;
        CornerResizeThumb.Visibility = Visibility.Collapsed;
        ExpandedPanel.Visibility = Visibility.Collapsed;
        CollapsedPanel.Visibility = Visibility.Visible;

        if (!_collapsedPositionLocked)
        {
            SetDefaultCollapsedPosition();
        }

        EnsureCollapsedPositionInBounds();
        AnimateWindowTo(CollapsedWidth, CollapsedHeight, _collapsedLeft, _collapsedTop);
    }

    private void ExpandWindow()
    {
        _collapseTimer.Stop();
        _expandTimer.Stop();
        if (!_isCollapsed)
        {
            return;
        }

        _isCollapsed = false;
        ResizeThumb.Visibility = Visibility.Visible;
        CornerResizeThumb.Visibility = Visibility.Visible;
        CollapsedPanel.Visibility = Visibility.Collapsed;
        ExpandedPanel.Visibility = Visibility.Visible;

        var workArea = SystemParameters.WorkArea;
        var targetLeft = workArea.Right - _expandedWidth - WindowMargin;
        var targetTop = Math.Max(workArea.Top + WindowMargin, workArea.Bottom - _expandedHeight - WindowMargin);
        AnimateWindowTo(_expandedWidth, _expandedHeight, targetLeft, targetTop);
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

        BeginTaskDrag(_draggedTask);
    }

    private void BeginTaskDrag(TaskItem task)
    {
        _draggedTaskContainer = ActiveTasksList.ItemContainerGenerator.ContainerFromItem(task) as ListBoxItem;
        if (_draggedTaskContainer is not null)
        {
            _draggedTaskContainer.Opacity = 0.35;
        }

        DragGhostText.Text = string.IsNullOrWhiteSpace(task.Title) ? "Task" : task.Title;
        UpdateDragGhostPosition();
        DragGhostPopup.IsOpen = true;

        try
        {
            DragDrop.DoDragDrop(ActiveTasksList, task, DragDropEffects.Move);
        }
        finally
        {
            DragGhostPopup.IsOpen = false;
            if (_draggedTaskContainer is not null)
            {
                _draggedTaskContainer.Opacity = 1;
                _draggedTaskContainer = null;
            }

            _draggedTask = null;
        }
    }

    private void ActiveTasksList_OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (DragGhostPopup.IsOpen)
        {
            UpdateDragGhostPosition();
            Mouse.SetCursor(Cursors.SizeAll);
            e.UseDefaultCursors = false;
            e.Handled = true;
        }
    }

    private void ActiveTasksList_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateDragGhostPosition();
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void UpdateDragGhostPosition()
    {
        if (!DragGhostPopup.IsOpen)
        {
            return;
        }

        var screenPoint = PointToScreen(Mouse.GetPosition(this));
        var localPoint = RootGrid.PointFromScreen(screenPoint);
        DragGhostPopup.HorizontalOffset = localPoint.X + 16;
        DragGhostPopup.VerticalOffset = localPoint.Y + 16;
    }

    private async void ActiveTasksList_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TaskItem)))
        {
            return;
        }

        var dragged = e.Data.GetData(typeof(TaskItem)) as TaskItem;
        if (dragged is null)
        {
            return;
        }

        var target = GetTaskFromDependencyObject((DependencyObject)e.OriginalSource);
        if (target is not null)
        {
            await _viewModel.MoveTaskAsync(dragged, target);
        }
        else
        {
            await _viewModel.MoveTaskToEndAsync(dragged);
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

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private async void CompletedTasksExpander_OnChanged(object sender, RoutedEventArgs e)
    {
        await _viewModel.PersistAsync();
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        HelpPopup.IsOpen = !HelpPopup.IsOpen;
    }

    private void CollapsedPanel_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_isCollapsed && !_isDraggingCollapsed)
        {
            _expandTimer.Stop();
            _expandTimer.Start();
        }
    }

    private void CollapsedPanel_OnMouseLeave(object sender, MouseEventArgs e)
    {
        _expandTimer.Stop();
    }

    private void CollapsedPanel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isCollapsed)
        {
            return;
        }

        _expandTimer.Stop();
        _isDraggingCollapsed = true;
        _collapsedPositionLocked = true;
        var screenPoint = PointToScreen(e.GetPosition(this));
        _collapsedDragOffset = new Point(screenPoint.X - Left, screenPoint.Y - Top);
        CollapsedPanel.CaptureMouse();
        e.Handled = true;
    }

    private void CollapsedPanel_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isCollapsed || !_isDraggingCollapsed || !CollapsedPanel.IsMouseCaptured)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var screenPoint = PointToScreen(e.GetPosition(this));
        _collapsedLeft = Math.Clamp(screenPoint.X - _collapsedDragOffset.X, workArea.Left, workArea.Right - CollapsedWidth);
        _collapsedTop = Math.Clamp(screenPoint.Y - _collapsedDragOffset.Y, workArea.Top, workArea.Bottom - CollapsedHeight);
        Left = _collapsedLeft;
        Top = _collapsedTop;
    }

    private void CollapsedPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isCollapsed)
        {
            return;
        }

        _isDraggingCollapsed = false;
        CollapsedPanel.ReleaseMouseCapture();
        e.Handled = true;
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
        Height = Math.Clamp(Height, 520, workArea.Height - (2 * WindowMargin));
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
        var candidateHeight = Math.Clamp(Height - e.VerticalChange, 520, maxHeight);

        Width = candidateWidth;
        Height = candidateHeight;
        _expandedWidth = Width;
        _expandedHeight = Height;
        ApplyExpandedPosition();
    }
}
