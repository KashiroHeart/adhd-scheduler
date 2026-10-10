using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AdhdScheduler.Core.ViewModels;

namespace AdhdScheduler.App.Views;

/// <summary>画面A：実行画面。枠なし・最前面。背景をドラッグして移動する。</summary>
public partial class ExecutionWindow : Window
{
    private const double ScreenMargin = 24;

    public ExecutionWindow()
    {
        InitializeComponent();
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - ScreenMargin;
        Top = workArea.Top + ScreenMargin;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ExecutionViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is ExecutionViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExecutionViewModel.IsAddingPrecursor)
            && sender is ExecutionViewModel { IsAddingPrecursor: true })
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => PrecursorPurpose.Focus());
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
