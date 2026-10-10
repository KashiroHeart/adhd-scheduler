using System.Collections.ObjectModel;
using AdhdScheduler.Core.Data;
using AdhdScheduler.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdhdScheduler.Core.ViewModels;

public class GoalRow
{
    public GoalRow(Goal goal, bool isActive)
    {
        Goal = goal;
        IsActive = isActive;
    }

    public Goal Goal { get; }

    public Guid Id => Goal.Id;

    public string Title => Goal.Title;

    public bool IsActive { get; }
}

public class StepRow
{
    public StepRow(Step step, int number)
    {
        Step = step;
        Number = number;
    }

    public Step Step { get; }

    public Guid Id => Step.Id;

    public int Number { get; }

    public string Purpose => Step.Purpose;

    public string Reason => Step.Reason;
}

/// <summary>画面D：計画画面。</summary>
public partial class PlanningViewModel : ObservableObject
{
    private readonly AppStore _store;
    private readonly IWindowService _windows;

    public PlanningViewModel(AppStore store, IWindowService windows)
    {
        _store = store;
        _windows = windows;
        Refresh();
    }

    public ObservableCollection<GoalRow> Goals { get; } = new();

    public ObservableCollection<StepRow> Steps { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameGoalCommand), nameof(DeleteGoalCommand), nameof(SetActiveGoalCommand), nameof(AppendStepCommand))]
    private GoalRow? _selectedGoal;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGoalCommand))]
    private string _newGoalTitle = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameGoalCommand))]
    private string _editGoalTitle = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveStepCommand), nameof(DeleteStepCommand), nameof(MoveStepUpCommand), nameof(MoveStepDownCommand))]
    private StepRow? _selectedStep;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AppendStepCommand), nameof(SaveStepCommand))]
    private string _editPurpose = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AppendStepCommand), nameof(SaveStepCommand))]
    private string _editReason = "";

    public void Refresh() => Reload(SelectedGoal?.Id ?? _store.GetActiveGoalId(), SelectedStep?.Id);

    private void Reload(Guid? goalId, Guid? stepId)
    {
        var activeGoalId = _store.GetActiveGoalId();
        Goals.Clear();
        foreach (var goal in _store.GetGoals())
        {
            Goals.Add(new GoalRow(goal, goal.Id == activeGoalId));
        }
        SelectedGoal = Goals.FirstOrDefault(g => g.Id == goalId) ?? Goals.FirstOrDefault();
        LoadSteps(stepId);
    }

    partial void OnSelectedGoalChanged(GoalRow? value)
    {
        EditGoalTitle = value?.Title ?? "";
        LoadSteps(null);
    }

    partial void OnSelectedStepChanged(StepRow? value)
    {
        EditPurpose = value?.Purpose ?? "";
        EditReason = value?.Reason ?? "";
    }

    private void LoadSteps(Guid? stepId)
    {
        Steps.Clear();
        if (SelectedGoal is not null)
        {
            var number = 1;
            foreach (var step in _store.GetQueuedSteps(SelectedGoal.Id))
            {
                Steps.Add(new StepRow(step, number++));
            }
        }
        SelectedStep = Steps.FirstOrDefault(s => s.Id == stepId);
    }

    // ---- 目標 ----

    private bool CanAddGoal() => !string.IsNullOrWhiteSpace(NewGoalTitle);

    [RelayCommand(CanExecute = nameof(CanAddGoal))]
    private void AddGoal()
    {
        if (!CanAddGoal())
        {
            return;
        }
        var goal = _store.AddGoal(NewGoalTitle);
        NewGoalTitle = "";
        Reload(goal.Id, null);
    }

    private bool CanRenameGoal() => SelectedGoal is not null && !string.IsNullOrWhiteSpace(EditGoalTitle);

    [RelayCommand(CanExecute = nameof(CanRenameGoal))]
    private void RenameGoal()
    {
        if (!CanRenameGoal())
        {
            return;
        }
        _store.RenameGoal(SelectedGoal!.Id, EditGoalTitle);
        Refresh();
    }

    private bool HasSelectedGoal() => SelectedGoal is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedGoal))]
    private void DeleteGoal()
    {
        if (SelectedGoal is null
            || !_windows.Confirm($"目標「{SelectedGoal.Title}」と、その1手をすべて削除します。よろしいですか？"))
        {
            return;
        }
        _store.DeleteGoal(SelectedGoal.Id);
        Reload(null, null);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedGoal))]
    private void SetActiveGoal()
    {
        if (SelectedGoal is null)
        {
            return;
        }
        _store.SetActiveGoal(SelectedGoal.Id);
        Refresh();
    }

    // ---- 1手 ----

    private bool HasStepInput() => !string.IsNullOrWhiteSpace(EditPurpose) && !string.IsNullOrWhiteSpace(EditReason);

    private bool CanAppendStep() => SelectedGoal is not null && HasStepInput();

    [RelayCommand(CanExecute = nameof(CanAppendStep))]
    private void AppendStep()
    {
        if (!CanAppendStep())
        {
            return;
        }
        _store.AppendStep(SelectedGoal!.Id, EditPurpose, EditReason);
        LoadSteps(null);
        ClearEditor();
    }

    private bool CanSaveStep() => SelectedStep is not null && HasStepInput();

    [RelayCommand(CanExecute = nameof(CanSaveStep))]
    private void SaveStep()
    {
        if (!CanSaveStep())
        {
            return;
        }
        _store.UpdateStep(SelectedStep!.Id, EditPurpose, EditReason);
        LoadSteps(SelectedStep.Id);
    }

    private bool HasSelectedStep() => SelectedStep is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void DeleteStep()
    {
        if (SelectedStep is null || !_windows.Confirm("この1手を削除します。よろしいですか？"))
        {
            return;
        }
        _store.DeleteStep(SelectedStep.Id);
        LoadSteps(null);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void MoveStepUp() => MoveStep(-1);

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void MoveStepDown() => MoveStep(1);

    private void MoveStep(int offset)
    {
        if (SelectedStep is null)
        {
            return;
        }
        var id = SelectedStep.Id;
        _store.MoveStep(id, offset);
        LoadSteps(id);
    }

    [RelayCommand]
    private void ClearEditor()
    {
        SelectedStep = null;
        EditPurpose = "";
        EditReason = "";
    }

    [RelayCommand]
    private void OpenLaterList() => _windows.OpenLaterList();
}
