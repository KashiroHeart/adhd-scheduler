using AdhdScheduler.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdhdScheduler.Core.ViewModels;

public enum ExecutionState
{
    NoActiveGoal,
    Empty,
    HasStep,
}

/// <summary>画面A：実行画面。</summary>
public partial class ExecutionViewModel : ObservableObject
{
    private readonly AppStore _store;
    private readonly IWindowService _windows;

    public ExecutionViewModel(AppStore store, IWindowService windows)
    {
        _store = store;
        _windows = windows;
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStep), nameof(IsEmpty), nameof(IsNoActiveGoal), nameof(ShowStepButtons))]
    private ExecutionState _state;

    [ObservableProperty]
    private string _purpose = "";

    [ObservableProperty]
    private string _reason = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStepButtons))]
    private bool _isAddingPrecursor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmPrecursorCommand))]
    private string _newPurpose = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmPrecursorCommand))]
    private string _newReason = "";

    public Guid? CurrentStepId { get; private set; }

    public bool HasStep => State == ExecutionState.HasStep;

    public bool IsEmpty => State == ExecutionState.Empty;

    public bool IsNoActiveGoal => State == ExecutionState.NoActiveGoal;

    public bool ShowStepButtons => HasStep && !IsAddingPrecursor;

    public void Refresh()
    {
        var hasActiveGoal = _store.GetActiveGoalId() is not null;
        var step = hasActiveGoal ? _store.GetNextStep() : null;
        if (step?.Id != CurrentStepId)
        {
            CancelPrecursor();
        }

        CurrentStepId = step?.Id;
        Purpose = step?.Purpose ?? "";
        Reason = step?.Reason ?? "";
        State = step is not null ? ExecutionState.HasStep
            : hasActiveGoal ? ExecutionState.Empty
            : ExecutionState.NoActiveGoal;
    }

    [RelayCommand]
    private void Complete()
    {
        if (CurrentStepId is not Guid id)
        {
            return;
        }
        _store.CompleteStep(id);
        Refresh();
    }

    [RelayCommand]
    private void BeginPrecursor()
    {
        if (!HasStep)
        {
            return;
        }
        NewPurpose = "";
        NewReason = "";
        IsAddingPrecursor = true;
    }

    private bool CanConfirmPrecursor() =>
        !string.IsNullOrWhiteSpace(NewPurpose) && !string.IsNullOrWhiteSpace(NewReason);

    [RelayCommand(CanExecute = nameof(CanConfirmPrecursor))]
    private void ConfirmPrecursor()
    {
        if (CurrentStepId is not Guid id || !CanConfirmPrecursor())
        {
            return;
        }
        _store.InsertBefore(id, NewPurpose, NewReason);
        CancelPrecursor();
        Refresh();
    }

    [RelayCommand]
    private void CancelPrecursor()
    {
        IsAddingPrecursor = false;
        NewPurpose = "";
        NewReason = "";
    }

    [RelayCommand]
    private void OpenPlanning() => _windows.OpenPlanning();
}
