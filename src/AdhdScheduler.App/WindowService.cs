using System.Linq;
using System.Windows;
using AdhdScheduler.App.Interop;
using AdhdScheduler.App.Views;
using AdhdScheduler.Core.Data;
using AdhdScheduler.Core.ViewModels;

namespace AdhdScheduler.App;

/// <summary>
/// ウィンドウの表示・非表示をまとめて扱う。
/// 計画画面を開いている間は実行画面を隠し、閉じたら最新の状態で再表示する。
/// </summary>
internal sealed class WindowService : IWindowService
{
    private readonly AppStore _store;
    private readonly ExecutionViewModel _executionViewModel;
    private readonly ExecutionWindow _execution;
    private PlanningViewModel? _planningViewModel;
    private PlanningWindow? _planning;
    private LaterListViewModel? _laterListViewModel;
    private LaterListWindow? _laterList;
    private QuickAddWindow? _quickAdd;
    private bool _shuttingDown;

    public WindowService(AppStore store)
    {
        _store = store;
        _executionViewModel = new ExecutionViewModel(store, this);
        _execution = new ExecutionWindow { DataContext = _executionViewModel };
        _execution.Closing += (_, e) =>
        {
            if (!_shuttingDown)
            {
                e.Cancel = true;
                _execution.Hide();
            }
        };
    }

    public void ShowExecution()
    {
        _executionViewModel.Refresh();
        _execution.Show();
        _execution.Activate();
    }

    public void OpenPlanning()
    {
        if (_planning is not null)
        {
            _planning.Activate();
            return;
        }

        _planningViewModel = new PlanningViewModel(_store, this);
        _planning = new PlanningWindow { DataContext = _planningViewModel };
        _planning.Closed += (_, _) =>
        {
            _planning = null;
            _planningViewModel = null;
            if (!_shuttingDown)
            {
                ShowExecution();
            }
        };
        _execution.Hide();
        _planning.Show();
        _planning.Activate();
    }

    public void OpenLaterList()
    {
        if (_planning is null)
        {
            return;
        }
        if (_laterList is not null)
        {
            _laterList.Activate();
            return;
        }

        _laterListViewModel = new LaterListViewModel(_store);
        _laterListViewModel.StepsChanged += (_, _) => _planningViewModel?.Refresh();
        _laterList = new LaterListWindow { DataContext = _laterListViewModel, Owner = _planning };
        _laterList.Activated += (_, _) => _laterListViewModel?.Refresh();
        _laterList.Closed += (_, _) =>
        {
            _laterList = null;
            _laterListViewModel = null;
        };
        _laterList.Show();
    }

    public void ShowQuickAdd()
    {
        if (_quickAdd is not null)
        {
            _quickAdd.Activate();
            return;
        }

        var previous = NativeMethods.GetForegroundWindow();
        _quickAdd = new QuickAddWindow(new QuickAddViewModel(_store), previous);
        _quickAdd.Closed += (_, _) =>
        {
            _quickAdd = null;
            _laterListViewModel?.Refresh();
        };
        _quickAdd.Show();
    }

    public bool Confirm(string message)
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var result = owner is null
            ? MessageBox.Show(message, "確認", MessageBoxButton.YesNo)
            : MessageBox.Show(owner, message, "確認", MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }

    public void PrepareShutdown() => _shuttingDown = true;
}
