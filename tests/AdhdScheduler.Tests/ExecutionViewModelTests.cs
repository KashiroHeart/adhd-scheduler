using AdhdScheduler.Core.ViewModels;

namespace AdhdScheduler.Tests;

public class ExecutionViewModelTests : IDisposable
{
    private readonly TestStore _test = new();
    private readonly FakeWindowService _windows = new();

    private Core.Data.AppStore Store => _test.Store;

    public void Dispose() => _test.Dispose();

    private ExecutionViewModel CreateViewModel() => new(Store, _windows);

    [Fact]
    public void NoActiveGoal_State()
    {
        Store.AddGoal("目標");

        var vm = CreateViewModel();

        Assert.Equal(ExecutionState.NoActiveGoal, vm.State);
        Assert.True(vm.IsNoActiveGoal);
        Assert.False(vm.HasStep);
        Assert.False(vm.ShowStepButtons);
    }

    [Fact]
    public void Empty_State_WhenActiveGoalHasNoSteps()
    {
        var goal = Store.AddGoal("目標");
        Store.SetActiveGoal(goal.Id);

        var vm = CreateViewModel();

        Assert.Equal(ExecutionState.Empty, vm.State);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void HasStep_ShowsPurposeAndReason()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "机の上を片付ける", "作業場所を確保するため");
        Store.SetActiveGoal(goal.Id);

        var vm = CreateViewModel();

        Assert.Equal(ExecutionState.HasStep, vm.State);
        Assert.Equal("机の上を片付ける", vm.Purpose);
        Assert.Equal("作業場所を確保するため", vm.Reason);
        Assert.True(vm.ShowStepButtons);
    }

    [Fact]
    public void Complete_ShowsNextStep_ThenEmpty()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "ra");
        Store.AppendStep(goal.Id, "b", "rb");
        Store.SetActiveGoal(goal.Id);
        var vm = CreateViewModel();

        vm.CompleteCommand.Execute(null);
        Assert.Equal("b", vm.Purpose);
        Assert.Equal("rb", vm.Reason);

        vm.CompleteCommand.Execute(null);
        Assert.Equal(ExecutionState.Empty, vm.State);
        Assert.Equal("", vm.Purpose);
        Assert.Equal("", vm.Reason);
    }

    [Fact]
    public void Precursor_RequiresBothFields()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        Store.SetActiveGoal(goal.Id);
        var vm = CreateViewModel();

        vm.BeginPrecursorCommand.Execute(null);
        Assert.True(vm.IsAddingPrecursor);
        Assert.False(vm.ShowStepButtons);
        Assert.False(vm.ConfirmPrecursorCommand.CanExecute(null));

        vm.NewPurpose = "x";
        Assert.False(vm.ConfirmPrecursorCommand.CanExecute(null));
        vm.NewReason = "  ";
        Assert.False(vm.ConfirmPrecursorCommand.CanExecute(null));
        vm.NewReason = "rx";
        Assert.True(vm.ConfirmPrecursorCommand.CanExecute(null));
    }

    [Fact]
    public void Precursor_Cancel_KeepsCurrentStep()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        Store.SetActiveGoal(goal.Id);
        var vm = CreateViewModel();

        vm.BeginPrecursorCommand.Execute(null);
        vm.NewPurpose = "x";
        vm.NewReason = "rx";
        vm.CancelPrecursorCommand.Execute(null);

        Assert.False(vm.IsAddingPrecursor);
        Assert.True(vm.ShowStepButtons);
        Assert.Equal("a", vm.Purpose);
        Assert.Single(Store.GetQueuedSteps(goal.Id));
    }

    [Fact]
    public void Precursor_Confirm_ShowsNewStep_ThenOriginalAfterCompletion()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "ra");
        Store.SetActiveGoal(goal.Id);
        var vm = CreateViewModel();

        vm.BeginPrecursorCommand.Execute(null);
        vm.NewPurpose = "x";
        vm.NewReason = "rx";
        vm.ConfirmPrecursorCommand.Execute(null);

        Assert.False(vm.IsAddingPrecursor);
        Assert.Equal("x", vm.Purpose);
        Assert.Equal("rx", vm.Reason);

        vm.BeginPrecursorCommand.Execute(null);
        vm.NewPurpose = "y";
        vm.NewReason = "ry";
        vm.ConfirmPrecursorCommand.Execute(null);
        Assert.Equal("y", vm.Purpose);

        vm.CompleteCommand.Execute(null);
        Assert.Equal("x", vm.Purpose);
        vm.CompleteCommand.Execute(null);
        Assert.Equal("a", vm.Purpose);
        Assert.Equal("ra", vm.Reason);
    }

    [Fact]
    public void BeginPrecursor_DoesNothing_WithoutStep()
    {
        var vm = CreateViewModel();
        vm.BeginPrecursorCommand.Execute(null);
        Assert.False(vm.IsAddingPrecursor);
    }

    [Fact]
    public void OpenPlanning_UsesWindowService()
    {
        var vm = CreateViewModel();
        vm.OpenPlanningCommand.Execute(null);
        Assert.Equal(1, _windows.PlanningOpened);
    }

    [Fact]
    public void Refresh_ReflectsActiveGoalChange()
    {
        var g1 = Store.AddGoal("1");
        var g2 = Store.AddGoal("2");
        Store.AppendStep(g1.Id, "a", "r");
        Store.AppendStep(g2.Id, "p", "r");
        Store.SetActiveGoal(g1.Id);
        var vm = CreateViewModel();
        vm.BeginPrecursorCommand.Execute(null);

        Store.SetActiveGoal(g2.Id);
        vm.Refresh();

        Assert.Equal("p", vm.Purpose);
        Assert.False(vm.IsAddingPrecursor);
    }
}
