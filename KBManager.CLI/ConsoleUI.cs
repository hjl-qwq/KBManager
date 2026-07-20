using System;
using System.Collections.Generic;
using System.Linq;

namespace KBManager.CLI
{
    /// <summary>
    /// Console output helpers — colors, formatting, prompts.
    /// Keeps rendering concerns separate from business logic.
    /// </summary>
    public static class ConsoleUI
    {
        // ---- colors -----------------------------------------------------------

        public static void WriteSuccess(string text) => WriteLineColor(text, ConsoleColor.Green);
        public static void WriteError(string text) => WriteLineColor(text, ConsoleColor.Red);
        public static void WriteWarning(string text) => WriteLineColor(text, ConsoleColor.Yellow);
        public static void WriteInfo(string text) => WriteLineColor(text, ConsoleColor.Cyan);
        public static void WriteDim(string text) => WriteLineColor(text, ConsoleColor.DarkGray);

        public static void WriteLineColor(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }

        public static void WriteColor(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = prev;
        }

        // ---- header / footer --------------------------------------------------

        public static void WriteHeader(string title)
        {
            Console.Clear();
            var divider = new string('=', 50);
            Console.WriteLine();
            WriteLineColor($"  {divider}", ConsoleColor.Cyan);
            WriteLineColor($"  {title.PadLeft((50 + title.Length) / 2)}", ConsoleColor.Cyan);
            WriteLineColor($"  {divider}", ConsoleColor.Cyan);
            Console.WriteLine();
        }

        public static void WriteSubHeader(string title)
        {
            var divider = new string('-', 40);
            WriteLineColor($"  {divider}", ConsoleColor.DarkCyan);
            WriteLineColor($"  {title}", ConsoleColor.DarkCyan);
            WriteLineColor($"  {divider}", ConsoleColor.DarkCyan);
            Console.WriteLine();
        }

        public static void WriteDivider()
        {
            Console.WriteLine(new string('-', 50));
        }

        // ---- prompts ----------------------------------------------------------

        public static string ReadLine(string prompt)
        {
            WriteColor($"  {prompt}: ", ConsoleColor.White);
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        public static string ReadMasked(string prompt)
        {
            WriteColor($"  {prompt}: ", ConsoleColor.White);
            var pass = string.Empty;
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    pass = pass[..^1];
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
            }
            Console.WriteLine();
            return pass;
        }

        public static bool Confirm(string prompt)
        {
            WriteColor($"  {prompt} [y/N]: ", ConsoleColor.Yellow);
            var key = Console.ReadKey(true);
            Console.WriteLine(key.KeyChar);
            return key.Key == ConsoleKey.Y;
        }

        public static void PressAnyKey(string message = "Press any key to continue...")
        {
            Console.WriteLine();
            WriteDim($"  {message}");
            try { Console.ReadKey(true); }
            catch (InvalidOperationException) { /* piped stdin — skip */ }
        }

        // ---- tables -----------------------------------------------------------

        public static void WriteFileTable(IReadOnlyList<KBManager.core.FileEntryDto> files, bool showIndex = true)
        {
            if (files.Count == 0)
            {
                WriteDim("  (no files)");
                return;
            }

            Console.WriteLine();
            WriteLineColor($"  {"#",-4} {"File",-50} {"Tags"}", ConsoleColor.White);
            WriteLineColor($"  {new string('-', 3)} {new string('-', 49)} {new string('-', 30)}", ConsoleColor.DarkGray);

            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                var tags = f.Tags.Count > 0 ? string.Join(", ", f.Tags) : "-";
                var name = f.FileName.Length > 48 ? "..." + f.FileName[^45..] : f.FileName;

                if (showIndex)
                    Console.Write($"  {i + 1,-4}");
                else
                    Console.Write($"  {"",-4}");

                WriteColor($"{name,-50}", ConsoleColor.White);

                if (f.Tags.Count > 0)
                    WriteLineColor(tags, ConsoleColor.Green);
                else
                    WriteLineColor(tags, ConsoleColor.DarkGray);
            }
            Console.WriteLine();
            WriteDim($"  Total: {files.Count} file(s)");
        }

        public static void WriteTagList(IReadOnlyList<KBManager.core.TagEntryDto> tags)
        {
            if (tags.Count == 0)
            {
                WriteDim("  (no tags)");
                return;
            }

            const int cols = 4;
            var colWidth = Console.WindowWidth / cols - 2;
            for (int i = 0; i < tags.Count; i++)
            {
                WriteColor($"  ● {tags[i].TagName}".PadRight(colWidth), ConsoleColor.Green);
                if ((i + 1) % cols == 0) Console.WriteLine();
            }
            if (tags.Count % cols != 0) Console.WriteLine();
            WriteDim($"  Total: {tags.Count} tag(s)");
        }

        public static void ShowServiceResult(KBManager.core.ServiceResult result)
        {
            Console.WriteLine();
            if (result.Success)
                WriteSuccess($"  ✓ {result.Message}");
            else
                WriteError($"  ✗ {result.Message}");
        }
    }
}
