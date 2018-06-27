using System;

namespace DoomSpriteAnimator
{
    static class ConsolePrint
    {
        static string PrintPrefix = "";

        public static void Print(ConsoleColor color, string format, params string[] args)
        {
            if (Console.ForegroundColor == color)
            {
                Console.WriteLine(PrintPrefix + string.Format(format, args));
            }
            else
            {
                ConsoleColor c = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(PrintPrefix + string.Format(format, args));
                Console.ForegroundColor = c;
            }
        }

        public static void Print(string format, params string[] args)
            => Print(ConsoleColor.Gray, format, args);

        public static void Warning(string format, params string[] args)
            => Print(ConsoleColor.Yellow, "Warning: " + format, args);

        public static void Error(string format, params string[] args)
            => Print(ConsoleColor.Red, "Error: " + format, args);

        public static void Indent()
            => PrintPrefix += "  ";

        public static void Unindent()
            => PrintPrefix = PrintPrefix.Length > 2 ? PrintPrefix.Substring(2) : "";
    }
}
