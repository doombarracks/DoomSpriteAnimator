﻿using System;
using System.Windows.Media.Imaging;

namespace AnimatedGif
{
    public static class AnimatedGif
    {
        /// <summary>
        ///     Create a new Animated GIF
        /// </summary>
        /// <param name="filePath">The Path where the Animated GIF gets saved</param>
        /// <param name="delay">Delay between frames</param>
        /// <param name="repeat">GIF Repeat count (null meaning forever)</param>
        /// <returns></returns>
        public static AnimatedGifCreator Create(string filePath, int delay, byte? repeat = 0)
        {
            return new AnimatedGifCreator(filePath, delay, repeat);
        }

        /// <summary>
        ///     Load an Animated GIF from File (not yet custom implemented)
        /// </summary>
        /// <param name="filePath">Path to GIF File</param>
        /// <returns>GifBitmapDecoder using the GIF File</returns>
        public static GifBitmapDecoder Load(string filePath)
        {
            return new GifBitmapDecoder(new Uri(filePath), BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.Default);
        }
    }
}