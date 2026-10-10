using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AdhdScheduler.App.Interop;
using AdhdScheduler.Core.ViewModels;

namespace AdhdScheduler.App.Views;

/// <summary>
/// 画面B：クイック登録。Enter で保存して閉じる、Esc で破棄して閉じる。
/// 閉じた後は直前にアクティブだったウィンドウへフォーカスを戻す。
/// </summary>
public partial class QuickAddWindow : Window
{
    private readonly QuickAddViewModel _viewModel;
    private readonly IntPtr _previousWindow;

    public QuickAddWindow(QuickAddViewModel viewModel, IntPtr previousWindow)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _previousWindow = previousWindow;
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle);
            Activate();
            Input.Focus();
        };
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // IME 変換中の Enter / Esc は Key.ImeProcessed として届くため、ここでは反応しない。
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _viewModel.Save();
            Close();
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_previousWindow != IntPtr.Zero && NativeMethods.IsWindow(_previousWindow))
        {
            NativeMethods.SetForegroundWindow(_previousWindow);
        }
    }
}
