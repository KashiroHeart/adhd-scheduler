namespace AdhdScheduler.Core.Models;

public enum StepStatus
{
    Queued = 0,
    Done = 1,
}

public class Step
{
    public Guid Id { get; set; }
    public Guid GoalId { get; set; }
    public string Purpose { get; set; } = "";
    public string Reason { get; set; } = "";
    public int QueueOrder { get; set; }
    public StepStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
