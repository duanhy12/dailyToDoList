using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using DailyToDoList.Models;
using DailyToDoList.Services;
using DailyToDoList.ViewModels;

namespace DailyToDoList;

public partial class MainWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    private const double DefaultExpandedHeightFactor = 0.82;
    private const double MinimumExpandedWidth = 300;
    private const double CollapsedWidth = 72;
    private const double CollapsedHeight = 72;
    private const double WindowMargin = 10;

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _collapseTimer;
    private readonly Dictionary<Guid, double> _taskTopCache = new();
    private TaskItem? _draggedTask;
    private ListBoxItem? _draggedTaskContainer;
    private Point _dragStartPoint;
    private bool _isCollapsed;
    private bool _isDraggingTask;
    private bool _isTaskPointerDown;
    private bool _isDraggingCollapsed;
    private bool _collapsedPositionLocked;
    private bool _collapsedDragMoved;
    private double _expandedWidth;
    private double _expandedHeight;
    private double _collapsedLeft;
    private double _collapsedTop;
    private Point _collapsedDragOffset;
    private Point _collapsedMouseDownPoint;
    private Point _collapsedMouseDownScreenPoint;
    private Point _collapsedWindowStartPoint;
    private int _currentTaskInsertionIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new TaskStorageService());
        DataContext = _viewModel;

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _collapseTimer.Tick += CollapseTimer_OnTick;

        Loaded += OnLoaded;
        MouseLeave += OnWindowMouseLeave;
        PreviewMouseMove += OnWindowPreviewMouseMove;
        PreviewMouseLeftButtonUp += OnWindowPreviewMouseLeftButtonUp;
        Deactivated += (_, _) => ScheduleCollapseIfNeeded();
        ExpandedPanel.MouseEnter += (_, _) => _collapseTimer.Stop();
        ActiveTasksList.LayoutUpdated += ActiveTasksList_OnLayoutUpdated;
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
        if (_isCollapsed || _isDraggingCollapsed || _isDraggingTask || _isTaskPointerDown)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void CollapseTimer_OnTick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (_isCollapsed || _isDraggingTask || _isTaskPointerDown)
        {
            return;
        }

        var mousePosition = GetCursorScreenPosition();
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
        _dragStartPoint = e.GetPosition(RootGrid);
        _draggedTask = GetTaskFromDependencyObject((DependencyObject)e.OriginalSource);
        _isTaskPointerDown = _draggedTask is not null;
        _collapseTimer.Stop();
    }

    private void ActiveTasksList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingTask)
        {
            _isTaskPointerDown = false;
            _draggedTask = null;
        }
    }

    private void ActiveTasksList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingTask)
        {
            UpdateTaskDrag();
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || _draggedTask is null)
        {
            return;
        }

        var position = e.GetPosition(RootGrid);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        BeginTaskDrag(_draggedTask);
    }

    private void BeginTaskDrag(TaskItem task)
    {
        if (_isDraggingTask)
        {
            return;
        }

        _isDraggingTask = true;
        _isTaskPointerDown = false;
        _currentTaskInsertionIndex = ActiveTasksList.Items.IndexOf(task);
        _draggedTaskContainer = ActiveTasksList.ItemContainerGenerator.ContainerFromItem(task) as ListBoxItem;
        if (_draggedTaskContainer is not null)
        {
            _draggedTaskContainer.Opacity = 0.08;
        }

        DragGhostText.Text = string.IsNullOrWhiteSpace(task.Title) ? "Task" : task.Title;
        UpdateDragGhostPosition();
        DragGhostPopup.IsOpen = true;
        Mouse.Capture(RootGrid);
    }

    private void UpdateTaskDrag()
    {
        if (!_isDraggingTask || _draggedTask is null)
        {
            return;
        }

        UpdateDragGhostPosition();

        var targetIndex = GetInsertionIndexFromCursor();
        if (targetIndex >= 0 && targetIndex != _currentTaskInsertionIndex)
        {
            if (_viewModel.MoveTaskToIndex(_draggedTask, targetIndex))
            {
                _currentTaskInsertionIndex = targetIndex;
            }
        }
    }

    private void EndTaskDrag()
    {
        if (!_isDraggingTask)
        {
            return;
        }

        _isDraggingTask = false;
        _isTaskPointerDown = false;
        DragGhostPopup.IsOpen = false;
        if (_draggedTaskContainer is not null)
        {
            _draggedTaskContainer.Opacity = 1;
            _draggedTaskContainer = null;
        }

        Mouse.Capture(null);
        _currentTaskInsertionIndex = -1;
        _ = _viewModel.PersistAsync();
        _draggedTask = null;
    }

    private void OnWindowPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingTask)
        {
            UpdateTaskDrag();
        }
    }

    private void OnWindowPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingTask)
        {
            EndTaskDrag();
        }

        _isTaskPointerDown = false;
        _draggedTask = null;
    }

    private void UpdateDragGhostPosition()
    {
        if (!DragGhostPopup.IsOpen)
        {
            return;
        }

        var screenPoint = GetCursorScreenPosition();
        var localPoint = RootGrid.PointFromScreen(screenPoint);
        DragGhostPopup.HorizontalOffset = localPoint.X + 18;
        DragGhostPopup.VerticalOffset = localPoint.Y + 12;
    }

    private int GetInsertionIndexFromCursor()
    {
        var cursorPoint = ActiveTasksList.PointFromScreen(GetCursorScreenPosition());
        var items = ActiveTasksList.Items.Cast<TaskItem>().ToList();
        if (items.Count == 0)
        {
            return 0;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (ActiveTasksList.ItemContainerGenerator.ContainerFromItem(items[index]) is not ListBoxItem container)
            {
                continue;
            }

            var topLeft = container.TransformToAncestor(ActiveTasksList).Transform(new Point(0, 0));
            var midpoint = topLeft.Y + (container.ActualHeight / 2);
            if (cursorPoint.Y < midpoint)
            {
                return index;
            }
        }

        return items.Count;
    }

    private void ActiveTasksList_OnLayoutUpdated(object? sender, EventArgs e)
    {
        foreach (var task in ActiveTasksList.Items.Cast<TaskItem>())
        {
            if (ActiveTasksList.ItemContainerGenerator.ContainerFromItem(task) is not ListBoxItem container)
            {
                continue;
            }

            var top = container.TransformToAncestor(ActiveTasksList).Transform(new Point(0, 0)).Y;
            if (_taskTopCache.TryGetValue(task.Id, out var previousTop) && Math.Abs(previousTop - top) > 0.5)
            {
                var transform = container.RenderTransform as TranslateTransform;
                if (transform is null)
                {
                    transform = new TranslateTransform();
                    container.RenderTransform = transform;
                }

                transform.Y = previousTop - top;
                transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(170))
                {
                    EasingFunction = new QuadraticEase()
                });
            }

            _taskTopCache[task.Id] = top;
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

    private void CollapsedPanel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isCollapsed)
        {
            return;
        }

        _isDraggingCollapsed = true;
        _collapsedPositionLocked = true;
        _collapsedDragMoved = false;
        _collapsedMouseDownPoint = e.GetPosition(this);
        _collapsedMouseDownScreenPoint = GetCursorScreenPosition();
        _collapsedWindowStartPoint = new Point(Left, Top);
        _collapsedDragOffset = new Point(_collapsedMouseDownScreenPoint.X - Left, _collapsedMouseDownScreenPoint.Y - Top);
        AnimateWindowCardScale(0.96);
        CollapsedPanel.CaptureMouse();
        e.Handled = true;
    }

    private void CollapsedPanel_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isCollapsed || !_isDraggingCollapsed || !CollapsedPanel.IsMouseCaptured)
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        if (!_collapsedDragMoved &&
            Math.Abs(currentPoint.X - _collapsedMouseDownPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPoint.Y - _collapsedMouseDownPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _collapsedDragMoved = true;
        var workArea = SystemParameters.WorkArea;
        var screenPoint = GetCursorScreenPosition();
        var deltaX = screenPoint.X - _collapsedMouseDownScreenPoint.X;
        var deltaY = screenPoint.Y - _collapsedMouseDownScreenPoint.Y;
        _collapsedLeft = Math.Clamp(_collapsedWindowStartPoint.X + deltaX, workArea.Left, workArea.Right - CollapsedWidth);
        _collapsedTop = Math.Clamp(_collapsedWindowStartPoint.Y + deltaY, workArea.Top, workArea.Bottom - CollapsedHeight);
        Left = _collapsedLeft;
        Top = _collapsedTop;
    }

    private void CollapsedPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isCollapsed)
        {
            return;
        }

        var shouldExpand = !_collapsedDragMoved;
        _isDraggingCollapsed = false;
        CollapsedPanel.ReleaseMouseCapture();
        AnimateWindowCardScale(1);
        e.Handled = true;

        if (shouldExpand)
        {
            ExpandWindow();
        }
    }

    private static Point GetCursorScreenPosition()
    {
        GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    private void AnimateWindowCardScale(double scale)
    {
        var duration = TimeSpan.FromMilliseconds(120);
        WindowCardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration) { EasingFunction = new QuadraticEase() });
        WindowCardScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration) { EasingFunction = new QuadraticEase() });
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
