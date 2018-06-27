using System;

namespace DoomSpriteAnimator
{
    class Program
    {
        static void Main(string[] args)
        {
            //args = new string[] { "Doom_Definition.json" };

            if (args.Length > 0)
            {
                foreach (string pdPath in args)
                {
                    ConsolePrint.Print("Processing definition file \"{0}\".", pdPath);

                    ConsolePrint.Indent();
                    SpriteAnimator.ProcessProgramDefinitionFile(pdPath);
                    ConsolePrint.Unindent();
                }
            }
            else
            {
                ConsolePrint.Print("Usage: Pass the path of definition files to start process.");
            }

            ConsolePrint.Print("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
