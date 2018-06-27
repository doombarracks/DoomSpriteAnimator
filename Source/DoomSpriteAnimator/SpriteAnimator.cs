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

namespace DoomSpriteAnimator
{
    /*
     * 現在 SpriteSheetJSON 會無法得知 ReferenceActorRegion!
     */
    public static class SpriteAnimator
    {
        /// <summary>
        /// Helper class for merging regions.
        /// </summary>
        class RegionMerger
        {
            /// <summary>
            /// Gets merged region.
            /// </summary>
            public Rectangle Region { get; private set; } = Rectangle.Empty;

            bool _first = true;

            public static Rectangle Merge(Rectangle a, Rectangle b)
                => Rectangle.Union(a, b);

            /// <summary>
            /// Merges another region.
            /// </summary>
            /// <param name="region">Region to be merged.</param>
            public void MergeWith(Rectangle region)
            {
                if (_first)
                {
                    Region = region;
                    _first = false;
                }
                else
                {
                    Region = Merge(Region, region);
                }
            }
        }

        class Sprite
        {
            /// <summary>
            /// Gets four-alphabet name of sprite.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// Gets frame character of sprite.
            /// </summary>
            public char Frame { get; private set; }

            /// <summary>
            /// Gets rotation character of sprite.
            /// </summary>
            public char Rotation { get; private set; }

            /// <summary>
            /// Gets full name of frame. (Name + frame, 5 characters long.)
            /// </summary>
            public string FullFrameName => Name + Frame;

            /// <summary>
            /// Gets full name of sprite. (Name + frame + rotation, 6 characters long.)
            /// </summary>
            public string FullSpriteName => FullFrameName + Rotation;

            /// <summary>
            /// Gets image content of this sprite.
            /// </summary>
            public Image Image { get; private set; }

            /// <summary>
            /// Gets offset information of this sprite.
            /// </summary>
            public Point Offset { get; private set; }

            /// <summary>
            /// Gets combined sprite size and offset information into a rectangular region in pixels for representing where this sprite should be drawn.
            /// </summary>
            public Rectangle Region { get; private set; }

            public Sprite(string name, char frame, char rotation, Image image, int offsetX, int offsetY)
            {
                if (name == null || name.Length != 4)
                {
                    throw new ArgumentException("Sprite's \"Name\" field should be 4 alphabets in length.");
                }
                Image = image ?? throw new ArgumentNullException("Sprite image cannot be null.");

                Name = name.ToUpper();
                Frame = char.ToUpper(frame);
                Rotation = char.ToUpper(rotation);

                Offset = new Point(offsetX, offsetY);
                Region = new Rectangle(-offsetX, -offsetY, image.Width, image.Height);
            }

            /// <summary>
            /// Does mirroring of this sprite and does all necessary changes so it can still be drawn correctly.
            /// </summary>
            /// <param name="rotation">Rotation after mirroring.</param>
            /// <returns>New sprite after mirroring.</returns>
            public Sprite GetMirror(char frame, char rotation)
            {
                Image img = (Image)Image.Clone();
                img.RotateFlip(RotateFlipType.Rotate180FlipY);
                return new Sprite(Name, frame, rotation, img, img.Width - Offset.X, Offset.Y);
            }

            /// <summary>
            /// Draws sprite on another image.
            /// </summary>
            /// <param name="g">Graphic object of the image to be drawn.</param>
            /// <param name="origin">Left-top corner to draw this sprite.</param>
            /// <param name="region">Region area for this sprite to align offset. (Usually be the maximum offset and sprite size so all sprites can fit.)</param>
            public void Draw(Graphics g, Point origin, Rectangle region)
                => g.DrawImage(Image, new Rectangle(origin.X + (Region.X - region.X), origin.Y + (Region.Y - region.Y), Image.Width, Image.Height));

            public override string ToString()
                => "Sprite: " + FullSpriteName;
        }

        /// <summary>
        /// Container of sprites for specific frame with all rotations.
        /// </summary>
        class SpriteFrame
        {
            Dictionary<char, Sprite> _sprites = new Dictionary<char, Sprite>();

            /// <summary>
            /// Gets four-alphabet name of all sprites in this frame.
            /// </summary>
            public string Name { get; private set; } = "    ";

            /// <summary>
            /// Gets frame character of all sprites in this frame.
            /// </summary>
            public char Frame { get; private set; } = 'A';

            /// <summary>
            /// Gets full name of frame. (Name + frame, 5 characters long.)
            /// </summary>
            public string FullFrameName => Name + Frame;

            /// <summary>
            /// Gets sprite by rotation.
            /// </summary>
            /// <param name="rotation">Rotation to find sprite.</param>
            /// <returns>
            /// Find sprite by rotation.
            /// If sprite is not found and rotation is not '0', it will try to find '0' rotation.
            /// If sprite is not found and rotation is '0', it will try to find '1' rotation.
            /// If all attempts fail, null will be returned.
            /// </returns>
            public Sprite this[char rotation]
            {
                get
                {
                    char r = char.ToUpper(rotation);
                    return _sprites.TryGetValue(r, out Sprite s)
                        ? s
                        : (_sprites.TryGetValue(r == '0' ? '1' : '0', out s) ? s : null);
                }
            }

            /// <summary>
            /// Gets a list of sprites in this sprite frame. (Sorted by rotation.)
            /// </summary>
            public IList<Sprite> Sprites
            {
                get
                {
                    if (_sortedSprites == null)
                    {
                        List<Sprite> list = _sprites.Values.ToList();
                        list.Sort((a, b) => a.Rotation.CompareTo(b.Rotation));
                        _sortedSprites = list.AsReadOnly();
                        _rotations = null;
                    }
                    return _sortedSprites;
                }
            }
            IList<Sprite> _sortedSprites = null;

