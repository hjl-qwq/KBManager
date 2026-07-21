using CommunityToolkit.Mvvm.ComponentModel;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Base class for all ViewModels. Provides ObservableObject from CommunityToolkit.Mvvm.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    /// <summary>
    /// Set busy state with a status message atomically.
    /// </summary>
    protected void SetBusy(string message)
    {
        StatusMessage = message;
        IsBusy = true;
    }

    /// <summary>
    /// Clear busy state with optional completion message.
    /// </summary>
    protected void ClearBusy(string? message = null)
    {
        IsBusy = false;
        if (message != null) StatusMessage = message;
    }
}
