using System.Windows;
using AdhdScheduler.App.Interop;
using AdhdScheduler.App.Tray;
using AdhdScheduler.Core.Data;

namespace AdhdScheduler.App;

public partial class App : Application
{
    private WindowService? _windows;
    private TrayIcon? _tray;
    private GlobalHotKey? _hotKey;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var databasePath = AppStore.DefaultDatabasePath;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(databasePath)!);
        var store = new AppStore(databasePath);
        store.Initialize();

        _windows = new WindowService(store);
        _tray = new TrayIcon(_windows.ShowExecution, _windows.OpenPlanning, ExitApplication);

        _hotKey = new GlobalHotKey(_windows.ShowQuickAdd);
        if (!_hotKey.Register())
        {
            MessageBox.Show("ホットキー Ctrl+Shift+A を登録できませんでした", "AdhdScheduler");
        }

        _windows.ShowExecution();
    }

    private void ExitApplication()
    {
        _windows?.PrepareShutdown();
        _hotKey?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }
}
