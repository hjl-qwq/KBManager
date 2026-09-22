using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KBManager.GUI.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace KBManager.GUI.Views;

/// <summary>
/// The search sidebar: tag autocomplete plus a result list that opens files in
/// the editor area.
/// </summary>
public partial class SearchView : UserControl
{
    private TextBox? _tagTextBox;
    private ListBox? _suggestionListBox;
    private CancellationTokenSource? _lostFocusCts;
    private bool _handlingSuggestionSelection;

    public SearchView()
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

        _tagTextBox = this.FindControl<TextBox>("TagTextBox");
        _suggestionListBox = this.FindControl<ListBox>("SuggestionListBox");

        if (_tagTextBox != null)
        {
            _tagTextBox.GotFocus += OnTagTextBoxGotFocus;
            _tagTextBox.LostFocus += OnTagTextBoxLostFocus;
            _tagTextBox.KeyDown += OnTagTextBoxKeyDown;
        }

        if (_suggestionListBox != null)
            _suggestionListBox.SelectionChanged += OnSuggestionSelectionChanged;
    }

    private void OnTagTextBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            _ = vm.LoadSuggestionsCommand.ExecuteAsync(null);
    }

    private async void OnTagTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        _lostFocusCts?.Cancel();
        var cts = new CancellationTokenSource();
        _lostFocusCts = cts;

        try
        {
            // Give a click on the dropdown time to register before closing it.
            await Task.Delay(200, cts.Token);
            if (DataContext is SearchViewModel vm)
                vm.CloseSuggestionsCommand.Execute(null);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async void OnTagTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not SearchViewModel vm) return;

        if (vm.SearchCommand.CanExecute(null))
            await vm.SearchCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    private void OnSuggestionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_handlingSuggestionSelection) return;
        if (sender is not ListBox listBox) return;
        if (listBox.SelectedItem is not TagSuggestionItem suggestion) return;
        if (DataContext is not SearchViewModel vm) return;

        _handlingSuggestionSelection = true;
        _lostFocusCts?.Cancel();
        listBox.SelectedItem = null;

        // Defer so the ItemsSource is not mutated mid-selection.
        Dispatcher.UIThread.Post(async () =>
        {
            await vm.SelectSuggestionCommand.ExecuteAsync(suggestion);
            _handlingSuggestionSelection = false;
        });
    }
}
