using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using KBManager.GUI.ViewModels;
using System.Linq;

namespace KBManager.GUI.Views;

/// <summary>
/// The explorer sidebar.
///
/// The tag preview popup is a standard ToolTip with a one-second show delay and
/// pointer placement (see the item template), so no hover bookkeeping is needed
/// here. This class only adds the VS Code style activation behaviour: a single click
/// previews a file, a double click (or Enter) opens it for real and toggles folders,
/// and right-click selects the row the context menu will act on.
/// </summary>
public partial class ExplorerView : UserControl
{
    public ExplorerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var tree = this.FindControl<TreeView>("FileTreeView");
        if (tree == null) return;

        tree.Tapped += OnTreeTapped;
        tree.DoubleTapped += OnTreeDoubleTapped;
        tree.KeyDown += OnTreeKeyDown;
        tree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Single click = preview. Folders are skipped on purpose: they are toggled by
    /// their chevron and by double-clicking the row, and acting here as well would
    /// cancel those out.
    /// </summary>
    private async void OnTreeTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ExplorerViewModel vm) return;

        var node = ResolveNode(e.Source as Visual);
        if (node is not { IsFile: true, FullPath: not null }) return;

        await vm.ActivateNodeAsync(node, preview: true);
    }

    /// <summary>
    /// Double click = open for real: the file gets a permanent tab, and the preview
    /// the first click created is simply promoted instead of being opened twice.
    /// </summary>
    private async void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ExplorerViewModel vm) return;
        var node = ResolveNode(e.Source as Visual) ?? vm.SelectedNode;
        await vm.ActivateNodeAsync(node);
    }

    private async void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ExplorerViewModel vm) return;

        await vm.ActivateNodeAsync(vm.SelectedNode);
        e.Handled = true;
    }

    /// <summary>Right-clicking a row selects it, so the context menu and any
    /// follow-up keyboard action clearly refer to that row.</summary>
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        if (DataContext is not ExplorerViewModel vm) return;

        var node = ResolveNode(e.Source as Visual);
        if (node != null) vm.SelectedNode = node;
    }

    /// <summary>Walk up from the pressed element to the tree row's data context.</summary>
    private static FileTreeNode? ResolveNode(Visual? source)
    {
        foreach (var visual in source?.GetSelfAndVisualAncestors() ?? Enumerable.Empty<Visual>())
        {
            if (visual is Control { DataContext: FileTreeNode node }) return node;
        }
        return null;
    }
}
