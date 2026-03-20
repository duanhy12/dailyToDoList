using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using DailyToDoList.Models;
using DailyToDoList.Services;

namespace DailyToDoList.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TaskStorageService _storageService;
    private bool _showCompletedTasks;
    private string _newTaskTitle = string.Empty;
    private bool _isBusy;

    public MainViewModel(TaskStorageService storageService)
    {
        _storageService = storageService;
        Tasks = new ObservableCollection<TaskItem>();
        Tasks.CollectionChanged += OnTasksCollectionChanged;

        ActiveTasks = CollectionViewSource.GetDefaultView(Tasks);
        ActiveTasks.Filter = task => task is TaskItem item && !item.IsCompleted;
        ActiveTasks.SortDescriptions.Add(new SortDescription(nameof(TaskItem.DisplayOrder), ListSortDirection.Ascending));

        CompletedTasks = new ListCollectionView(Tasks);
        CompletedTasks.Filter = task => task is TaskItem item && item.IsCompleted;
        CompletedTasks.SortDescriptions.Add(new SortDescription(nameof(TaskItem.CompletedAt), ListSortDirection.Descending));
    }

    public ObservableCollection<TaskItem> Tasks { get; }

    public ICollectionView ActiveTasks { get; }

    public ICollectionView CompletedTasks { get; }

    public string NewTaskTitle
    {
        get => _newTaskTitle;
        set
        {
            if (_newTaskTitle == value)
            {
                return;
            }

            _newTaskTitle = value;
            OnPropertyChanged(nameof(NewTaskTitle));
        }
    }

    public bool ShowCompletedTasks
    {
        get => _showCompletedTasks;
        set
        {
            if (_showCompletedTasks == value)
            {
                return;
            }

            _showCompletedTasks = value;
            OnPropertyChanged(nameof(ShowCompletedTasks));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _storageService.LoadAsync();
            foreach (var task in items.OrderBy(task => task.IsCompleted).ThenBy(task => task.DisplayOrder))
            {
                AttachTask(task);
                Tasks.Add(task);
            }

            NormalizeDisplayOrder();
            RefreshViews();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddTaskAsync()
    {
        var trimmed = NewTaskTitle.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var item = new TaskItem
        {
            Title = trimmed,
            DisplayOrder = NextActiveOrder()
        };

        AttachTask(item);
        Tasks.Add(item);
        NewTaskTitle = string.Empty;
        RefreshViews();
        await PersistAsync();
    }

    public async Task SetTaskCompletedAsync(TaskItem? item, bool isCompleted)
    {
        if (item is null || item.IsCompleted == isCompleted)
        {
            return;
        }

        item.IsCompleted = isCompleted;
        if (item.IsCompleted)
        {
            item.CompletedAt = DateTimeOffset.Now;
            item.DisplayOrder = int.MaxValue;
        }
        else
        {
            item.CompletedAt = null;
            item.DisplayOrder = NextActiveOrder();
        }

        NormalizeDisplayOrder();
        RefreshViews();
        await PersistAsync();
    }

    public async Task DeleteTaskAsync(TaskItem? item)
    {
        if (item is null)
        {
            return;
        }

        DetachTask(item);
        Tasks.Remove(item);
        NormalizeDisplayOrder();
        RefreshViews();
        await PersistAsync();
    }

    public async Task MoveTaskAsync(TaskItem draggedItem, TaskItem targetItem)
    {
        if (draggedItem == targetItem || draggedItem.IsCompleted || targetItem.IsCompleted)
        {
            return;
        }

        var activeTasks = Tasks
            .Where(task => !task.IsCompleted)
            .OrderBy(task => task.DisplayOrder)
            .ToList();

        var fromIndex = activeTasks.IndexOf(draggedItem);
        var toIndex = activeTasks.IndexOf(targetItem);
        if (fromIndex < 0 || toIndex < 0)
        {
            return;
        }

        activeTasks.RemoveAt(fromIndex);
        activeTasks.Insert(toIndex, draggedItem);

        for (var index = 0; index < activeTasks.Count; index++)
        {
            activeTasks[index].DisplayOrder = index;
        }

        RefreshViews();
        await PersistAsync();
    }

    public async Task PersistAsync()
    {
        await _storageService.SaveAsync(Tasks.OrderBy(task => task.IsCompleted).ThenBy(task => task.DisplayOrder));
    }

    private int NextActiveOrder()
    {
        return Tasks.Where(task => !task.IsCompleted)
            .Select(task => task.DisplayOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;
    }

    private void NormalizeDisplayOrder()
    {
        var order = 0;
        foreach (var task in Tasks.Where(task => !task.IsCompleted).OrderBy(task => task.DisplayOrder))
        {
            task.DisplayOrder = order++;
        }
    }

    private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (TaskItem item in e.NewItems)
            {
                AttachTask(item);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (TaskItem item in e.OldItems)
            {
                DetachTask(item);
            }
        }
    }

    private void AttachTask(TaskItem task)
    {
        task.PropertyChanged -= OnTaskPropertyChanged;
        task.PropertyChanged += OnTaskPropertyChanged;
    }

    private void DetachTask(TaskItem task)
    {
        task.PropertyChanged -= OnTaskPropertyChanged;
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TaskItem.Title))
        {
            _ = PersistAsync();
        }
    }

    private void RefreshViews()
    {
        ActiveTasks.Refresh();
        CompletedTasks.Refresh();
        OnPropertyChanged(nameof(ActiveTasks));
        OnPropertyChanged(nameof(CompletedTasks));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(ActiveCount));
    }

    public int CompletedCount => Tasks.Count(task => task.IsCompleted);

    public int ActiveCount => Tasks.Count(task => !task.IsCompleted);

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
