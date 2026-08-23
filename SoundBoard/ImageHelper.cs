#region Usings

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Image = System.Windows.Controls.Image;

#endregion

namespace SoundBoard
{
    /// <summary>
    /// Static helper class to retrieve images
    /// </summary>
    internal static class ImageHelper
    {
        #region Public static methods

        /// <summary>
        /// Returns an <see cref="Image"/> for the given <paramref name="path"/>.
        /// </summary>
        public static Image GetImage(string path, int? width = null, int? height = null, bool light = false)
        {
            path = $@"{path}{(light ? _lightPathPostfix : string.Empty)}{_extension}";

            if (width is null == false && height is null == false)
            {
                return new Image {Source = new BitmapImage(new Uri(path)), Width = (int) width, Height = (int) height};
            }
            else if (width is null == false)
            {
                return new Image {Source = new BitmapImage(new Uri(path)), Width = (int) width};
            }
            else if (height is null == false)
            {
                return new Image {Source = new BitmapImage(new Uri(path)), Height = (int) height};
            }
            else
            {
                return new Image {Source = new BitmapImage(new Uri(path))};
            }
        }

        /// <summary>
        /// Returns a speaker icon, crossed out when <paramref name="muted"/>.
        /// </summary>
        public static FrameworkElement GetMuteIcon(int size, bool light, bool muted) => muted
            ? BuildIcon(size, light, new IconShape(Speaker), new IconShape(MuteCross, IconStrokeThickness))
            : BuildIcon(size, light, new IconShape(Speaker), new IconShape(InnerSoundWave, IconStrokeThickness), new IconShape(OuterSoundWave, IconStrokeThickness));

        /// <summary>
        /// Returns a headphones icon, the usual sign for "listen to this one on its own".
        /// </summary>
        public static FrameworkElement GetSoloIcon(int size, bool light) =>
            BuildIcon(size, light, new IconShape(HeadphoneBand, IconStrokeThickness + 0.4), new IconShape(HeadphoneCups));

        #endregion

        #region Private static methods

        /// <summary>
        /// Draws <paramref name="shapes"/> on the icon's own 24-by-24 canvas, scaled to <paramref name="size"/> and drawn in
        /// white on a dark button or black on a light one, so that these icons follow the same rules as the bitmap ones.
        /// </summary>
        private static FrameworkElement BuildIcon(int size, bool light, params IconShape[] shapes)
        {
            Brush brush = new SolidColorBrush(light ? Colors.White : Colors.Black);
            Canvas canvas = new Canvas {Width = IconCanvasSize, Height = IconCanvasSize};

            foreach (IconShape shape in shapes)
            {
                Path path = new Path {Data = Geometry.Parse(shape.Data)};

                if (shape.StrokeThickness > 0)
                {
                    path.Stroke = brush;
                    path.StrokeThickness = shape.StrokeThickness;
                    path.StrokeStartLineCap = PenLineCap.Round;
                    path.StrokeEndLineCap = PenLineCap.Round;
                }
                else
                {
                    path.Fill = brush;
                }

                canvas.Children.Add(path);
            }

            return new Viewbox {Width = size, Height = size, Child = canvas};
        }

        #endregion

        #region IconShape struct

        /// <summary>
        /// One path of a vector icon, drawn filled or, when given a thickness, stroked.
        /// </summary>
        private readonly struct IconShape
        {
            public IconShape(string data, double strokeThickness = 0)
            {
                Data = data;
                StrokeThickness = strokeThickness;
            }

            public string Data { get; }

            public double StrokeThickness { get; }
        }

        #endregion

        #region Public static consts

        public static string PlayButtonPath = @"pack://application:,,,/Images/play-arrow";

        public static string PauseButtonPath = @"pack://application:,,,/Images/pause-button";

        public static string StopButtonPath = @"pack://application:,,,/Images/stop-button";

        public static string CloseButtonPath = @"pack://application:,,,/Images/close";

        public static string AddButtonPath = @"pack://application:,,,/Images/add";

        public static string AddFocusButtonPath = @"pack://application:,,,/Images/add_focus";

        public static string MenuButtonPath = @"pack://application:,,,/Images/menu";

        public static string CheckIconPath = @"pack://application:,,,/Images/check";

        public static string LoopIconPath = @"pack://application:,,,/Images/loop";

        public static string WarningIconPath = @"pack://application:,,,/Images/warning";

        public static string KeyboardIconPath = @"pack://application:,,,/Images/keyboard";

        public static string XIconPath = @"pack://application:,,,/Images/x";

        public static string RightIconPath = @"pack://application:,,,/Images/right";

        #endregion

        #region Private static consts

        private static string _lightPathPostfix = @"_light";

        private static string _extension = @".png";

        // ----- Vector icons ----- //
        //
        // Mute and solo have no bitmap in Images, so they are drawn instead. All of the coordinates below are on the same
        // 24-by-24 grid, which BuildIcon scales to whatever size the caller asks for.

        private const double IconCanvasSize = 24;

        private const double IconStrokeThickness = 1.8;

        /// <summary>A loudspeaker facing right: the back of the box, then the cone.</summary>
        private const string Speaker = "M4,9 H8 L13,4.5 V19.5 L8,15 H4 Z";

        /// <summary>The nearer of the two arcs coming off the speaker.</summary>
        private const string InnerSoundWave = "M15.5,9 A4,4 0 0,1 15.5,15";

        /// <summary>The further of the two arcs coming off the speaker.</summary>
        private const string OuterSoundWave = "M18,6.5 A7,7 0 0,1 18,17.5";

        /// <summary>The cross that replaces the arcs when the sound is muted.</summary>
        private const string MuteCross = "M16,9.5 L21,14.5 M21,9.5 L16,14.5";

        /// <summary>The headband, drawn as a semicircle with a short straight drop at each ear.</summary>
        private const string HeadphoneBand = "M4.6,14 V12.5 A7.4,7.4 0 0,1 19.4,12.5 V14";

        /// <summary>
        /// Both earcups. Deliberately large next to the band: at the size these are drawn, a headphone shape with dainty
        /// cups reads as a plain arch and nothing else.
        /// </summary>
        private const string HeadphoneCups = "M1.5,12 H7.5 V21.5 H1.5 Z M16.5,12 H22.5 V21.5 H16.5 Z";

        #endregion
    }
}
