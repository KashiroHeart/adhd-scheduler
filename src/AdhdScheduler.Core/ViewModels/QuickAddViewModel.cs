using AdhdScheduler.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdhdScheduler.Core.ViewModels;

/// <summary>画面B：クイック登録。</summary>
public partial class QuickAddViewModel : ObservableObject
{
    private readonly AppStore _store;

    public QuickAddViewModel(AppStore store)
    {
        _store = store;
    }

    [ObservableProperty]
    private string _text = "";

    /// <summary>空白のみなら保存しない。</summary>
    public void Save() => _store.AddLaterItem(Text);
}
