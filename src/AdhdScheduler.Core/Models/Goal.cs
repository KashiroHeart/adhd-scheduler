namespace AdhdScheduler.Core.Models;

public class Goal
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
