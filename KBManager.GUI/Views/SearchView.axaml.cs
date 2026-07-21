using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KBManager.GUI.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace KBManager.GUI.Views;

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
            _tagTextBox.GotFocus += TagTextBox_GotFocus;
            _tagTextBox.LostFocus += TagTextBox_LostFocus;
        }

        if (_suggestionListBox != null)
        {
            _suggestionListBox.SelectionChanged += SuggestionListBox_SelectionChanged;
        }
    }

    private void TagTextBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            vm.LoadSuggestionsCommand.Execute(null);
    }

    private async void TagTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        _lostFocusCts?.Cancel();
        var cts = new CancellationTokenSource();
        _lostFocusCts = cts;

        try
        {
            await Task.Delay(200, cts.Token);
            if (DataContext is SearchViewModel vm)
                vm.CloseSuggestionsCommand.Execute(null);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void SuggestionListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_handlingSuggestionSelection) return;
        if (sender is not ListBox listBox) return;
        if (listBox.SelectedItem is not TagSuggestion suggestion) return;
        if (DataContext is not SearchViewModel vm) return;

        _handlingSuggestionSelection = true;
        _lostFocusCts?.Cancel();
        var captured = suggestion;
        listBox.SelectedItem = null;

        // Defer so we don't mutate ItemsSource mid-selection.
        Dispatcher.UIThread.Post(() =>
        {
            vm.SelectSuggestionCommand.Execute(captured);
            _handlingSuggestionSelection = false;
        });
    }
}
