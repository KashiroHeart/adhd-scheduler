using System.Collections.ObjectModel;
using AdhdScheduler.Core.Data;
using AdhdScheduler.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdhdScheduler.Core.ViewModels;

/// <summary>画面C：後でやる一覧。</summary>
public partial class LaterListViewModel : ObservableObject
{
    private readonly AppStore _store;

    public LaterListViewModel(AppStore store)
    {
        _store = store;
        Refresh();
    }

    /// <summary>タスク化で1手が追加されたとき。</summary>
    public event EventHandler? StepsChanged;

    public ObservableCollection<LaterItem> Items { get; } = new();

    public ObservableCollection<Goal> Goals { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTaskifying))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmTaskifyCommand))]
    private LaterItem? _taskifyTarget;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmTaskifyCommand))]
    private Goal? _taskifyGoal;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmTaskifyCommand))]
    private string _taskifyPurpose = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmTaskifyCommand))]
    private string _taskifyReason = "";

    public bool IsTaskifying => TaskifyTarget is not null;

    public void Refresh()
    {
        var selectedGoalId = TaskifyGoal?.Id;
        Goals.Clear();
        foreach (var goal in _store.GetGoals())
        {
            Goals.Add(goal);
        }
        TaskifyGoal = Goals.FirstOrDefault(g => g.Id == selectedGoalId);

        var targetId = TaskifyTarget?.Id;
        Items.Clear();
        foreach (var item in _store.GetLaterItems())
        {
            Items.Add(item);
        }
        TaskifyTarget = Items.FirstOrDefault(i => i.Id == targetId);
    }

    [RelayCommand]
    private void Delete(LaterItem? item)
    {
        if (item is null)
        {
            return;
        }
        _store.DeleteLaterItem(item.Id);
        Refresh();
    }

    [RelayCommand]
    private void BeginTaskify(LaterItem? item)
    {
        TaskifyTarget = item;
        TaskifyGoal = null;
        TaskifyPurpose = "";
        TaskifyReason = "";
    }

    private bool CanConfirmTaskify() =>
        TaskifyTarget is not null
        && TaskifyGoal is not null
        && !string.IsNullOrWhiteSpace(TaskifyPurpose)
        && !string.IsNullOrWhiteSpace(TaskifyReason);

    [RelayCommand(CanExecute = nameof(CanConfirmTaskify))]
    private void ConfirmTaskify()
    {
        if (!CanConfirmTaskify())
        {
            return;
        }
        _store.TaskifyLaterItem(TaskifyTarget!.Id, TaskifyGoal!.Id, TaskifyPurpose, TaskifyReason);
        CancelTaskify();
        Refresh();
        StepsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelTaskify() => BeginTaskify(null);
}
