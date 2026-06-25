using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AestikModLoader.App
{
    public static class ConsoleMode
    {
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        public static bool Enabled { get; private set; }

        public static void TryAttach()
        {
            if (Enabled)
            {
                return;
            }

            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }

            try
            {
                StreamWriter writer = new StreamWriter(Console.OpenStandardOutput());
                writer.AutoFlush = true;
                Console.SetOut(writer);
                Console.SetError(writer);
                Enabled = true;
            }
            catch
            {
            }
        }

        public static void WriteLine(string text)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                Console.WriteLine(text);
            }
            catch
            {
            }
        }
    }
}
