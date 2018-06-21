using AnimatedGif;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SpriteGenerator
{
    class Program
    {
        static string PrintPrefix = "";

        static void Print(ConsoleColor color, string format, params string[] args)
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

        static void Print(string format, params string[] args) =>
            Print(ConsoleColor.Gray, format, args);

        static void Warning(string format, params string[] args) =>
            Print(ConsoleColor.Yellow, "Warning: " + format, args);

        static void Error(string format, params string[] args) =>
            Print(ConsoleColor.Red, "Error: " + format, args);

        static void Main(string[] args)
        {
            //args = new string[] { "Doom_Definition.json" };

            if (args.Length > 0)
            {
                foreach (string pdPath in args)
                {
                    PrintPrefix = "";
                    Print("Processing definition file \"{0}\".", pdPath);

                    ProcessProgramDefinitionFile(pdPath);
                }
            }
            else
            {
                Print("Usage: Pass the path of definition files to start process.");
            }

            PrintPrefix = "";
            Print("Press any key to exit...");
            Console.ReadKey();
        }

        static void ProcessProgramDefinitionFile(string pdPath)
        {
            PrintPrefix = "  ";

            ProgramDefinition pd = null;

            // Open file and read json file.
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                pd = js.Deserialize<ProgramDefinition>(File.ReadAllText(pdPath));
            }
            catch
            {
                Error("Failed to open definition: \"{0}\"", pdPath);
                return;
            }

            if (pd.ActorStates == null)
            {
                if (pd.ExportActorStatesAnimatedGIF)
                {
                    Warning("ActorStates must be defined to export animated GIF.");
                    pd.ExportActorStatesAnimatedGIF = false;
                }
                if (!string.IsNullOrEmpty(pd.ExportActorStatesAnimatedCSSPath))
                {
                    Warning("ActorStates must be defined to export CSS animation.");
                    pd.ExportActorStatesAnimatedCSSPath = null;
                }
                if (pd.ExportActorStatesSpriteSheet)
                {
                    Warning("ActorStates must be defined to export states sprite sheet.");
                    pd.ExportActorStatesSpriteSheet = false;
                }
                if (!string.IsNullOrEmpty(pd.ExportActorStatesSpriteSheetJSONPath))
                {
                    Warning("ActorStates must be defined to export states sprite sheet JSON file.");
                    pd.ExportActorStatesSpriteSheetJSONPath = null;
                }
            }

            if (pd.PngDirectory == null)
            {
                Error("PngDirectory is not defined.");
                return;
            }

            if (!Directory.Exists(pd.PngDirectory))
            {
                Error("PngPath \"{0}\" not exist.", pd.PngDirectory);
                return;
            }

            if (string.IsNullOrEmpty(pd.ExportBaseDirectory))
            {
                Error("ExportBaseDirectory is not defined.");
                return;
            }

            if (pd.ContainsExport_Full)
            {
                Print("Processing full sprites...");

                PrintPrefix = "    ";
                ExportAll(
                    Directory.GetFiles(pd.PngDirectory, "*.png"),
                    null,
                    null,
                    pd.ExportNamePrefix,
                    pd.ExportFullSpriteSheet,
                    pd.ExportFullSpriteSheetJSONPath,
                    null,
                    false,
                    MergePath(pd.ExportBaseDirectory, "Full"));
                PrintPrefix = "  ";
            }

            if (pd.ContainsExport_States)
            {
                Print("Processing actor states sprites...");

                // Separate export names from actor states.
                Dictionary<string, string> exportNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in pd.ActorStates)
                {
                    string n = kvp.Value.ExportName;
                    if (string.IsNullOrEmpty(n)) continue;

                    if (!exportNames.ContainsKey(n))
                    {
                        exportNames.Add(kvp.Key, n);
                    }
                    else
                    {
                        Warning("Export name conflict (\"{0}\"). Will be removed to prevent errors.", n);
                        kvp.Value.ExportName = null;
                    }
                }

                PrintPrefix = "    ";
                ExportAll(
                    FindReferencedPngs(pd.ActorStates, pd.PngDirectory),
                    pd.ActorStates,
                    exportNames,
                    pd.ExportNamePrefix,
                    pd.ExportActorStatesSpriteSheet,
                    pd.ExportActorStatesSpriteSheetJSONPath,
                    pd.ExportActorStatesAnimatedCSSPath,
                    pd.ExportActorStatesAnimatedGIF,
                    MergePath(pd.ExportBaseDirectory, "ActorStates"));
                PrintPrefix = "  ";
            }
        }

        static void ExportAll(string[] pngList, Dictionary<string, ActorStatesJSON> actorStates, Dictionary<string, string> exportName, string namePrefix, bool exportSpriteSheet, string spriteSheetJSONPath, string cssPath, bool exportAnimatedGIF, string exportBaseDirectory)
        {
            CreateDirectoryIfNotExist(exportBaseDirectory);

            Dictionary<string, SpriteCollection> spriteCollection = GetSpriteCollection(pngList);

            if (exportSpriteSheet)
            {
                Print("Exporting sprite sheet...");
                ExportSpriteSheet(
                    spriteCollection,
                    exportName,
                    MergePath(exportBaseDirectory, "SpriteSheet"),
                    namePrefix);
            }

            bool hasSpriteSheetJSON = !string.IsNullOrEmpty(spriteSheetJSONPath),
                hasCSS = !string.IsNullOrEmpty(cssPath);
            if (hasSpriteSheetJSON || hasCSS)
            {
                Dictionary<string, SpriteSheetJSON> spriteSheetJSON = GetSpriteSheetJSON(spriteCollection, exportName, namePrefix);

                if (hasSpriteSheetJSON)
                {
                    Print("Exporting sprite sheet JSON...");
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    File.WriteAllText(
                        MergePath(exportBaseDirectory, spriteSheetJSONPath),
                        js.Serialize(spriteSheetJSON));
                }

                if (hasCSS)
                {
                    Print("Exporting animated CSS...");
                    ExportAnimatedCSS(
                        spriteSheetJSON,
                        actorStates,
                        exportName,
                        MergePath(exportBaseDirectory, "SpriteSheet"), // So it can be referenced directly to all sprite sheets.
                        namePrefix,
                        cssPath);
                }
            }

            if (exportAnimatedGIF)
            {
                Print("Exporting animated GIF...");
                ExportAnimatedGIF(
                    spriteCollection,
                    actorStates,
                    exportName,
                    MergePath(exportBaseDirectory, "AnimatedGIF"),
                    namePrefix);
            }
        }

        /// <summary>
        /// Finds all associated png images from actor states.
        /// </summary>
        static string[] FindReferencedPngs(Dictionary<string, ActorStatesJSON> actorStates, string pngDirectory)
        {
            // Find all possible files referenced in states.
            string[] pngPaths = Directory.GetFiles(pngDirectory, "*.png");
            HashSet<string> fileList = new HashSet<string>();
            foreach (var kvp in actorStates)
            {
                string name = kvp.Key;
                List<ActorStatesJSON.State> states = kvp.Value.GetAllStates();

                // Get all possible files with name prefix.
                var files = pngPaths.Where((s) => Path.GetFileNameWithoutExtension(s).StartsWith(name, StringComparison.OrdinalIgnoreCase));
                foreach (ActorStatesJSON.State state in states)
                {
                    // Find frame and best rotation (1 first, then 0).
                    bool found = false;
                    foreach (string file in files)
                    {
                        string f = Path.GetFileNameWithoutExtension(file).Substring(4);
                        switch (f.Length)
                        {
                            case 2:
                            case 4:
                                char rotation;
                                if (f.StartsWith(state.Frame))
                                {
                                    rotation = f[1];
                                    found = rotation == '0' || rotation == '1';
                                }
                                if (!found && f.Length == 4 && f.Substring(2).StartsWith(state.Frame))
                                {
                                    rotation = f[3];
                                    found = rotation == '0' || rotation == '1';
                                }
                                break;
                        }
                        if (found)
                        {
                            if (!fileList.Contains(file))
                            {
                                fileList.Add(file);
                            }
                            break;
                        }
                    }
                }
            }

            return fileList.ToArray();
        }

        /// <summary>
        /// Gets all sprite collections from specified pngs.
        /// </summary>
        static Dictionary<string, SpriteCollection> GetSpriteCollection(string[] pngPaths)
        {
            Dictionary<string, SpriteCollection> spriteCollection = new Dictionary<string, SpriteCollection>(StringComparer.Ordinal);

            // Construct sprite list.
            int success = 0, fail = 0;
            foreach (var f in pngPaths)
            {
                try
                {
                    // Check file name first.
                    string fn = Path.GetFileNameWithoutExtension(f);
                    if (fn.Length != 6 && fn.Length != 8) // Not a valid sprite name.
                    {
                        continue;
                    }

                    // Name of this sprite.
                    string name = fn.Substring(0, 4);

                    if (!spriteCollection.TryGetValue(name, out SpriteCollection sprites))
                    {
                        sprites = new SpriteCollection(name);
                        spriteCollection.Add(name, sprites);
                    }

                    // Find offset chunk and offset values.
                    byte[] bytes = File.ReadAllBytes(f);
                    int x = 0, y = 0; // So it will be (0, 0) when offset is not found.
                    for (int i = 0; i < bytes.Length - 12; i++)
                    {
                        if (bytes[i] == 'g' && bytes[i + 1] == 'r' && bytes[i + 2] == 'A' && bytes[i + 3] == 'b')
                        {
                            x = (bytes[i + 4] << 24) | (bytes[i + 5] << 16) | (bytes[i + 6] << 8) | bytes[i + 7];
                            y = (bytes[i + 8] << 24) | (bytes[i + 9] << 16) | (bytes[i + 10] << 8) | bytes[i + 11];
                            break;
                        }
                    }

                    // Load image.
                    Image img = Image.FromStream(new MemoryStream(bytes));

                    // Make the first sprite.
                    sprites.AddSprite(new Sprite(name, fn.Substring(4, 1), fn.Substring(5, 1), img, new Rectangle(-x, -y, img.Width, img.Height)));

                    // If the is a second sprite, make it.
                    if (fn.Length > 6)
                    {
                        Image img2 = (Image)img.Clone();
                        img2.RotateFlip(RotateFlipType.Rotate180FlipY);
                        sprites.AddSprite(new Sprite(name, fn.Substring(6, 1), fn.Substring(7, 1), img2, new Rectangle(-img.Width + x, -y, img.Width, img.Height)));
                    }

                    success++;
                }
                catch
                {
                    fail++;
                }
            }

            if (fail > 0)
            {
                Warning("Some errors happen while loading png images.");
            }

            return spriteCollection;
        }

        /// <summary>
        /// Helper function for getting image name for this sprite sheet.
        /// </summary>
        static string GetFileName(string spriteName, Dictionary<string, string> exportName, string namePrefix)
            => exportName != null && exportName.ContainsKey(spriteName) ? namePrefix + exportName[spriteName] : namePrefix + spriteName;

        /// <summary>
        /// Automatically creates a directory if it deosn't exist and make sure it is a relative path instead of an absolute one.
        /// </summary>
        static void CreateDirectoryIfNotExist(string directory)
        {
            directory = directory.Trim('\\');
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Merges two paths and remove leading and ending backslashes, making it a relative path.
        /// </summary>
        static string MergePath(params string[] paths)
            => Path.Combine(paths).Trim('\\');

        /// <summary>
        /// Exports sprite collection according to the sprite collection provided.
        /// </summary>
        static void ExportSpriteSheet(Dictionary<string, SpriteCollection> spriteCollection, Dictionary<string, string> exportName, string baseDirectory, string namePrefix)
        {
            CreateDirectoryIfNotExist(baseDirectory);

            foreach (var kvp in spriteCollection)
            {
                string spriteName = kvp.Key;
                SpriteCollection sprites = kvp.Value;

                string imgName = GetFileName(spriteName, exportName, namePrefix);

                // Construct empty image.
                Rectangle region = sprites.Region;
                int frameCount = sprites.FrameCount;
                int rotationCount = sprites.MaxRotationCountPerFrame;
                Bitmap bmp = new Bitmap(region.Width * rotationCount, region.Height * frameCount, PixelFormat.Format32bppArgb);

                // Draw every frame and sprite.
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    int ix, iy = 0; // Anchor position for bitmap.
                    g.CompositingMode = CompositingMode.SourceOver;
                    foreach (var f in sprites.Frames)
                    {
                        ix = 0;
                        foreach (var s in f.Sprites)
                        {
                            g.DrawImage(s.Image, new Rectangle(ix + (s.Region.X - region.X), iy + (s.Region.Y - region.Y), s.Image.Width, s.Image.Height));
                            ix += region.Width;
                        }
                        iy += region.Height;
                    }
                }

                bmp.Save(MergePath(baseDirectory, imgName + ".png"));
            }
        }

        /// <summary>
        /// Gets sprite sheet JSON object so it can be used further.
        /// For user defined animation with sprite sheets, this JSON will be helpful.
        /// </summary>
        static Dictionary<string, SpriteSheetJSON> GetSpriteSheetJSON(Dictionary<string, SpriteCollection> spriteCollection, Dictionary<string, string> exportName, string namePrefix)
        {
            // Build sprite sheet and json data.
            Dictionary<string, SpriteSheetJSON> spriteSheetJSON = new Dictionary<string, SpriteSheetJSON>(StringComparer.Ordinal);
            StringBuilder jsonFrame = new StringBuilder();
            foreach (var kvp in spriteCollection)
            {
                string spriteName = kvp.Key;
                SpriteCollection sprites = kvp.Value;

                string imgName = GetFileName(spriteName, exportName, namePrefix);

                // Draw every frame and sprite.
                SpriteSheetJSON json = new SpriteSheetJSON();
                foreach (var f in sprites.Frames)
                {
                    jsonFrame.Clear();
                    jsonFrame.Append(f.Frame + " ");
                    foreach (var s in f.Sprites)
                    {
                        jsonFrame.Append(s.Rotation);
                    }
                    json.Frames.Add(jsonFrame.ToString());
                }

                json.SpriteWidth = sprites.Region.Width;
                json.SpriteHeight = sprites.Region.Height;
                spriteSheetJSON.Add(imgName, json);
            }

            return spriteSheetJSON;
        }

        /// <summary>
        /// Exports a CSS file with a testing HTML file to view the result of sprite animation by CSS3.
        /// </summary>
        static void ExportAnimatedCSS(Dictionary<string, SpriteSheetJSON> spriteSheetJSON, Dictionary<string, ActorStatesJSON> actorStates, Dictionary<string, string> exportName, string baseDirectory, string namePrefix, string cssPath)
        {
            if (string.IsNullOrEmpty(cssPath))
            {
                return;
            }

            if (Path.GetExtension(cssPath) != ".css")
            {
                string newPath = Path.ChangeExtension(cssPath, "css");
                Warning("CSS file extension must be \".css\". Output path will be changed from \"{0}\" to \"{1}\".", cssPath, newPath);
                cssPath = newPath;
            }

            // Create test html.
            StringBuilder html = new StringBuilder();
            html.AppendLine("<html>")
                .AppendLine("<head>")
                .AppendLine("<title>Test</title>")
                .AppendLine("<link rel=\"stylesheet\" type=\"text/css\" href=\"" + cssPath + "\">")
                .AppendLine("<style>")
                .AppendLine("body { background-color: black }")
                .AppendLine("</style>")
                .AppendLine("</head>")
                .AppendLine("<body>");

            // Create css script in memory.
            StringBuilder css = new StringBuilder();
            foreach (var kvp in actorStates)
            {
                string spriteName = kvp.Key;
                string imgName = GetFileName(spriteName, exportName, namePrefix);

                // Cannot find sprite sheet definitions.
                if (!spriteSheetJSON.TryGetValue(imgName, out SpriteSheetJSON ssj))
                {
                    Warning("Sprite sheet \"{0}\" not found in JSON and will be skipped.", imgName);
                    continue;
                }

                string className = "SAni_" + imgName;
                string kfName = "SKF_" + imgName;

                // Get all states.
                List<ActorStatesJSON.State> states = kvp.Value.GetAllStates();

                // Total amount of duration.
                double totalDuration = states.Sum(x => x.Duration);

                // Generate class first.
                css.AppendFormat(".{0} {{", className).AppendLine()
                    .AppendFormat("\twidth: {0}px;", ssj.SpriteWidth).AppendLine()
                    .AppendFormat("\theight: {0}px;", ssj.SpriteHeight).AppendLine()
                    .AppendFormat("\tbackground: url('{0}.png');", imgName).AppendLine()
                    .AppendFormat("\tanimation: {0} {1:0.000}s step-end infinite;", kfName, totalDuration / 35.0).AppendLine()
                    .AppendLine("}");

                // Generate key frames.
                css.AppendFormat("@keyframes {0} {{", kfName).AppendLine();
                int duration = 0;
                foreach (var s in states)
                {
                    int fi = 0;
                    foreach (string f in ssj.Frames)
                    {
                        if (f.StartsWith(s.Frame)) break; // Find index of frame.
                        fi++;
                    }
                    int ri = 0; // Just take the first rotation.
                    css.AppendFormat("\t{0:0.00}% {{ background-position: {1}px {2}px }}",
                        duration / totalDuration * 100.0, -ri * ssj.SpriteWidth, -fi * ssj.SpriteHeight).AppendLine();
                    duration += s.Duration;
                }
                css.AppendLine("}").AppendLine();

                html.AppendLine("<div class=\"" + className + "\"></div>");
            }

            // Export css file.
            File.WriteAllText(MergePath(baseDirectory, cssPath), css.ToString());

            // Export testing html file.
            html.AppendLine("</body>")
                .AppendLine("</html>");
            File.WriteAllText(MergePath(baseDirectory, Path.ChangeExtension(cssPath, "html")), html.ToString());
        }

        /// <summary>
        /// Exports animated GIF files accodring to the sprite collection provided.
        /// </summary>
        static void ExportAnimatedGIF(Dictionary<string, SpriteCollection> spriteCollection, Dictionary<string, ActorStatesJSON> actorStates, Dictionary<string, string> exportName, string baseDirectory, string namePrefix)
        {
            CreateDirectoryIfNotExist(baseDirectory);

            foreach (var kvp in actorStates)
            {
                string spriteName = kvp.Key;
                string imgName = GetFileName(spriteName, exportName, namePrefix);

                if (!spriteCollection.TryGetValue(spriteName, out SpriteCollection sprites))
                {
                    Warning("Sprite collection \"{0}\" not found and will be skipped.", imgName);
                    continue;
                }

                // Construct GIF image.
                AnimatedGifCreator gif = AnimatedGif.AnimatedGif.Create(MergePath(baseDirectory, imgName + ".gif"), 1000 / 35);
                Dictionary<Sprite, Bitmap> processedSprites = new Dictionary<Sprite, Bitmap>();

                List<ActorStatesJSON.State> states = kvp.Value.GetAllStates();
                Rectangle region = sprites.Region;
                foreach (var state in states)
                {
                    int fi = 0;
                    foreach (var f in sprites.Frames)
                    {
                        if (f.Frame == state.Frame) break;
                        fi++;
                    }
                    int ri = 0; // Just take the first rotation.

                    var s = sprites.Frames[fi].Sprites[ri];
                    if (!processedSprites.TryGetValue(s, out Bitmap gifFrame))
                    {
                        gifFrame = new Bitmap(region.Width, region.Height, s.Image.PixelFormat);
                        using (Graphics ggif = Graphics.FromImage(gifFrame))
                        {
                            ggif.DrawImage(s.Image, new Rectangle(s.Region.X - region.X, s.Region.Y - region.Y, s.Image.Width, s.Image.Height));
                        }
                        processedSprites.Add(s, gifFrame);
                    }

                    gif.AddFrame(gifFrame, (int)Math.Round(state.Duration * 1000.0 / 35.0), GifQuality.Bit8);
                }

                gif.Dispose();
            }
        }

        class ProgramDefinition
        {
            /// <summary>
            /// Directory of png images. (Sub-directories are not included in the search.)
            /// </summary>
            public string PngDirectory { get; set; }

            /// <summary>
            /// Definition of actor states.
            /// </summary>
            public Dictionary<string, ActorStatesJSON> ActorStates { get; set; }

            /// <summary>
            /// Define base directory for all exported files.
            /// </summary>
            public string ExportBaseDirectory { get; set; }

            /// <summary>
            /// Define name prefix for all exported files.
            /// </summary>
            public string ExportNamePrefix { get; set; }

            /// <summary>
            /// Whether to export a sprite sheet with all pngs within specified path.
            /// </summary>
            public bool ExportFullSpriteSheet { get; set; }

            /// <summary>
            /// File path for exporting a JSON file with all definitions in a sprite sheet.
            /// </summary>
            public string ExportFullSpriteSheetJSONPath { get; set; }

            /// <summary>
            /// Whether to export a sprite sheet with all pngs defined in ActorStates.
            /// </summary>
            public bool ExportActorStatesSpriteSheet { get; set; }

            /// <summary>
            /// File path for exporting a JSON file with all definitions in a sprite sheet.
            /// This is only applicable when ActorStates is defined.
            /// </summary>
            public string ExportActorStatesSpriteSheetJSONPath { get; set; }

            /// <summary>
            /// Whether to export animated GIF.
            /// This is only applicable when ActorStates is defined.
            /// </summary>
            public bool ExportActorStatesAnimatedGIF { get; set; }

            /// <summary>
            /// File path for exporting a CSS file for sprite animation.
            /// This is only applicable when ActorStates is defined.
            /// </summary>
            public string ExportActorStatesAnimatedCSSPath { get; set; }

            public bool ContainsExport_Full =>
                ExportFullSpriteSheet
                || !string.IsNullOrEmpty(ExportFullSpriteSheetJSONPath);

            public bool ContainsExport_States =>
                ExportActorStatesSpriteSheet
                || !string.IsNullOrEmpty(ExportActorStatesSpriteSheetJSONPath)
                || ExportActorStatesAnimatedGIF
                || !string.IsNullOrEmpty(ExportActorStatesAnimatedCSSPath);
        }

        class SpriteCollection
        {
            public string Name { get; private set; }

            Dictionary<string, SpriteFrameContainer> frames = new Dictionary<string, SpriteFrameContainer>(StringComparer.Ordinal);

            public List<SpriteFrameContainer> Frames
            {
                get
                {
                    List<SpriteFrameContainer> f = frames.Values.ToList();
                    f.Sort((x, y) => x.Frame.CompareTo(y.Frame));
                    return f;
                }
            }

            public Rectangle Region
            {
                get
                {
                    Rectangle r = Rectangle.Empty;
                    bool empty = true;
                    foreach (var kvp in frames)
                    {
                        r = empty ? kvp.Value.Region : Rectangle.Union(r, kvp.Value.Region);
                        empty = false;
                    }
                    return r;
                }
            }

            public int FrameCount => frames.Count;

            public int MaxRotationCountPerFrame
            {
                get
                {
                    int c = 0;
                    foreach (var kvp in frames)
                    {
                        c = Math.Max(c, kvp.Value.RotationCount);
                    }
                    return c;
                }
            }

            public SpriteCollection(string name)
            {
                Name = name;
            }

            public void AddSprite(Sprite sprite)
            {
                if (sprite.Name != Name)
                {
                    throw new Exception("Name mismatch.");
                }
                if (!frames.TryGetValue(sprite.Frame, out SpriteFrameContainer f))
                {
                    f = new SpriteFrameContainer(sprite.Frame);
                    frames.Add(sprite.Frame, f);
                }
                f.AddSprite(sprite);
            }
        }

        class SpriteFrameContainer
        {
            public string Frame { get; private set; }

            Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

            public List<Sprite> Sprites
            {
                get
                {
                    List<Sprite> s = sprites.Values.ToList();
                    s.Sort((x, y) => x.Rotation.CompareTo(y.Rotation));
                    return s;
                }
            }

            public Rectangle Region
            {
                get
                {
                    Rectangle r = Rectangle.Empty;
                    bool empty = true;
                    foreach (var kvp in sprites)
                    {
                        r = empty ? kvp.Value.Region : Rectangle.Union(r, kvp.Value.Region);
                        empty = false;
                    }
                    return r;
                }
            }

            public int RotationCount => sprites.Count;

            public SpriteFrameContainer(string frame)
            {
                Frame = frame;
            }

            public void AddSprite(Sprite sprite)
            {
                if (sprite.Frame != Frame)
                {
                    throw new Exception("Frame mismatch.");
                }
                sprites.Add(sprite.Rotation, sprite);
            }
        }

        class Sprite
        {
            public string Name { get; private set; }
            public string Frame { get; private set; }
            public string Rotation { get; private set; }
            public Image Image { get; private set; }
            public Rectangle Region { get; private set; }

            public Sprite(string name, string frame, string rotation, Image image, Rectangle region)
            {
                Name = name;
                Frame = frame;
                Rotation = rotation;
                Image = image;
                Region = region;
            }
        }

        class SpriteSheetJSON
        {
            public int SpriteWidth { get; set; }
            public int SpriteHeight { get; set; }
            public List<string> Frames { get; set; } = new List<string>();
        }

        class ActorStatesJSON
        {
            public List<string> States { get; set; }

            public string ExportName { get; set; }

            public class State
            {
                public string Frame { get; private set; }
                public int Duration { get; private set; }

                public State(string frame, int duration)
                {
                    Frame = frame;
                    Duration = duration;
                }
            }

            public List<State> GetAllStates()
            {
                List<State> states = new List<State>();
                foreach (string s in States)
                {
                    states.AddRange(GetStates(s));
                }
                return states;
            }

            public static List<State> GetStates(string state)
            {
                string[] tokens = state.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2) return null;

                List<State> states = new List<State>();
                if (!int.TryParse(tokens[1], out int duration)) return null;
                foreach (char c in tokens[0])
                {
                    states.Add(new State(c.ToString(), duration));
                }
                return states;
            }
        }

#if false
        /// <summary>
        /// Read all lumps (must be sprites) in a folder and output size and offset in a JavaScript object.
        /// Need to export all sprite lumps from slade first.
        /// </summary>
        static void ReadLumps(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("var SpriteInfoTable = {");
            foreach (var f in Directory.GetFiles(path, "*.lmp"))
            {
                try
                {
                    using (BinaryReader reader = new BinaryReader(File.Open(f, FileMode.Open)))
                    {
                        sb.AppendFormat("\t\"{0}\":{{w:{1},h:{2},x:{3},y:{4}}},",
                            Path.GetFileNameWithoutExtension(f),
                            reader.ReadInt16(),
                            reader.ReadInt16(),
                            reader.ReadInt16(),
                            reader.ReadInt16()).AppendLine();
                    }
                }
                catch { }
            }
            sb.AppendLine("};");
            File.WriteAllText(Path.Combine(path, "_SpriteData.txt"), sb.ToString());
        }
#endif
    }
}