            /// <summary>
            /// Gets all keys available in this frame.
            /// </summary>
            public char[] Rotations
            {
                get
                {
                    IList<Sprite> list = Sprites;
                    if (_rotations == null)
                    {
                        _rotations = list.Select(x => x.Rotation).ToArray();
                    }
                    return _rotations;
                }
            }
            char[] _rotations = null;

            /// <summary>
            /// Gets the merged region of all rotations.
            /// </summary>
            public Rectangle Region
            {
                get
                {
                    RegionMerger rm = new RegionMerger();
                    foreach (var kvp in _sprites)
                    {
                        rm.MergeWith(kvp.Value.Region);
                    }
                    return rm.Region;
                }
            }

            /// <summary>
            /// Extracts specified rotations to a new sprite frame.
            /// </summary>
            /// <param name="rotations">Rotations to be extracted.</param>
            /// <returns>New sprite frame with specified rotations only.</returns>
            public SpriteFrame ExtractRotations(char[] rotations)
            {
                SpriteFrame sf = new SpriteFrame();
                foreach (char c in rotations)
                {
                    sf.AddSprite(this[c]);
                }
                return sf;
            }

            /// <summary>
            /// Adds a sprite to this sprite frame.
            /// If rotation of this sprite already exists, this old one will be overwritten.
            /// If sprite name and frame conflicts with existing ones in this sprite frame, an exception will be thrown.
            /// </summary>
            /// <param name="sprite">Sprite to add to this sprite frame.</param>
            public void AddSprite(Sprite sprite)
            {
                // Sanity check.
                if (sprite == null) return;

                char r = char.ToUpper(sprite.Rotation);
                if (_sprites.Count > 0)
                {
                    if (Name != sprite.Name)
                    {
                        throw new Exception("Sprite name mismatch.");
                    }
                    if (Frame != sprite.Frame)
                    {
                        throw new Exception("Sprite frame mismatch.");
                    }

                    if (!_sprites.ContainsKey(r))
                    {
                        _sprites.Add(r, sprite);
                    }
                    else
                    {
                        _sprites[r] = sprite;
                    }
                }
                else
                {
                    Name = sprite.Name;
                    Frame = sprite.Frame;
                    _sprites.Add(r, sprite);
                }
                _sortedSprites = null;
            }

            public override string ToString()
                => "SpriteFrame: " + Name + " " + Frame;
        }

        /// <summary>
        /// A collection of sprites.
        /// </summary>
        class SpritePool
        {
            Dictionary<string, Sprite> _pool = new Dictionary<string, Sprite>();

            /// <summary>
            /// Gets all sprites sorted by name.
            /// </summary>
            public IList<Sprite> Sprites
            {
                get
                {
                    if (_sprites == null)
                    {
                        List<Sprite> list = _pool.Values.ToList();
                        list.Sort((a, b) => a.FullSpriteName.CompareTo(b.FullSpriteName));
                        _sprites = list.AsReadOnly();
                    }
                    return _sprites;
                }
            }
            IList<Sprite> _sprites = null;

            /// <summary>
            /// Gets all sprites with specified full frame name sorted by rotation.
            /// </summary>
            /// <param name="fullFrameName">Full frame name to find sprites.</param>
            /// <returns>Sprite array with full frame name specified.</returns>
            public Sprite[] GetSprite(string fullFrameName)
            {
                fullFrameName = fullFrameName.ToUpper();
                IList<Sprite> list = Sprites;
                return list.Where(x => x.FullFrameName == fullFrameName).ToArray();
            }

            /// <summary>
            /// Gets a sprite from specified full frame name and rotation.
            /// </summary>
            /// <param name="fullFrameName">Full frame name to find sprites.</param>
            /// <param name="rotation">Rotation of the sprite to find.</param>
            /// <returns>
            /// Sprite with specified names.
            /// If no sprite is found, null will be returned.
            /// </returns>
            public Sprite GetSprite(string fullFrameName, char rotation)
                => _pool.TryGetValue(fullFrameName + char.ToUpper(rotation), out Sprite s)
                    ? s
                    : (_pool.TryGetValue(fullFrameName + char.ToUpper(rotation == '0' ? '1' : '0'), out s) ? s : null);

            /// <summary>
            /// Adds a sprite to the pool.
            /// If sprite already exists, the old one will be overwritten.
            /// </summary>
            /// <param name="sprite">Sprite to add to pool.</param>
            public void AddSprite(Sprite sprite)
            {
                if (_pool.ContainsKey(sprite.FullSpriteName))
                {
                    _pool[sprite.FullSpriteName] = sprite;
                }
                else
                {
                    _pool.Add(sprite.FullSpriteName, sprite);
                }
            }

            public override string ToString()
                => "SpritePool: " + _pool.Count + " sprite" + (_pool.Count > 1 ? "s" : "") + ".";
        }

        /// <summary>
        /// An actor state.
        /// </summary>
        class State
        {
            /// <summary>
            /// Gets four-alphabet name of sprite.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// Gets frame character of sprite.
            /// </summary>
            public char Frame { get; private set; }

            /// <summary>
            /// Gets full name of frame. (Name + frame, 5 characters long.)
            /// </summary>
            public string FullFrameName => Name + Frame;

            /// <summary>
            /// Duration in Doom tics. (35 tics per second.)
            /// </summary>
            public int Duration { get; private set; }

