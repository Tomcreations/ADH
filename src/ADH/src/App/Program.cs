using System;
using System.Windows.Forms;

namespace AestikModLoader.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--console", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "--terminal", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleMode.TryAttach();
                    ConsoleMode.WriteLine("ADH console mode enabled.");
                    break;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HtmlLauncherForm());
        }
    }
}
