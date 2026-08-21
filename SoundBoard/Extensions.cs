#region Usings

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using NAudio.CoreAudioApi;
using NLog;

#endregion

namespace SoundBoard
{
    #region MenuItemExtensions class

    /// <summary>
    /// Extensions on the <see cref="MenuItem"/> class.
    /// </summary>
    internal static class MenuItemExtensions
    {
        #region Separator property

        /// <summary>
        /// Indicates whether a <see cref="Separator"/> should be added after the <see cref="MenuItem"/> it is set on.
        /// </summary>
        /// <remarks>
        /// This is an attached property (rather than, say, a static dictionary keyed on the menu item) so that the
        /// value lives on the menu item itself and is collected along with it.
        /// </remarks>
        public static readonly DependencyProperty SeparatorProperty = DependencyProperty.RegisterAttached(
            "Separator", typeof(bool), typeof(MenuItemExtensions), new PropertyMetadata(false));

        /// <summary>
        /// Get the Separator property
        /// </summary>
        public static bool GetSeparator(this MenuItem menuItem)
        {
            return (bool)menuItem.GetValue(SeparatorProperty);
        }

        /// <summary>
        /// Set the Separator property
        /// </summary>
        /// <param name="menuItem"></param>
        /// <param name="value"></param>
        public static void SetSeparator(this MenuItem menuItem, bool value)
        {
            menuItem.SetValue(SeparatorProperty, value);
        }

        #endregion
    }

    #endregion

    #region ContextMenuExtensions class

    /// <summary>
    /// Extensions on the <see cref="ContextMenu"/> class.
    /// </summary>
    internal static class ContextMenuExtensions
    {
        /// <summary>
        /// Add separators to the given <paramref name="contextMenu"/>.
        /// </summary>
        /// <param name="contextMenu"></param>
        public static void AddSeparators(this ContextMenu contextMenu)
        {
            if (contextMenu is null == false)
            {
                for (int i = contextMenu.Items.Count - 1; i >= 0; --i)
                {
                    // Remove any existing separators
                    if (contextMenu.Items[i] is Separator)
                    {
                        contextMenu.Items.RemoveAt(i);
                    }
                    // Now add any needed separators
                    else if (contextMenu.Items[i] is MenuItem menuItem
                             && menuItem.GetSeparator() // We need a separator after this item
                             && contextMenu.Items.Count > i + 1 // There is at least one more item in the list (so the separator can separate something!)
                             && contextMenu.Items[i + 1] is Separator == false) // There isn't already a separator after this item
                    {
                        contextMenu.Items.Insert(i + 1, new Separator());
                    }
                }
            }
        }
    }

    #endregion

    #region ColorExtensions class

    /// <summary>
    /// Extensions on the <see cref="System.Windows.Media.Color"/> class.
    /// </summary>
    internal static class ColorExtensions
    {
        /// <summary>
        /// Returns true if the R, G, and B values of the <paramref name="color"/> are full (ignores the A value).
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static bool IsWhite(this System.Windows.Media.Color color)
        {
            return color.R == byte.MaxValue && color.G == byte.MaxValue && color.B == byte.MaxValue;
        }

        /// <summary>
        /// Returns true if the R, G, and B values of the <paramref name="color"/> are empty (ignores the A value).
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static bool IsBlack(this System.Windows.Media.Color color)
        {
            return color.R == 0 && color.G == 0 && color.B == 0;
        }
    }

    #endregion

    #region TabItemExtensions class

    /// <summary>
    /// Extensions on the <see cref="TabItem"/> class.
    /// </summary>
    internal static class TabItemExtensions
    {
        /// <summary>
        /// Returns <see langword="true"/> if the given <paramref name="tabItem"/> is the <see cref="Selector.SelectedItem"/> of its <see cref="FrameworkElement.Parent"/> <see cref="TabControl"/>.
        /// </summary>
        /// <param name="tabItem"></param>
        /// <returns></returns>
        public static bool IsSelectedItem(this TabItem tabItem)
        {
            return tabItem == (tabItem.Parent as TabControl)?.SelectedItem;
        }

        #region Rows property

        /// <summary>
        /// The number of button rows on the <see cref="TabItem"/> it is set on.
        /// </summary>
        /// <remarks>
        /// This is an attached property (rather than, say, a static dictionary keyed on the tab item) so that the
        /// value lives on the tab item itself and is collected along with it. There is deliberately no default in the
        /// property metadata; when no value has been set, <see cref="GetRows"/> falls back to the current global
        /// setting, which can change at runtime.
        /// </remarks>
        public static readonly DependencyProperty RowsProperty = DependencyProperty.RegisterAttached(
            "Rows", typeof(int), typeof(TabItemExtensions), new PropertyMetadata());