            public State(string name, char frame, int duration)
            {
                if (name == null || name.Length != 4)
                {
                    throw new ArgumentException("State's \"Name\" field should be 4 alphabets in length.");
                }

                Name = name.ToUpper();
                Frame = char.ToUpper(frame);
                Duration = duration;
            }

            public override string ToString()
                => "State: " + Name + " " + Frame + " " + Duration;
        }

        /// <summary>
        /// A collection of actor states and other definitions.
        /// </summary>
        class Actor
        {
            /// <summary>
            /// Name of this actor.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// List of all states specified.
            /// </summary>
            public IList<State> States { get; private set; }
            
            /// <summary>
            /// All rotations of this actor will be used.
            /// </summary>
            public char[] Rotations { get; private set; }

            /// <summary>
            /// Whether to consider size and offset among all rotations.
            /// </summary>
            public bool RegionAmongAllRotations { get; private set; }

            /// <summary>
            /// Gets a name list for referencing other regions so they can be displayed seamlessly switching to others.
            /// When this field is defined, RegionAmongAllRotations will be ignored.
            /// </summary>
            public string[] ReferencedActorRegionNames { get; private set; }

            /// <summary>
            /// Gets whether we defined reference actors for drawing regions.
            /// </summary>
            public bool HasReferencedActorRegion => ReferencedActorRegionNames != null && ReferencedActorRegionNames.Length > 0;

            /// <summary>
            /// Constructs an actor by JSON.
            /// </summary>
            /// <param name="name">Name of the actor.</param>
            /// <param name="json">JSON object to pass the information.</param>
            public Actor(string name, ActorJSON json)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Actor name must be specified.");
                }
                if (json == null)
                {
                    throw new ArgumentNullException("Actor JSON cannot be null.");
                }

                string rotation = string.IsNullOrEmpty(json.Rotations) ? "1" : json.Rotations;

                Name = name;
                States = json.GetStates().AsReadOnly();
                RegionAmongAllRotations = json.RegionAmongAllRotations;
                ReferencedActorRegionNames = json.ReferencedActorRegionNames;

                // Remove duplicates. (We do not sort since the order might be useful.)
                Rotations = rotation.ToList().Distinct().ToArray();
            }

