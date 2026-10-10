namespace AdhdScheduler.Core.ViewModels;

/// <summary>ViewModel からウィンドウ操作を行うための窓口。</summary>
public interface IWindowService
{
    void OpenPlanning();

    void OpenLaterList();

    bool Confirm(string message);
}
