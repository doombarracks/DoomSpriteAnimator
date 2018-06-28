using System;
using System.Drawing;
using System.IO;

namespace AnimatedGif {
    public class AnimatedGifCreator : IDisposable {
        private FileStream _stream;

        public AnimatedGifCreator(string filePath, int delay, byte? repeat = null) {
            FilePath = filePath;
            Delay = delay;
            Repeat = repeat;
        }

        public string FilePath { get; }
        public int Delay { get; }
        public byte? Repeat { get; }
        public int FrameCount { get; private set; }

        public void Dispose() {
            Finish();
        }

        /// <summary>
        ///     Add a new frame to the GIF
        /// </summary>
        /// <param name="image">The image to add to the GIF stack</param>
        /// <param name="delay">The delay in milliseconds this GIF will be delayed (-1: Indicating class property delay)</param>
        /// <param name="quality">The GIFs quality</param>
        public void AddFrame(Image image, int delay = -1, GifQuality quality = GifQuality.Default) {
            var gif = new GifClass();
            gif.LoadGifPicture(image, quality);

            if (_stream == null) {
                _stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _stream.Write(CreateHeaderBlock());
                _stream.Write(gif.ScreenDescriptor.ToArray());
                _stream.Write(CreateApplicationExtensionBlock(Repeat));
            }

            _stream.Write(CreateGraphicsControlExtensionBlock(delay > -1 ? delay : Delay));
            _stream.Write(gif.ImageDescriptor.ToArray());
            _stream.Write(gif.ColorTable.ToArray());
            _stream.Write(gif.ImageData.ToArray());

            FrameCount++;
        }

        /// <summary>
        ///     Add a new frame to the GIF
        /// </summary>
        /// <param name="path">The image's path which will be added to the GIF stack</param>
        /// <param name="delay">The delay in milliseconds this GIF will be delayed (-1: Indicating class property delay)</param>
        /// <param name="quality">The GIFs quality</param>
        public void AddFrame(string path, int delay = -1, GifQuality quality = GifQuality.Default) {
            using (var img = Helper.LoadImage(path)) {
                AddFrame(img, delay, quality);
            }
        }

        /// <summary>
        ///     Finish creating the GIF and start flushing
        /// </summary>
        private void Finish() {
            if (_stream == null)
                return;
            _stream.WriteByte(0x3B); // Image terminator
            _stream.Dispose();
        }

        /// <summary>
        ///     Create the GIFs header block (GIF89a)
        /// </summary>
        private static byte[] CreateHeaderBlock() {
            return new[] {(byte) 'G', (byte) 'I', (byte) 'F', (byte) '8', (byte) '9', (byte) 'a'};
        }

        private static byte[] CreateApplicationExtensionBlock(byte? repeat)
            => new byte[]
            {
                0x21, // Extension introducer
                0xFF, // Application extension
                0x0B, // Size of block
                (byte)'N', // NETSCAPE2.0
                (byte)'E',
                (byte)'T',
                (byte)'S',
                (byte)'C',
                (byte)'A',
                (byte)'P',
                (byte)'E',
                (byte)'2',
                (byte)'.',
                (byte)'0',
                0x03, // Size of block
                0x01, // Loop indicator
                (byte)(repeat.HasValue ? repeat.Value : 0), // Number of repetitions
                (byte)(repeat.HasValue ? 1 : 0), // 0 for endless loop
                0x00, // Block terminator
            };

        private static byte[] CreateGraphicsControlExtensionBlock(int delay)
            => new byte[]
            {
                0x21, // Extension introducer
                0xF9, // Graphic control extension
                0x04, // Size of block
                0x09, // Flags: reserved, disposal method, user input, transparent color
                (byte)(delay / 10), // Delay time low byte
                (byte)((delay / 10) >> 8), // Delay time high byte
                0xFF, // Transparent color index
                0x00, // Block terminator
            };
    }
}