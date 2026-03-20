using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DailyToDoList.Models;

public class TaskItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private bool _isCompleted;
    private int _displayOrder;
    private DateTimeOffset? _completedAt;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isCompleted, value);
    }

    public int DisplayOrder
    {
        get => _displayOrder;
        set => SetField(ref _displayOrder, value);
    }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? CompletedAt
    {
        get => _completedAt;
        set => SetField(ref _completedAt, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
