using System;
using System.Collections.Generic;

namespace KBManager.CLI
{
    /// <summary>
    /// A single item in an interactive menu.
    /// </summary>
    public class MenuItem
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? Hint { get; set; }            // extra info shown on the right
        public ConsoleColor? AccentColor { get; set; }
        public bool IsBackItem { get; set; }          // renders as "← Back"

        public MenuItem() { }

        public MenuItem(string id, string text, string? hint = null, ConsoleColor? accent = null, bool isBack = false)
        {
            Id = id;
            Text = text;
            Hint = hint;
            AccentColor = accent;
            IsBackItem = isBack;
        }

        public static MenuItem Back => new("__back__", "← Back", isBack: true);
        public static MenuItem Separator => new("__sep__", new string('-', 40)) { AccentColor = ConsoleColor.DarkGray };
    }

    /// <summary>
    /// Arrow-key-driven interactive menu.
    /// Usage:
    ///   var menu = new InteractiveMenu("Title", items);
    ///   var chosen = menu.Show();   // returns the selected MenuItem.Id
    /// </summary>
    public class InteractiveMenu
    {
        private readonly string _title;
        private readonly List<MenuItem> _items;
        private readonly bool _showExitHint;

        private int _selectedIndex;

        public InteractiveMenu(string title, List<MenuItem> items, bool showExitHint = true)
        {
            _title = title;
            _items = items.FindAll(i => i.Id != "__sep__"); // separators handled during render
            _showExitHint = showExitHint;
        }

        /// <summary>
        /// Display the menu and return the selected item's Id.
        /// Returns "__back__" on Escape.
        /// </summary>
        public string Show()
        {
            _selectedIndex = 0;
            Console.CursorVisible = false;

            while (true)
            {
                Render();
                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        _selectedIndex = (_selectedIndex - 1 + _items.Count) % _items.Count;
                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        _selectedIndex = (_selectedIndex + 1) % _items.Count;
                        break;

                    case ConsoleKey.Enter:
                        Console.CursorVisible = true;
                        Console.WriteLine();
                        return _items[_selectedIndex].Id;

                    case ConsoleKey.Escape:
                        Console.CursorVisible = true;
                        Console.WriteLine();
                        return "__back__";

                    case ConsoleKey.D1:
                    case ConsoleKey.D2:
                    case ConsoleKey.D3:
                    case ConsoleKey.D4:
                    case ConsoleKey.D5:
                    case ConsoleKey.D6:
                    case ConsoleKey.D7:
                    case ConsoleKey.D8:
                    case ConsoleKey.D9:
                        int num = key.Key - ConsoleKey.D1;
                        if (num < _items.Count)
                        {
                            Console.CursorVisible = true;
                            Console.WriteLine();
                            return _items[num].Id;
                        }
                        break;
                }
            }
        }

        // ---- rendering --------------------------------------------------------

        private void Render()
        {
            // Clear and redraw from a safe position.  We anchor at the top of the
            // current console window to avoid negative-cursor bugs when piped.
            int top = Math.Max(0, Console.CursorTop - _items.Count - 6);

            // Clear the region we are about to paint
            for (int i = 0; i < _items.Count + 6; i++)
            {
                try
                {
                    Console.SetCursorPosition(0, top + i);
                    Console.Write(new string(' ', Console.WindowWidth));
                }
                catch (ArgumentOutOfRangeException) { /* best-effort */ }
            }

            try { Console.SetCursorPosition(0, top); }
            catch (ArgumentOutOfRangeException) { top = 0; Console.SetCursorPosition(0, 0); }

            // Title
            Console.WriteLine();
            ConsoleUI.WriteLineColor($"  {_title}", ConsoleColor.Cyan);
            ConsoleUI.WriteLineColor($"  {new string('-', Math.Min(50, Console.WindowWidth - 4))}", ConsoleColor.DarkGray);
            Console.WriteLine();

            // Items
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var isSelected = i == _selectedIndex;

                if (isSelected)
                {
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"  ▶ {item.Text}");
                    if (!string.IsNullOrEmpty(item.Hint))
                        Console.Write($"  ({item.Hint})");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                else
                {
                    var color = item.AccentColor ?? ConsoleColor.Gray;
                    if (item.IsBackItem) color = ConsoleColor.DarkGray;
                    Console.Write("    ");
                    ConsoleUI.WriteLineColor(item.Text, color);
                }
            }

            Console.WriteLine();
            if (_showExitHint)
                ConsoleUI.WriteDim("  ↑↓ Navigate   Enter Select   Esc Back   # Quick-select");
            else
                ConsoleUI.WriteDim("  ↑↓ Navigate   Enter Select   # Quick-select");
        }
    }
}