        /// <summary>
        /// Get the Rows property
        /// </summary>
        /// <param name="tabItem"></param>
        /// <returns></returns>
        public static int GetRows(this TabItem tabItem)
        {
            return tabItem.ReadLocalValue(RowsProperty) is int rows ? rows : GlobalSettings.NewPageDefaultRows;
        }

        /// <summary>
        /// Set the Rows property
        /// </summary>
        /// <param name="tabItem"></param>
        /// <param name="value"></param>
        public static void SetRows(this TabItem tabItem, int value)
        {
            tabItem.SetValue(RowsProperty, value);
        }

        #endregion

        #region TabContextMenu property

        /// <summary>
        /// The <see cref="ContextMenu"/> that has been built for the <see cref="TabItem"/> it is set on.
        /// </summary>
        /// <remarks>
        /// Tab context menus are shown manually (rather than assigned to <see cref="FrameworkElement.ContextMenu"/>)
        /// so that bubbled right-clicks from child controls can be filtered out, so we need somewhere to record whether
        /// a menu has already been built for a given tab. Storing it as an attached property means the menu is released
        /// as soon as the tab is.
        /// </remarks>
        public static readonly DependencyProperty TabContextMenuProperty = DependencyProperty.RegisterAttached(
            "TabContextMenu", typeof(ContextMenu), typeof(TabItemExtensions), new PropertyMetadata(null));

        /// <summary>
        /// Get the TabContextMenu property
        /// </summary>
        /// <param name="tabItem"></param>
        /// <returns></returns>
        public static ContextMenu GetTabContextMenu(this TabItem tabItem)
        {
            return (ContextMenu)tabItem.GetValue(TabContextMenuProperty);
        }

        /// <summary>
        /// Set the TabContextMenu property
        /// </summary>
        /// <param name="tabItem"></param>
        /// <param name="value"></param>
        public static void SetTabContextMenu(this TabItem tabItem, ContextMenu value)
        {
            tabItem.SetValue(TabContextMenuProperty, value);
        }

        #endregion

        #region Columns property

        /// <summary>
        /// The number of button columns on the <see cref="TabItem"/> it is set on. See <see cref="RowsProperty"/>.
        /// </summary>
        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
            "Columns", typeof(int), typeof(TabItemExtensions), new PropertyMetadata());

        /// <summary>
        /// Get the Columns property
        /// </summary>
        /// <param name="tabItem"></param>
        /// <returns></returns>
        public static int GetColumns(this TabItem tabItem)
        {
            return tabItem.ReadLocalValue(ColumnsProperty) is int columns ? columns : GlobalSettings.NewPageDefaultColumns;
        }

        /// <summary>
        /// Set the Columns property
        /// </summary>
        /// <param name="tabItem"></param>
        /// <param name="value"></param>
        public static void SetColumns(this TabItem tabItem, int value)
        {
            tabItem.SetValue(ColumnsProperty, value);
        }

        #endregion
    }

    #endregion

    #region MMDevice extensions

    /// <summary>
    /// Extensions on <see cref="MMDevice"/>.
    /// </summary>
    public static class MMDeviceExtensions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Converts the <see cref="MMDevice.ID"/> into a <see cref="Guid"/>,
        /// or <see langword="null"/> if the ID cannot be parsed.
        /// </summary>
        /// <remarks>
        /// This deliberately does not fall back to <see cref="Guid.Empty"/> on failure, because
        /// <see cref="Guid.Empty"/> is the sentinel meaning "the default device" everywhere else in the app.
        /// Returning it here would make an unparseable device indistinguishable from the default one.
        /// </remarks>
        /// <param name="mmDevice"></param>
        /// <returns></returns>
        public static Guid? GetGuid(this MMDevice mmDevice)
        {
            return TryGetGuid(mmDevice, out Guid result) ? result : (Guid?)null;
        }

        /// <summary>
        /// Attempts to convert the <see cref="MMDevice.ID"/> into a <see cref="Guid"/>.
        /// Returns <see langword="true"/> on success, in which case <paramref name="guid"/> holds the parsed value.
        /// </summary>
        /// <param name="mmDevice"></param>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static bool TryGetGuid(this MMDevice mmDevice, out Guid guid)
        {
            try
            {
                return Guid.TryParse(mmDevice.ID.Substring(mmDevice.ID.IndexOf('{', 1) + 1, Guid.Empty.ToString().Length), out guid);
            }
            catch (Exception ex)
            {
                // The ID wasn't even shaped like we expect (too short, no brace, null), so there's nothing to parse.
                Logger.Debug(ex, "Device ID '{0}' is not in the expected format", mmDevice?.ID);
                guid = Guid.Empty;
                return false;
            }
        }
    }

    #endregion
}
