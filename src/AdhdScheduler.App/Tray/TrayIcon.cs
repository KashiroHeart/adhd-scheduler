using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdhdScheduler.App.Tray;

/// <summary>タスクトレイ常駐。メニューは「実行画面を開く／計画画面を開く／終了」のみ。</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action openExecution, Action openPlanning, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("実行画面を開く", null, (_, _) => openExecution());
        menu.Items.Add("計画画面を開く", null, (_, _) => openPlanning());
        menu.Items.Add("終了", null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AdhdScheduler",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
