using System;
using System.Threading.Tasks;

namespace KBManager.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "KBManager — Knowledge Base CLI";

            try
            {
                var app = new CliApp();
                await app.RunAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot read keys"))
            {
                // Piped / non-interactive environment — graceful exit.
                Console.WriteLine("KBManager requires an interactive terminal.");
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteError($"Fatal error: {ex.Message}");
                ConsoleUI.WriteDim(ex.StackTrace ?? "(no stack trace)");
                ConsoleUI.PressAnyKey("Press any key to exit...");
            }
        }
    }
}