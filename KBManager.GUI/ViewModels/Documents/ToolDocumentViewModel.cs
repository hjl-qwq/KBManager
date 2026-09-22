using System;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// A non-file tool page (Settings, Git operations) shown as a tab in the editor
/// area. The page's own ViewModel drives the view through a DataTemplate.
/// </summary>
public class ToolDocumentViewModel : DocumentViewModel
{
    private readonly string _title;
    private readonly Action? _onOpened;

    /// <summary>The page ViewModel rendered inside this document.</summary>
    public ViewModelBase Page { get; }

    public ToolDocumentViewModel(string title, ViewModelBase page, Action? onOpened = null)
    {
        _title = title;
        Page = page;
        _onOpened = onOpened;
    }

    public override string Title => _title;

    public override string LocationLabel => _title;

    /// <summary>Load this page's data; called once when the tab is first opened.</summary>
    public void NotifyOpened() => _onOpened?.Invoke();
}
