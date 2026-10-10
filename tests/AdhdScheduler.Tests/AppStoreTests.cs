using AdhdScheduler.Core.Models;

namespace AdhdScheduler.Tests;

public class AppStoreTests : IDisposable
{
    private readonly TestStore _test = new();

    private Core.Data.AppStore Store => _test.Store;

    public void Dispose() => _test.Dispose();

    private List<string> QueuedPurposes(Guid goalId) =>
        Store.GetQueuedSteps(goalId).Select(s => s.Purpose).ToList();

    private void AssertContiguousOrder(Guid goalId) =>
        Assert.Equal(
            Enumerable.Range(0, Store.GetQueuedSteps(goalId).Count),
            Store.GetQueuedSteps(goalId).Select(s => s.QueueOrder));

    [Fact]
    public void Initialize_CanRunTwice()
    {
        Store.Initialize();
        Assert.Empty(Store.GetGoals());
        Assert.Null(Store.GetActiveGoalId());
    }

    [Fact]
    public void GetNextStep_ReturnsNull_WhenNoActiveGoal()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        Assert.Null(Store.GetNextStep());
    }

    [Fact]
    public void GetNextStep_ReturnsFirstQueuedStepOfActiveGoal()
    {
        var other = Store.AddGoal("別");
        Store.AppendStep(other.Id, "other", "r");
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        Store.AppendStep(goal.Id, "b", "r");
        Store.SetActiveGoal(goal.Id);

        Assert.Equal("a", Store.GetNextStep()!.Purpose);
    }

    [Fact]
    public void CompleteStep_AdvancesToNextStep_AndRecordsCompletion()
    {
        var goal = Store.AddGoal("目標");
        var a = Store.AppendStep(goal.Id, "a", "r");
        Store.AppendStep(goal.Id, "b", "r");
        Store.SetActiveGoal(goal.Id);

        Store.CompleteStep(a.Id);

        Assert.Equal("b", Store.GetNextStep()!.Purpose);
        var done = Store.GetStep(a.Id)!;
        Assert.Equal(StepStatus.Done, done.Status);
        Assert.NotNull(done.CompletedAt);
    }

    [Fact]
    public void CompleteStep_LastStep_LeavesQueueEmpty()
    {
        var goal = Store.AddGoal("目標");
        var a = Store.AppendStep(goal.Id, "a", "r");
        Store.SetActiveGoal(goal.Id);

        Store.CompleteStep(a.Id);

        Assert.Null(Store.GetNextStep());
        Assert.Empty(Store.GetQueuedSteps(goal.Id));
    }

    [Fact]
    public void AppendStep_AddsToEnd_WithContiguousOrder()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        Store.AppendStep(goal.Id, "b", "r");
        var c = Store.AppendStep(goal.Id, "c", "r");

        Assert.Equal(new[] { "a", "b", "c" }, QueuedPurposes(goal.Id));
        Assert.Equal(2, c.QueueOrder);
        AssertContiguousOrder(goal.Id);
    }

    [Fact]
    public void AppendStep_TrimsInput()
    {
        var goal = Store.AddGoal("  目標  ");
        var step = Store.AppendStep(goal.Id, "  a  ", "\n r \n");

        Assert.Equal("目標", Store.GetGoals().Single().Title);
        Assert.Equal("a", step.Purpose);
        Assert.Equal("r", Store.GetStep(step.Id)!.Reason);
    }

    [Theory]
    [InlineData("", "r")]
    [InlineData("a", "")]
    [InlineData("  ", "r")]
    [InlineData("a", " \n ")]
    public void AppendStep_RejectsEmptyPurposeOrReason(string purpose, string reason)
    {
        var goal = Store.AddGoal("目標");
        Assert.Throws<ArgumentException>(() => Store.AppendStep(goal.Id, purpose, reason));
        Assert.Empty(Store.GetQueuedSteps(goal.Id));
    }

    [Fact]
    public void AddGoal_RejectsEmptyTitle()
    {
        Assert.Throws<ArgumentException>(() => Store.AddGoal(" "));
    }

    [Fact]
    public void InsertBefore_PutsNewStepImmediatelyBefore()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        var b = Store.AppendStep(goal.Id, "b", "r");
        Store.AppendStep(goal.Id, "c", "r");

        var x = Store.InsertBefore(b.Id, "x", "r");

        Assert.Equal(new[] { "a", "x", "b", "c" }, QueuedPurposes(goal.Id));
        Assert.Equal(1, x.QueueOrder);
        AssertContiguousOrder(goal.Id);
    }

    [Fact]
    public void InsertBefore_CanBeStacked_AndOriginalReturnsAfterward()
    {
        var goal = Store.AddGoal("目標");
        var a = Store.AppendStep(goal.Id, "a", "r");
        Store.AppendStep(goal.Id, "b", "r");
        Store.SetActiveGoal(goal.Id);

        var x = Store.InsertBefore(a.Id, "x", "r");
        var y = Store.InsertBefore(x.Id, "y", "r");

        Assert.Equal(new[] { "y", "x", "a", "b" }, QueuedPurposes(goal.Id));
        Assert.Equal(y.Id, Store.GetNextStep()!.Id);
        Store.CompleteStep(y.Id);
        Assert.Equal(x.Id, Store.GetNextStep()!.Id);
        Store.CompleteStep(x.Id);
        Assert.Equal(a.Id, Store.GetNextStep()!.Id);
        AssertContiguousOrder(goal.Id);
    }

    [Fact]
    public void InsertBefore_RejectsEmptyInput()
    {
        var goal = Store.AddGoal("目標");
        var a = Store.AppendStep(goal.Id, "a", "r");

        Assert.Throws<ArgumentException>(() => Store.InsertBefore(a.Id, "x", ""));
        Assert.Equal(new[] { "a" }, QueuedPurposes(goal.Id));
    }

    [Fact]
    public void InsertBefore_RejectsCompletedStep()
    {
        var goal = Store.AddGoal("目標");
        var a = Store.AppendStep(goal.Id, "a", "r");
        Store.CompleteStep(a.Id);

        Assert.Throws<InvalidOperationException>(() => Store.InsertBefore(a.Id, "x", "r"));
    }

    [Fact]
    public void MoveStep_Reorders_WithContiguousOrder()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        Store.AppendStep(goal.Id, "b", "r");
        var c = Store.AppendStep(goal.Id, "c", "r");

        Assert.True(Store.MoveStep(c.Id, -1));
        Assert.Equal(new[] { "a", "c", "b" }, QueuedPurposes(goal.Id));
        Assert.True(Store.MoveStep(c.Id, -1));
        Assert.Equal(new[] { "c", "a", "b" }, QueuedPurposes(goal.Id));
        Assert.False(Store.MoveStep(c.Id, -1));
        Assert.Equal(new[] { "c", "a", "b" }, QueuedPurposes(goal.Id));
        AssertContiguousOrder(goal.Id);
    }

    [Fact]
    public void MoveStep_PastEnd_ReturnsFalse()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        var b = Store.AppendStep(goal.Id, "b", "r");

        Assert.False(Store.MoveStep(b.Id, 1));
        Assert.Equal(new[] { "a", "b" }, QueuedPurposes(goal.Id));
    }

    [Fact]
    public void DeleteStep_RenumbersRemaining()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        var b = Store.AppendStep(goal.Id, "b", "r");
        Store.AppendStep(goal.Id, "c", "r");

        Store.DeleteStep(b.Id);

        Assert.Equal(new[] { "a", "c" }, QueuedPurposes(goal.Id));
        AssertContiguousOrder(goal.Id);
    }

    [Fact]
    public void UpdateStep_ChangesText_AndRejectsEmpty()
    {
        var goal = Store.AddGoal("目標");
        var a = Store.AppendStep(goal.Id, "a", "r");

        Store.UpdateStep(a.Id, "a2", "r2");
        Assert.Throws<ArgumentException>(() => Store.UpdateStep(a.Id, "", "r3"));

        var step = Store.GetStep(a.Id)!;
        Assert.Equal("a2", step.Purpose);
        Assert.Equal("r2", step.Reason);
    }

    [Fact]
    public void QueueOperations_DoNotAffectOtherGoals()
    {
        var g1 = Store.AddGoal("1");
        var g2 = Store.AddGoal("2");
        var a = Store.AppendStep(g1.Id, "a", "r");
        Store.AppendStep(g2.Id, "p", "r");
        Store.AppendStep(g2.Id, "q", "r");

        Store.InsertBefore(a.Id, "x", "r");

        Assert.Equal(new[] { "p", "q" }, QueuedPurposes(g2.Id));
        AssertContiguousOrder(g2.Id);
    }

    [Fact]
    public void RenameGoal_ChangesTitle()
    {
        var goal = Store.AddGoal("目標");
        Store.RenameGoal(goal.Id, "新しい名前");
        Assert.Equal("新しい名前", Store.GetGoals().Single().Title);
    }

    [Fact]
    public void DeleteGoal_RemovesSteps_AndClearsActiveGoal()
    {
        var goal = Store.AddGoal("目標");
        var a = Store.AppendStep(goal.Id, "a", "r");
        Store.SetActiveGoal(goal.Id);

        Store.DeleteGoal(goal.Id);

        Assert.Empty(Store.GetGoals());
        Assert.Null(Store.GetStep(a.Id));
        Assert.Null(Store.GetActiveGoalId());
    }

    [Fact]
    public void DeleteGoal_KeepsActiveGoal_WhenAnotherGoalDeleted()
    {
        var active = Store.AddGoal("取り組み中");
        var other = Store.AddGoal("別");
        Store.SetActiveGoal(active.Id);

        Store.DeleteGoal(other.Id);

        Assert.Equal(active.Id, Store.GetActiveGoalId());
    }

    [Fact]
    public void LaterItems_AreReturnedOldestFirst()
    {
        Store.AddLaterItem("1");
        Store.AddLaterItem("2");
        Store.AddLaterItem("3");

        Assert.Equal(new[] { "1", "2", "3" }, Store.GetLaterItems().Select(i => i.Text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLaterItem_IgnoresEmptyText(string? text)
    {
        Assert.Null(Store.AddLaterItem(text));
        Assert.Empty(Store.GetLaterItems());
    }

    [Fact]
    public void DeleteLaterItem_RemovesItem()
    {
        var item = Store.AddLaterItem("メモ")!;
        Store.DeleteLaterItem(item.Id);
        Assert.Empty(Store.GetLaterItems());
    }

    [Fact]
    public void TaskifyLaterItem_AppendsStep_AndRemovesItem()
    {
        var goal = Store.AddGoal("目標");
        Store.AppendStep(goal.Id, "a", "r");
        var item = Store.AddLaterItem("メモ")!;

        Store.TaskifyLaterItem(item.Id, goal.Id, "絵を描く", "メモを形にするため");

        Assert.Equal(new[] { "a", "絵を描く" }, QueuedPurposes(goal.Id));
        Assert.Empty(Store.GetLaterItems());
    }

    [Fact]
    public void TaskifyLaterItem_KeepsItem_WhenInputInvalid()
    {
        var goal = Store.AddGoal("目標");
        var item = Store.AddLaterItem("メモ")!;

        Assert.Throws<ArgumentException>(() => Store.TaskifyLaterItem(item.Id, goal.Id, "a", " "));

        Assert.Single(Store.GetLaterItems());
        Assert.Empty(Store.GetQueuedSteps(goal.Id));
    }
}
