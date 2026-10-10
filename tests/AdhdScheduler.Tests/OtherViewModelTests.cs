using AdhdScheduler.Core.ViewModels;

namespace AdhdScheduler.Tests;

public class QuickAddViewModelTests : IDisposable
{
    private readonly TestStore _test = new();

    public void Dispose() => _test.Dispose();

    [Fact]
    public void Save_StoresText()
    {
        var vm = new QuickAddViewModel(_test.Store) { Text = "絵を描きたい" };
        vm.Save();
        Assert.Equal("絵を描きたい", _test.Store.GetLaterItems().Single().Text);
    }

    [Fact]
    public void Save_IgnoresEmptyText()
    {
        var vm = new QuickAddViewModel(_test.Store) { Text = "  " };
        vm.Save();
        Assert.Empty(_test.Store.GetLaterItems());
    }
}

public class LaterListViewModelTests : IDisposable
{
    private readonly TestStore _test = new();

    private Core.Data.AppStore Store => _test.Store;

    public void Dispose() => _test.Dispose();

    [Fact]
    public void Items_AreOldestFirst()
    {
        Store.AddLaterItem("1");
        Store.AddLaterItem("2");

        var vm = new LaterListViewModel(Store);

        Assert.Equal(new[] { "1", "2" }, vm.Items.Select(i => i.Text));
    }

    [Fact]
    public void Delete_RemovesItem()
    {
        Store.AddLaterItem("1");
        var vm = new LaterListViewModel(Store);

        vm.DeleteCommand.Execute(vm.Items[0]);

        Assert.Empty(vm.Items);
        Assert.Empty(Store.GetLaterItems());
    }

    [Fact]
    public void Taskify_RequiresGoalPurposeAndReason()
    {
        var goal = Store.AddGoal("目標");
        Store.AddLaterItem("メモ");
        var vm = new LaterListViewModel(Store);

        vm.BeginTaskifyCommand.Execute(vm.Items[0]);
        Assert.True(vm.IsTaskifying);
        Assert.False(vm.ConfirmTaskifyCommand.CanExecute(null));

        vm.TaskifyPurpose = "a";
        vm.TaskifyReason = "r";
        Assert.False(vm.ConfirmTaskifyCommand.CanExecute(null));

        vm.TaskifyGoal = vm.Goals.Single(g => g.Id == goal.Id);
        Assert.True(vm.ConfirmTaskifyCommand.CanExecute(null));

        vm.TaskifyReason = "";
        Assert.False(vm.ConfirmTaskifyCommand.CanExecute(null));
    }

    [Fact]
    public void Taskify_AppendsStep_RemovesItem_AndRaisesEvent()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "既存", "r");
        Store.AddLaterItem("メモ");
        var vm = new LaterListViewModel(Store);
        var raised = 0;
        vm.StepsChanged += (_, _) => raised++;

        vm.BeginTaskifyCommand.Execute(vm.Items[0]);
        vm.TaskifyGoal = vm.Goals[0];
        vm.TaskifyPurpose = "新しい1手";
        vm.TaskifyReason = "理由";
        vm.ConfirmTaskifyCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.False(vm.IsTaskifying);
        Assert.Equal(new[] { "既存", "新しい1手" }, Store.GetQueuedSteps(goal.Id).Select(s => s.Purpose));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void CancelTaskify_KeepsItem()
    {
        Store.AddGoal("目標");
        Store.AddLaterItem("メモ");
        var vm = new LaterListViewModel(Store);

        vm.BeginTaskifyCommand.Execute(vm.Items[0]);
        vm.CancelTaskifyCommand.Execute(null);

        Assert.False(vm.IsTaskifying);
        Assert.Single(vm.Items);
    }
}

public class PlanningViewModelTests : IDisposable
{
    private readonly TestStore _test = new();
    private readonly FakeWindowService _windows = new();

    private Core.Data.AppStore Store => _test.Store;

    public void Dispose() => _test.Dispose();

    private PlanningViewModel CreateViewModel() => new(Store, _windows);

