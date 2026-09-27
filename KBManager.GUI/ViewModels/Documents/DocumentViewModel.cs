using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Base class for anything hosted as a tab in the editor area: text documents
/// and tool pages (settings, Git) share the same tab strip and close behaviour.
/// </summary>
public abstract partial class DocumentViewModel : ObservableObject
{
    /// <summary>Label shown on the tab.</summary>
    public abstract string Title { get; }

    /// <summary>Secondary line shown in the status bar / tab tooltip.</summary>
    public abstract string LocationLabel { get; }

    /// <summary>
    /// True when <see cref="LocationLabel"/> carries information the title does not
    /// (a file's folder), so the tab tooltip shows it as a second line.
    /// </summary>
    public virtual bool HasLocationDetail => false;

    /// <summary>
    /// Repository-relative path for file documents; empty for tool pages.
    /// </summary>
    public virtual string RelativePath => string.Empty;

    /// <summary>Whether this document has a meaningful save action.</summary>
    public virtual bool CanSave => false;

    /// <summary>Highlighted as the currently visible tab.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Unsaved changes pending.</summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>
    /// Opened by a single click in the explorer: a provisional tab shown with an
    /// italic title. It is replaced by the next preview and stops being provisional
    /// as soon as the user actually works in it (or opens it with a double click).
    /// </summary>
    [ObservableProperty]
    private bool _isPreview;

    /// <summary>
    /// Turn a provisional (single-click) tab into a normal one: double-clicking the
    /// file, editing it, or re-opening it for real all land here.
    /// </summary>
    public void PromoteToPermanent()
    {
        if (IsPreview) IsPreview = false;
    }

    /// <summary>
    /// Wired by the shell when the document is added, so the tab strip can drive
    /// activation and closing without reaching up the visual tree.
    /// </summary>
    public Action<DocumentViewModel>? ActivateRequested { get; set; }

    /// <summary>Wired by the shell; asks the shell to close this document.</summary>
    public Action<DocumentViewModel>? CloseRequested { get; set; }

    [RelayCommand]
    private void Activate() => ActivateRequested?.Invoke(this);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this);

    /// <summary>Persist pending changes. Returns false when the save failed.</summary>
    public virtual Task<bool> SaveAsync() => Task.FromResult(true);
}