            public override string ToString()
                => "Actor: " + Name;
        }

        class ActorJSON
        {
            /// <summary>
            /// All states text. (Format: TEST ABCDEFGH 8)
            /// </summary>
            public string[] States { get; set; }

            /// <summary>
            /// All rotations of this actor will be used.
            /// </summary>
            public string Rotations { get; set; }

            /// <summary>
            /// Whether to consider size and offset among all rotations.
            /// </summary>
            public bool RegionAmongAllRotations { get; set; }

            /// <summary>
            /// For referencing other regions so they can be displayed seamlessly switching to others.
            /// When this field is defined, RegionAmongAllRotations will be ignored.
            /// </summary>
            public string[] ReferencedActorRegionNames { get; set; }

            /// <summary>
            /// Gets all states in this JSON.
            /// </summary>
            /// <returns>List of all states contained in this JSON.</returns>
            public List<State> GetStates()
            {
                List<State> states = new List<State>();
                foreach (string s in States)
                {
                    ParseState(states, s);
                }
                return states;
            }

            /// <summary>
            /// Parses a state string and add parsed states to list.
            /// </summary>
            /// <param name="list">List to put the states.</param>
            /// <param name="state">State string to be parsed.</param>
            static void ParseState(List<State> list, string state)
            {
                if (list == null || string.IsNullOrEmpty(state)) return;

                string[] tokens = state.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 3) return;

                string name = tokens[0],
                    frames = tokens[1],
                    duration = tokens[2];
                if (name.Length != 4 || !int.TryParse(duration, out int d)) return;
                foreach (char c in frames)
                {
                    list.Add(new State(name, c, d));
                }
            }
        }

        class SpriteSheetFindResult
        {
            /// <summary>
            /// Gets sprite frame we are looking for / sprite belongs to.
            /// </summary>
            public SpriteFrame SpriteFrame { get; private set; } = null;

            /// <summary>
            /// Gets sprite we are looking for.
            /// If we are looking for sprite frame only, whis field is null.
            /// </summary>
            public Sprite Sprite { get; private set; } = null;

            /// <summary>
            /// Row index of this sprite sheet.
            /// This number represents the index of sprite frame.
            /// </summary>
            public int RowIndex { get; private set; } = -1;

            /// <summary>
            /// Column index of this sprite.
            /// This number represents the rotation index (not rotation character) of sprite.
            /// I we are looking for sprite frame only, whis field is -1.
            /// </summary>
            public int ColumnIndex { get; private set; } = -1;

            public SpriteSheetFindResult(SpriteFrame spriteFrame, Sprite sprite, int rowIndex, int columnIndex)
            {
                SpriteFrame = spriteFrame;
                Sprite = sprite;
                RowIndex = rowIndex;
                ColumnIndex = columnIndex;
            }

            public SpriteSheetFindResult(SpriteFrame spriteFrame, int rowIndex)
            {
                SpriteFrame = spriteFrame;
                RowIndex = rowIndex;
            }
        }

        /// <summary>
        /// A sprite sheet with a collection of sprite frames.
        /// </summary>
        class SpriteSheet
        {
            Dictionary<string, SpriteFrame> _spriteFramesTable = new Dictionary<string, SpriteFrame>();

            /// <summary>
            /// Gets a sorted list of all sprite frames.
            /// </summary>
            public IList<SpriteFrame> SpriteFrames
            {
                get
                {
                    if (_spriteFrames == null)
                    {
                        List<SpriteFrame> list = _spriteFramesTable.Values.ToList();
                        list.Sort((a, b) => a.FullFrameName.CompareTo(b.FullFrameName));
                        _spriteFrames = list.AsReadOnly();
                    }
                    return _spriteFrames;
                }
            }
            IList<SpriteFrame> _spriteFrames = null;

            /// <summary>
            /// Gets the merged region of all sprite frames in this sprite sheet.
            /// </summary>
            public Rectangle Region
            {
                get
                {
                    RegionMerger rm = new RegionMerger();
                    foreach (var sf in SpriteFrames)
                    {
                        rm.MergeWith(sf.Region);
                    }
                    return rm.Region;
                }
            }

            /// <summary>
            /// Gets sprite sheet from actor and sprite pool.
            /// </summary>
            /// <param name="actor">Actor for providing states and name of sprites to be used.</param>
            /// <param name="spritePool">Sprite pool for providing actual content of sprites.</param>
            /// <returns></returns>
            static public SpriteSheet FromActorAndSpritePool(Actor actor, SpritePool spritePool)
            {
                if (actor == null || spritePool == null)
                {
                    throw new ArgumentNullException("Input arguments cannot be null.");
                }

                // Gets a list of unique sprite names.
                HashSet<string> frameList = new HashSet<string>();
                foreach (State s in actor.States)
                {
                    if (!frameList.Contains(s.FullFrameName))
                    {
                        frameList.Add(s.FullFrameName);
                    }
                }

                // Makes sprite sheet.
                SpriteSheet ss = new SpriteSheet();
                foreach (string f in frameList)
                {
                    foreach (char r in actor.Rotations)
                    {
                        ss.AddSprite(spritePool.GetSprite(f, r));
                    }
                }

                return ss;
            }

            /// <summary>
            /// Adds a sprite to this sprite sheet.
            /// </summary>
            /// <param name="sprite">Sprite to add to this sprite sheet.</param>
            public void AddSprite(Sprite sprite)
            {
                if (sprite == null) return;

                if (!_spriteFramesTable.TryGetValue(sprite.FullFrameName, out SpriteFrame sf))
                {
                    sf = new SpriteFrame();
                    _spriteFramesTable.Add(sprite.FullFrameName, sf);
                    _spriteFrames = null;
                }
                sf.AddSprite(sprite);
            }

            /// <summary>
            /// Adds a sprite frame to this sprite sheet.
            /// If the sprite frame with the same "FullFrameName" already exists, the old one will be overwritten.
            /// </summary>
            /// <param name="spriteFrame">Sprite frame to be added to this sprite sheet.</param>
            public void AddSpriteFrame(SpriteFrame spriteFrame)
            {
                if (spriteFrame == null) return;

                if (!_spriteFramesTable.ContainsKey(spriteFrame.FullFrameName))
                {
                    _spriteFramesTable.Add(spriteFrame.FullFrameName, spriteFrame);
                }
                else
                {
                    _spriteFramesTable[spriteFrame.FullFrameName] = spriteFrame;
                }
                _spriteFrames = null;
            }

            /// <summary>
            /// Extracts specified rotations to a new sprite sheet.
            /// </summary>
            /// <param name="rotations">Rotations to be extracted.</param>
            /// <returns>New sprite sheet with specified rotations only.</returns>
            public SpriteSheet ExtractRotation(params char[] rotations)
            {
                SpriteSheet ss = new SpriteSheet();
                foreach (SpriteFrame sf in SpriteFrames)
                {
                    ss.AddSpriteFrame(sf.ExtractRotations(rotations));
                }
                return ss;
            }

            /// <summary>
            /// Finds sprite frame by name.
            /// </summary>
            /// <param name="fullFrameName">Full frame name to find.</param>
            /// <returns>Result of the finding.</returns>
            public SpriteSheetFindResult FindSpriteFrame(string fullFrameName)
            {
                if (fullFrameName == null || fullFrameName.Length != 5)
                {
                    throw new ArgumentException("Invalid full frame name.");
                }

                fullFrameName = fullFrameName.ToUpper();

                // Find row.
                int row = -1;
                for (int i = 0; i < SpriteFrames.Count; i++)
                {
                    if (SpriteFrames[i].FullFrameName == fullFrameName)
                    {
                        row = i;
                        break;
                    }
                }
                return row >= 0
                    ? new SpriteSheetFindResult(SpriteFrames[row], row)
                    : null;
            }

            /// <summary>
            /// Finds sprite by name.
            /// </summary>
            /// <param name="fullFrameName">Full frame name to find.</param>
            /// <param name="rotation">Rotation of sprite.</param>
            /// <returns>Result of the finding.</returns>
            public SpriteSheetFindResult FindSprite(string fullFrameName, char rotation)
            {
                SpriteSheetFindResult ssfr = FindSpriteFrame(fullFrameName);
                if (ssfr == null) return null;

                Sprite s = ssfr.SpriteFrame[rotation];
                return new SpriteSheetFindResult(
                    ssfr.SpriteFrame,
                    s,
                    ssfr.RowIndex,
                    Array.IndexOf(ssfr.SpriteFrame.Rotations, char.ToUpper(rotation)));
            }

            /// <summary>
            /// Gets an array of string representing all frames and rotations of this sprite sheet.
            /// </summary>
            /// <returns>Texts of this sprite sheet. If there is nothing, null will be returned.</returns>
            public string[] GetTextList()
            {
                IList<SpriteFrame> spriteFrames = SpriteFrames;
                if (spriteFrames.Count == 0) return null;

                StringBuilder sb = new StringBuilder();
                List<string> texts = new List<string>(spriteFrames.Count);
                foreach (SpriteFrame sf in spriteFrames)
                {
                    sb.Clear();
                    sb.Append(sf.Name + " " + sf.Frame + " ");
                    foreach (Sprite s in sf.Sprites)
                    {
                        sb.Append(s.Rotation);
                    }
                    texts.Add(sb.ToString());
                }

                return texts.ToArray();
            }

            /// <summary>
            /// Gets an image of current sprite sheet.
            /// </summary>
            /// <param name="extraRegion">An extra region to consider when drawing sprites if specified. It will be merged to the region of the region of this sprite sheet itself.</param>
            /// <returns>Bitmap of this sprite sheet. If there is nothing, null will be returned.</returns>
            public Bitmap GetImage(Rectangle? extraRegion = null)
            {
                IList<SpriteFrame> spriteFrames = SpriteFrames;
                if (spriteFrames.Count == 0) return null;

                Rectangle region = extraRegion.HasValue
                    ? RegionMerger.Merge(Region, extraRegion.Value)
                    : Region;

                // Construct empty image.
                Bitmap bmp = new Bitmap(
                    spriteFrames.Max(x => x.Sprites.Count) * region.Width,
                    spriteFrames.Count * region.Height);

                // Draw every frame and sprite.
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CompositingMode = CompositingMode.SourceOver;
                    Point origin = new Point(0, 0); // Anchor position for bitmap.
                    foreach (SpriteFrame sf in spriteFrames)
                    {
                        origin.X = 0;
                        foreach (Sprite s in sf.Sprites)
                        {
                            s.Draw(g, origin, region);
                            origin.X += region.Width;
                        }
                        origin.Y += region.Height;
                    }
                }

                return bmp;
            }

            /// <summary>
            /// Gets a region of all other referenced actors.
            /// </summary>
            /// <param name="actors">Actor list to find reference actors.</param>
            /// <param name="spritePool">Sprite pool for finding region.</param>
            /// <param name="referenceActorNames">Actor names to be referenced for region.</param>
            /// <returns>Region merged with all reference actor regions. If no reference actors are define, the return value is the same as "Region" property.</returns>
            public Rectangle GetRegionWithActors(List<Actor> actors, SpritePool spritePool, string[] referenceActorNames)
            {
                if (referenceActorNames == null || referenceActorNames.Length == 0) return Region;

                List<Actor> referenced = actors.FindAll(x => referenceActorNames.Count(y => x.Name == y) > 0);
                RegionMerger rm = new RegionMerger();
                rm.MergeWith(Region);
                foreach (Actor a in referenced)
                {
                    rm.MergeWith(FromActorAndSpritePool(a, spritePool).Region);
                }
                return rm.Region;
            }
        }

        class SpriteSheetJSON
        {
            /// <summary>
            /// Width for every sprite.
            /// </summary>
            public int SpriteWidth { get; set; }

            /// <summary>
            /// Width for every height.
            /// </summary>
            public int SpriteHeight { get; set; }

            /// <summary>
            /// List of all frames. 
            /// </summary>
            public string[] Frames { get; set; }

            public SpriteSheetJSON() { }

            public SpriteSheetJSON(SpriteSheet spriteSheet, Rectangle? extraRegion = null)
            {
                if (spriteSheet == null)
                {
                    throw new ArgumentNullException("Sprite sheet cannot be null.");
                }

                Rectangle region = extraRegion.HasValue
                    ? RegionMerger.Merge(spriteSheet.Region, extraRegion.Value)
                    : spriteSheet.Region;
                SpriteWidth = region.Width;
                SpriteHeight = region.Height;
                Frames = spriteSheet.GetTextList();
            }
        }

        /// <summary>
        /// Gets a list of actors from JSON.
        /// </summary>
        /// <param name="actorJSONs">JSONs to be converted to actors.</param>
        /// <returns>Actors converted from JSONs.</returns>
        static List<Actor> GetActorsFromJSON(Dictionary<string, ActorJSON> actorJSONs)
        {
            List<Actor> actors = new List<Actor>();
            foreach (var kvp in actorJSONs)
            {
                actors.Add(new Actor(kvp.Key, kvp.Value));
            }
            return actors;
        }

        /// <summary>
        /// Finds all associated png images from actor states.
        /// </summary>
        /// <param name="actors">All actors for providing all possible sprite names.</param>
        /// <param name="pngDirectory">Directory for all PNG images to look for. (Sub-directories will not be searched.)</param>
        /// <returns>Any array of PNG image paths referenced by all actors.</returns>
        static string[] FindReferencedPngs(List<Actor> actors, string pngDirectory)
        {
            // Find all possible files referenced in states.
            string[] pngPaths = Directory.GetFiles(pngDirectory, "*.png");

            // Generate a big dictionary with all possible sprite name, frame and rotation combinations.
            Dictionary<string, string> lumpName2ImagePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in pngPaths)
            {
                string fn = Path.GetFileNameWithoutExtension(path);
                switch (fn.Length)
                {
                    case 6:
                        lumpName2ImagePath.Add(fn, path);
                        break;
                    case 8:
                        lumpName2ImagePath.Add(fn.Substring(0, 6), path);
                        lumpName2ImagePath.Add(fn.Substring(0, 4) + fn.Substring(6, 2), path);
                        break;
                }
            }

            // Find all referenced files from states.
            HashSet<string> fileList = new HashSet<string>();
            foreach (Actor a in actors)
            {
                char[] rotations = AddZeroRotation(a.Rotations);
                foreach (State s in a.States)
                {
                    foreach (char r in rotations)
                    {
                        string name = s.FullFrameName + r;
                        if (lumpName2ImagePath.TryGetValue(name, out string fn))
                        {
                            if (!fileList.Contains(fn)) fileList.Add(fn);
                            lumpName2ImagePath.Remove(name);
                        }
                    }
                }
            }

            return fileList.ToArray();
        }

        /// <summary>
        /// Gets a pool of all sprites.
        /// </summary>
        /// <param name="pngPaths">
        /// All PNG images to load for sprites.
        /// NOTE: If specified PNG does not contain grAb chunk for offset data, (0, 0) offset will be considered.
        /// </param>
        /// <returns>Sprite pool with all sprites loaded from specified PNG paths.</returns>
        static SpritePool GetSpritePool(string[] pngPaths)
        {
            SpritePool spritePool = new SpritePool();
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

                    // Add all possible sprites.
                    Sprite sprite = new Sprite(fn.Substring(0, 4), fn[4], fn[5], Image.FromStream(new MemoryStream(bytes)), x, y);
                    spritePool.AddSprite(sprite);
                    if (fn.Length == 8)
                    {
                        spritePool.AddSprite(sprite.GetMirror(fn[6], fn[7]));
                    }
                }
                catch { }
            }
            return spritePool;
        }

        /// <summary>
        /// Automatically creates a directory if it deosn't exist and make sure it is a relative path instead of an absolute one.
        /// </summary>
        /// <param name="directory">Directory to be checked.</param>
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
        /// <param name="paths">All paths to be merged.</param>
        /// <returns>Merged path.</returns>
        static string MergePath(params string[] paths)
            => Path.Combine(paths).Trim('\\');

        /// <summary>
        /// Adds rotation '0' for file catching purpose.
        /// </summary>
        /// <param name="rotations">Orginial rotations.</param>
        /// <returns>Rotations with '0'.</returns>
        static char[] AddZeroRotation(char[] rotations)
        {
            if (rotations.Contains('0')) return rotations;

            List<char> list = rotations.ToList();
            list.Add('0');
            return list.ToArray();
        }

        /// <summary>
        /// Creates a name with rotation ending.
        /// </summary>
        /// <param name="name">Name to add rotation ending. Should contain extension or it will be ruined.</param>
        /// <param name="rotation">Rotaion to be added.</param>
        /// <returns>New name with rotation ending.</returns>
        static string GetNameWithRotation(string name, char rotation)
            =>  rotation == '0'
                ? name
                : string.Format("{0}{1}R{2}", name, name.EndsWith("_") ? "" : "_", rotation);

        /// <summary>
        /// Exports sprite sheets to specified path.
        /// </summary>
        /// <param name="actors">Actors for providing states and sprite names to be drawn.</param>
        /// <param name="spritePool">Sprite pool for providing actual sprite data.</param>
        /// <param name="baseDirectory">Relative directory to output sprite sheets.</param>
        /// <param name="namePrefix">Name prefix of all sprite sheet images.</param>
        static void ExportSpriteSheets(List<Actor> actors, SpritePool spritePool, string baseDirectory, string namePrefix)
        {
            CreateDirectoryIfNotExist(baseDirectory);

            foreach (Actor a in actors)
            {
                SpriteSheet ss = SpriteSheet.FromActorAndSpritePool(a, spritePool);
                Rectangle region = a.HasReferencedActorRegion // Not a beautiful implementation, but it takes too much modifications to make it work simpler.
                    ? ss.GetRegionWithActors(actors, spritePool, a.ReferencedActorRegionNames)
                    : ss.Region;
                using (Bitmap bmp = ss.GetImage(region))
                {
                    bmp?.Save(MergePath(baseDirectory, namePrefix + a.Name + ".png"));
                }
            }
        }

        /// <summary>
        /// Gets sprite sheet JSON table for all sprite sheets.
        /// </summary>
        /// <param name="actors">Actors for building sprite sheets for JSONs.</param>
        /// <param name="spritePool">Sprite pool for building sprite sheets for JSONS.</param>
        /// <param name="namePrefix">Name prefix for image names to be written as the key of the table.</param>
        /// <returns>Table o sprite sheet JSONs.</returns>
        static Dictionary<string, SpriteSheetJSON> GetSpriteSheetJSON(List<Actor> actors, SpritePool spritePool, string namePrefix)
        {
            Dictionary<string, SpriteSheetJSON> jsons = new Dictionary<string, SpriteSheetJSON>(StringComparer.OrdinalIgnoreCase);
            foreach (Actor a in actors)
            {
                string name = namePrefix + a.Name;
                SpriteSheet ss = SpriteSheet.FromActorAndSpritePool(a, spritePool);
                Rectangle region = a.HasReferencedActorRegion // Not a beautiful implementation, but it takes too much modifications to make it work simpler.
                    ? ss.GetRegionWithActors(actors, spritePool, a.ReferencedActorRegionNames)
                    : ss.Region;
                SpriteSheetJSON ssJSON = new SpriteSheetJSON(ss, region);

                if (jsons.ContainsKey(name))
                {
                    jsons[name] = ssJSON;
                }
                else
                {
                    jsons.Add(name, ssJSON);
                }
            }
            return jsons;
        }

        /// <summary>
        /// Exports a CSS file with a testing HTML file to view the result of sprite animation by CSS3.
        /// </summary>
        /// <param name="actors">Actors for exporting CSS animation.</param>
        /// <param name="spritePool">Sprite pool for building sprite sheets for CSS animation.</param>
        /// <param name="baseDirectory">Relative directory to output files.</param>
        /// <param name="namePrefix">Name prefix of all sprite sheet images.</param>
        /// <param name="cssFileName">Output file name for CSS file.</param>
        static void ExportCSSAnimations(List<Actor> actors, SpritePool spritePool, string baseDirectory, string namePrefix, string cssFileName)
        {
            if (string.IsNullOrEmpty(cssFileName))
            {
                return;
            }
            
            CreateDirectoryIfNotExist(baseDirectory);

            if (Path.GetExtension(cssFileName) != ".css")
            {
                string newFileName = Path.ChangeExtension(cssFileName, "css");
                ConsolePrint.Warning("CSS file extension must be \".css\". Output path will be changed from \"{0}\" to \"{1}\".", cssFileName, newFileName);
                cssFileName = newFileName;
            }

            // Create test html.
            StringBuilder html = new StringBuilder();
            html.AppendLine("<html>")
                .AppendLine("<head>")
                .AppendLine("<title>Test</title>")
                .AppendLine("<link rel=\"stylesheet\" type=\"text/css\" href=\"" + cssFileName + "\">")
                .AppendLine("<style>")
                .AppendLine("body { background-color: black }")
                .AppendLine("</style>")
                .AppendLine("</head>")
                .AppendLine("<body>");

            // Create css script in memory.
            StringBuilder css = new StringBuilder();
            foreach (Actor a in actors)
            {
                string imgBaseName = namePrefix + a.Name;

                // Total amount of duration.
                double totalDuration = a.States.Sum(x => x.Duration);

                // Get sprite sheet and region.
                SpriteSheet ss = SpriteSheet.FromActorAndSpritePool(a, spritePool);
                Rectangle region = a.HasReferencedActorRegion // Not a beautiful implementation, but it takes too much modifications to make it work simpler.
                    ? ss.GetRegionWithActors(actors, spritePool, a.ReferencedActorRegionNames)
                    : ss.Region;
                foreach (char r in a.Rotations)
                {
                    string imgName = GetNameWithRotation(imgBaseName, r);
                    string className = "SAni_" + imgName;
                    string kfName = "SKF_" + imgName;

                    // Generate class first.
                    css.AppendFormat(".{0} {{", className).AppendLine()
                        .AppendFormat("\twidth: {0}px;", region.Width).AppendLine()
                        .AppendFormat("\theight: {0}px;", region.Height).AppendLine()
                        .AppendFormat("\tbackground: url('{0}.png');", imgBaseName).AppendLine()
                        .AppendFormat("\tanimation: {0} {1:0.000}s step-end infinite;", kfName, totalDuration / 35.0).AppendLine()
                        .AppendLine("}");

                    // Generate key frames.
                    css.AppendFormat("@keyframes {0} {{", kfName).AppendLine();
                    int duration = 0;
                    foreach (State s in a.States)
                    {
                        // Skip zero duration.
                        if (s.Duration <= 0)
                        {
                            continue;
                        }

                        // Find sprite position in sprite sheet.
                        SpriteSheetFindResult find = ss.FindSprite(s.FullFrameName, r);
                        if (find == null)
                        {
                            throw new Exception("Sprite not found to export CSS animation.");
                        }

                        css.AppendFormat("\t{0:0.00}% {{ background-position: {1}px {2}px }}",
                            duration / totalDuration * 100.0, -find.ColumnIndex * region.Width, -find.RowIndex * region.Height).AppendLine();
                        duration += s.Duration;
                    }
                    css.AppendLine("}").AppendLine();

                    html.AppendLine("<div class=\"" + className + "\"></div>");
                }
            }

            // Export css file.
            File.WriteAllText(MergePath(baseDirectory, cssFileName), css.ToString());

            // Export testing html file.
            html.AppendLine("</body>")
                .AppendLine("</html>");
            File.WriteAllText(MergePath(baseDirectory, Path.ChangeExtension(cssFileName, "html")), html.ToString());
        }

        /// <summary>
        /// Exports GIF images according to the actors spcified.
        /// </summary>
        /// <param name="actors">Actors for providing states and sprite names to be drawn.</param>
        /// <param name="spritePool">Sprite pool for providing actual sprite data.</param>
        /// <param name="baseDirectory">Relative directory to output GIF images.</param>
        /// <param name="namePrefix">Name prefix of all GIF images.</param>
        static void ExportAnimatedGIFs(List<Actor> actors, SpritePool spritePool, string baseDirectory, string namePrefix)
        {
            CreateDirectoryIfNotExist(baseDirectory);

            Point origin = new Point(0, 0); // Will be used for drawing sprite.
            foreach (Actor a in actors)
            {
                string imgBaseName = namePrefix + a.Name;

                // Get sprite sheet.
                SpriteSheet ss = SpriteSheet.FromActorAndSpritePool(a, spritePool);
                foreach (char r in a.Rotations)
                {
                    string imgName = GetNameWithRotation(imgBaseName, r);
                    Rectangle region = a.HasReferencedActorRegion // Not a beautiful implementation, but it takes too much modifications to make it work simpler.
                        ? ss.GetRegionWithActors(actors, spritePool, a.ReferencedActorRegionNames)
                        : (a.RegionAmongAllRotations ? ss.Region : ss.ExtractRotation(r).Region);

                    // Construct GIF image.
                    AnimatedGifCreator gif = AnimatedGif.AnimatedGif.Create(MergePath(baseDirectory, imgName + ".gif"), 1000 / 35);
                    Dictionary<Sprite, Bitmap> processedSprites = new Dictionary<Sprite, Bitmap>();

                    foreach (State state in a.States)
                    {
                        if (state.Duration <= 0)
                        {
                            continue;
                        }

                        // Create necessary image for this sprite.
                        Sprite sprite = spritePool.GetSprite(state.FullFrameName, r);
                        if (sprite == null)
                        {
                            ConsolePrint.Warning("Cannot find sprite \"{0}\". GIF \"{1}\" export aborted.",  state.FullFrameName + r, Path.GetFileName(gif.FilePath));
                            break;
                        }
                        if (!processedSprites.TryGetValue(sprite, out Bitmap gifFrame))
                        {
                            gifFrame = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb); // Pixel format cannot be indexed or its graphics cannot be retrieved.
                            using (Graphics ggif = Graphics.FromImage(gifFrame))
                            {
                                sprite.Draw(ggif, origin, region);
                            }
                            processedSprites.Add(sprite, gifFrame);
                        }

                        gif.AddFrame(gifFrame, (int)Math.Round(state.Duration * 1000.0 / 35.0), GifQuality.Bit8);
                    }

                    gif.Dispose();
                }
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
            public Dictionary<string, ActorJSON> Actors { get; set; }

            /// <summary>
            /// Define base directory for all exported files.
            /// </summary>
            public string ExportBaseDirectory { get; set; }

            /// <summary>
            /// Define name prefix for all exported files.
            /// </summary>
            public string ExportNamePrefix { get; set; }

            /// <summary>
            /// Whether to export a sprite sheet with all pngs defined in Actors.
            /// </summary>
            public bool ExportSpriteSheet { get; set; }

            /// <summary>
            /// File path for exporting a JSON file with all definitions in a sprite sheet.
            /// </summary>
            public string ExportSpriteSheetJSONFileName { get; set; }

            /// <summary>
            /// File name for exporting a CSS file for sprite animation.
            /// </summary>
            public string ExportCSSAnimationFileName { get; set; }

            /// <summary>
            /// Whether to export animated GIF.
            /// </summary>
            public bool ExportAnimatedGIF { get; set; }

            public bool NothingToExport
                => !ExportSpriteSheet &&
                string.IsNullOrEmpty(ExportSpriteSheetJSONFileName) &&
                string.IsNullOrEmpty(ExportCSSAnimationFileName) &&
                !ExportAnimatedGIF;
        }

        public static void ProcessProgramDefinitionFile(string pdPath)
        {
            if (!File.Exists(pdPath))
            {
                ConsolePrint.Error("Definition file \"{0}\" does not exist.", pdPath);
                return;
            }

            ProgramDefinition pd = null;

            // Open file and read JSON file.
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                pd = js.Deserialize<ProgramDefinition>(File.ReadAllText(pdPath));
            }
            catch
            {
                ConsolePrint.Error("Failed to open definition file: \"{0}\"", pdPath);
                return;
            }

            if (pd.NothingToExport)
            {
                ConsolePrint.Print("Nothin to export.");
                return;
            }

            if (pd.PngDirectory == null)
            {
                ConsolePrint.Error("PngDirectory is not defined.");
                return;
            }

            if (!Directory.Exists(pd.PngDirectory))
            {
                ConsolePrint.Error("PngPath \"{0}\" not exist.", pd.PngDirectory);
                return;
            }

            if (string.IsNullOrEmpty(pd.ExportBaseDirectory))
            {
                ConsolePrint.Error("ExportBaseDirectory is not defined.");
                return;
            }

            // Get necessary data before we start.
            List<Actor> actors = GetActorsFromJSON(pd.Actors);
            string[] pngPaths = FindReferencedPngs(actors, pd.PngDirectory);
            SpritePool spritePool = GetSpritePool(pngPaths);

            if (pd.ExportSpriteSheet)
            {
                ConsolePrint.Print("Exporting sprite sheet...");
                ConsolePrint.Indent();
                ExportSpriteSheets(
                    actors,
                    spritePool,
                    MergePath(pd.ExportBaseDirectory, "SpriteSheet"),
                    pd.ExportNamePrefix);
                ConsolePrint.Unindent();
            }

            if (!string.IsNullOrEmpty(pd.ExportSpriteSheetJSONFileName))
            {
                ConsolePrint.Print("Exporting sprite sheet JSON...");
                ConsolePrint.Indent();
                Dictionary<string, SpriteSheetJSON> spriteSheetJSON = GetSpriteSheetJSON(actors, spritePool, pd.ExportNamePrefix);
                JavaScriptSerializer js = new JavaScriptSerializer();
                File.WriteAllText(
                    MergePath(pd.ExportBaseDirectory, pd.ExportSpriteSheetJSONFileName),
                    js.Serialize(spriteSheetJSON));
                ConsolePrint.Unindent();
            }

            if (!string.IsNullOrEmpty(pd.ExportCSSAnimationFileName))
            {
                ConsolePrint.Print("Exporting CSS animations...");
                ConsolePrint.Indent();
                ExportCSSAnimations(
                    actors,
                    spritePool,
                    MergePath(pd.ExportBaseDirectory, "SpriteSheet"), // So it can be referenced directly to all sprite sheets.
                    pd.ExportNamePrefix,
                    pd.ExportCSSAnimationFileName);
                ConsolePrint.Unindent();
            }

            if (pd.ExportAnimatedGIF)
            {
                ConsolePrint.Print("Exporting animated GIFs...");
                ConsolePrint.Indent();
                ExportAnimatedGIFs(
                    actors,
                    spritePool,
                    MergePath(pd.ExportBaseDirectory, "AnimatedGIF"),
                    pd.ExportNamePrefix);
                ConsolePrint.Unindent();
            }
        }
    }
}