    [Fact]
    public void AddGoal_SelectsNewGoal()
    {
        var vm = CreateViewModel();
        Assert.False(vm.AddGoalCommand.CanExecute(null));

        vm.NewGoalTitle = "目標";
        vm.AddGoalCommand.Execute(null);

        Assert.Equal("目標", vm.SelectedGoal!.Title);
        Assert.Equal("", vm.NewGoalTitle);
    }

    [Fact]
    public void SetActiveGoal_MarksGoal()
    {
        Store.AddGoal("1");
        Store.AddGoal("2");
        var vm = CreateViewModel();

        vm.SelectedGoal = vm.Goals[1];
        vm.SetActiveGoalCommand.Execute(null);

        Assert.Equal(vm.Goals[1].Id, Store.GetActiveGoalId());
        Assert.False(vm.Goals[0].IsActive);
        Assert.True(vm.Goals[1].IsActive);
    }

    [Fact]
    public void InitialSelection_IsActiveGoal()
    {
        Store.AddGoal("1");
        var g2 = Store.AddGoal("2");
        Store.SetActiveGoal(g2.Id);

        var vm = CreateViewModel();

        Assert.Equal(g2.Id, vm.SelectedGoal!.Id);
    }

    [Fact]
    public void AppendStep_RequiresPurposeAndReason()
    {
        Store.AddGoal("目標");
        var vm = CreateViewModel();

        vm.EditPurpose = "a";
        Assert.False(vm.AppendStepCommand.CanExecute(null));
        vm.EditReason = "r";
        Assert.True(vm.AppendStepCommand.CanExecute(null));

        vm.AppendStepCommand.Execute(null);

        Assert.Single(vm.Steps);
        Assert.Equal(1, vm.Steps[0].Number);
        Assert.Equal("", vm.EditPurpose);
        Assert.Equal("", vm.EditReason);
    }

    [Fact]
    public void SelectStep_LoadsEditor_AndSaveUpdates()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        var vm = CreateViewModel();

        vm.SelectedStep = vm.Steps[0];
        Assert.Equal("a", vm.EditPurpose);
        Assert.Equal("r", vm.EditReason);

        vm.EditPurpose = "a2";
        vm.SaveStepCommand.Execute(null);

        Assert.Equal("a2", Store.GetQueuedSteps(goal.Id).Single().Purpose);
        Assert.Equal("a2", vm.SelectedStep!.Purpose);
    }

    [Fact]
    public void MoveStep_KeepsSelection()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        Store.AppendStep(goal.Id, "b", "r");
        var vm = CreateViewModel();

        vm.SelectedStep = vm.Steps[1];
        vm.MoveStepUpCommand.Execute(null);

        Assert.Equal(new[] { "b", "a" }, vm.Steps.Select(s => s.Purpose));
        Assert.Equal("b", vm.SelectedStep!.Purpose);
        Assert.Equal(new[] { 1, 2 }, vm.Steps.Select(s => s.Number));
    }

    [Fact]
    public void DeleteStep_AsksForConfirmation()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        var vm = CreateViewModel();
        vm.SelectedStep = vm.Steps[0];

        _windows.ConfirmResult = false;
        vm.DeleteStepCommand.Execute(null);
        Assert.Single(vm.Steps);

        _windows.ConfirmResult = true;
        vm.DeleteStepCommand.Execute(null);
        Assert.Empty(vm.Steps);
    }

    [Fact]
    public void DeleteGoal_ClearsActiveGoal()
    {
        var goal = Store.AddGoal("目標");
        Store.SetActiveGoal(goal.Id);
        var vm = CreateViewModel();

        vm.DeleteGoalCommand.Execute(null);

        Assert.Empty(vm.Goals);
        Assert.Null(vm.SelectedGoal);
        Assert.Null(Store.GetActiveGoalId());
    }

    [Fact]
    public void RenameGoal_UpdatesTitle()
    {
        Store.AddGoal("目標");
        var vm = CreateViewModel();

        vm.EditGoalTitle = "新";
        vm.RenameGoalCommand.Execute(null);

        Assert.Equal("新", vm.SelectedGoal!.Title);
    }

    [Fact]
    public void OpenLaterList_UsesWindowService()
    {
        var vm = CreateViewModel();
        vm.OpenLaterListCommand.Execute(null);
        Assert.Equal(1, _windows.LaterListOpened);
    }
}
