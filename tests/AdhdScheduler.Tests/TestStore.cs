using AdhdScheduler.Core.Data;
using AdhdScheduler.Core.ViewModels;

namespace AdhdScheduler.Tests;

/// <summary>一時ファイルの DB を使う AppStore。時刻は呼ぶたびに1秒進む。</summary>
public sealed class TestStore : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"adhd-scheduler-test-{Guid.NewGuid():N}.db");
    private DateTime _now = new(2026, 1, 1, 9, 0, 0);

    public TestStore()
    {
        Store = new AppStore(_path, () => _now = _now.AddSeconds(1));
        Store.Initialize();
    }

    public AppStore Store { get; }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}

public sealed class FakeWindowService : IWindowService
{
    public int PlanningOpened { get; private set; }

    public int LaterListOpened { get; private set; }

    public bool ConfirmResult { get; set; } = true;

    public List<string> Confirmations { get; } = new();

    public void OpenPlanning() => PlanningOpened++;

    public void OpenLaterList() => LaterListOpened++;

    public bool Confirm(string message)
    {
        Confirmations.Add(message);
        return ConfirmResult;
    }
}
