using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace SoundBoard.Model
{
    /// <summary>
    /// Reads and writes the <c>soundboard.config</c> XML format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The format is hand-rolled and must stay byte-compatible with what every previous release wrote, because users' existing
    /// files (and their <c>.bak</c> copies) have to keep loading — and older builds should keep loading files written by this one.
    /// The layout is:
    /// </para>
    /// <code>
    /// &lt;?xml version="1.0" encoding="utf-8"?&gt;
    /// &lt;tabs schemaVersion="2"&gt;
    ///     &lt;GlobalSettings OutputDeviceGuid="guid,guid" InputDeviceGuid="" PassthroughOutputDeviceGuid=""
    ///                     AudioPassthroughLatency="10" NewPageDefaultRows="5" NewPageDefaultColumns="2" /&gt;
    ///     &lt;tab focused="True" rows="5" columns="2"&gt;
    ///         &lt;name&gt;Page name&lt;/name&gt;
    ///         &lt;button0 name="" path="" color="#FFAABBCC" volumeOffset="0" loop="False" stopAllSounds="False"
    ///                  nextSound="" id="guid" localHotkey="" globalHotkey="" row="0" column="0" /&gt;
    ///         &lt;button1 ... /&gt;
    ///     &lt;/tab&gt;
    /// &lt;/tabs&gt;
    /// </code>
    /// <para>
    /// Quirks that are deliberately preserved: button elements are named <c>button0</c>, <c>button1</c>, … (the index is in the
    /// element name); <c>row</c>/<c>column</c> attributes take precedence over that index; device lists are comma-joined GUIDs in a
    /// single attribute; an empty output-device set is written as <see cref="Guid.Empty"/>; every attribute is always written.
    /// <c>schemaVersion</c> is the only addition — older readers ignore unknown attributes.
    /// </para>
    /// </remarks>
    public static class ConfigSerializer
    {
        private const string RootElement = "tabs";
        private const string SchemaVersionAttribute = "schemaVersion";
        private const string GlobalSettingsElement = "GlobalSettings";
        private const string TabElement = "tab";
        private const string NameElement = "name";
        private const string ButtonElementPrefix = "button";

        // Attribute names for the device lists are historical: "OutputDeviceGuid" used to be a single scalar property.
        private const string OutputDeviceGuidAttribute = "OutputDeviceGuid";
        private const string InputDeviceGuidAttribute = "InputDeviceGuid";
        private const string PassthroughOutputDeviceGuidAttribute = "PassthroughOutputDeviceGuid";
        private const string AudioPassthroughLatencyAttribute = "AudioPassthroughLatency";
        private const string NewPageDefaultRowsAttribute = "NewPageDefaultRows";
        private const string NewPageDefaultColumnsAttribute = "NewPageDefaultColumns";

        private static readonly Regex ButtonElementRegex = new Regex($"^{ButtonElementPrefix}(\\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        #region Read

        /// <summary>
        /// Reads a config from a file.
        /// </summary>
        /// <param name="path">File to read.</param>
        /// <param name="warn">Optional sink for non-fatal problems (e.g. a sound outside its page's grid was dropped).</param>
        /// <exception cref="XmlException">The file is not well-formed XML.</exception>
        public static SoundBoardConfig Read(string path, Action<string> warn = null)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(stream, warn);
            }
        }

        /// <summary>
        /// Reads a config from a stream.
        /// </summary>
        /// <param name="stream">Stream to read. Left open.</param>
        /// <param name="warn">Optional sink for non-fatal problems (e.g. a sound outside its page's grid was dropped).</param>
        /// <exception cref="XmlException">The stream is not well-formed XML.</exception>
        public static SoundBoardConfig Read(Stream stream, Action<string> warn = null)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            warn = warn ?? (_ => { });

            var xmlDocument = new XmlDocument();
            xmlDocument.Load(stream);

            var config = new SoundBoardConfig();

            XmlElement root = xmlDocument.DocumentElement;
            if (root is null)
            {
                return config;
            }

            // Schema version
            config.SchemaVersion = SoundBoardConfig.LegacySchemaVersion;
            if (root.Attributes?[SchemaVersionAttribute]?.Value is string schemaVersionString && int.TryParse(schemaVersionString, out int schemaVersion))
            {
                config.SchemaVersion = schemaVersion;
            }

            if (config.SchemaVersion > SoundBoardConfig.CurrentSchemaVersion)
            {
                warn($"Config schema version {config.SchemaVersion} is newer than the supported version {SoundBoardConfig.CurrentSchemaVersion}; loading best-effort.");
            }

            // Global settings. These must be read before the tabs, because a tab without rows/columns falls back to the new-page defaults.
            if (xmlDocument.SelectSingleNode($"/{RootElement}/{GlobalSettingsElement}") is XmlNode globalSettingsNode)
            {
                ReadGlobalSettings(globalSettingsNode, config.Settings);
            }

            // Tabs
            XmlNodeList tabNodes = root.SelectNodes($"/{RootElement}/{TabElement}");
            if (tabNodes != null)
            {
                foreach (XmlNode tabNode in tabNodes)
                {
                    config.Pages.Add(ReadPage(tabNode, config.Settings, warn));
                }
            }

            return config;
        }

        private static void ReadGlobalSettings(XmlNode node, BoardSettings settings)
        {
            ReadGuidList(node, OutputDeviceGuidAttribute, settings.OutputDevices);
            ReadGuidList(node, InputDeviceGuidAttribute, settings.InputDevices);
            ReadGuidList(node, PassthroughOutputDeviceGuidAttribute, settings.PassthroughOutputDevices);

            if (TryReadInt(node, AudioPassthroughLatencyAttribute, out int latency))
            {
                settings.AudioPassthroughLatency = latency;
            }

            if (TryReadInt(node, NewPageDefaultRowsAttribute, out int rows))
            {
                settings.NewPageDefaultRows = rows;
            }

            if (TryReadInt(node, NewPageDefaultColumnsAttribute, out int columns))
            {
                settings.NewPageDefaultColumns = columns;
            }
        }

        private static void ReadGuidList(XmlNode node, string attributeName, ISet<Guid> target)
        {
            if (node.Attributes?[attributeName]?.Value is string value)
            {
                foreach (string part in value.Split(','))
                {
                    if (Guid.TryParse(part, out Guid guid))
                    {
                        target.Add(guid);
                    }
                }
            }
        }

        private static Page ReadPage(XmlNode tabNode, BoardSettings settings, Action<string> warn)
        {
            string name = tabNode[NameElement]?.InnerText;

            int rows = TryReadInt(tabNode, "rows", out int parsedRows) ? parsedRows : settings.NewPageDefaultRows;
            int columns = TryReadInt(tabNode, "columns", out int parsedColumns) ? parsedColumns : settings.NewPageDefaultColumns;

            var page = new Page(name, rows, columns);

            if (tabNode.Attributes?["focused"]?.Value is string focusedString && bool.TryParse(focusedString, out bool focused))
            {
                page.IsFocused = focused;
            }

            // Buttons are <button0>, <button1>, ... Collect them in index order; the row/column attributes (if present)
            // say where each one lives, otherwise the index does. The first button to claim a cell wins.
            var buttonNodes = new List<(int Index, XmlNode Node)>();
            foreach (XmlNode child in tabNode.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element && ButtonElementRegex.Match(child.Name) is Match match && match.Success)
                {
                    buttonNodes.Add((int.Parse(match.Groups[1].Value), child));
                }
            }

            var claimed = new HashSet<(int, int)>();
            foreach ((int index, XmlNode buttonNode) in buttonNodes.OrderBy(b => b.Index))
            {
                int row = TryReadInt(buttonNode, "row", out int parsedRow) ? parsedRow : (columns > 0 ? index / columns : 0);
                int column = TryReadInt(buttonNode, "column", out int parsedColumn) ? parsedColumn : (columns > 0 ? index % columns : 0);

                if (!page.IsInRange(row, column))
                {
                    warn($"Dropping {buttonNode.Name} on page \"{name}\": position ({row}, {column}) is outside the {rows}x{columns} grid.");
                    continue;
                }

                if (!claimed.Add((row, column)))
                {
                    warn($"Dropping {buttonNode.Name} on page \"{name}\": position ({row}, {column}) is already taken.");
                    continue;
                }

                Sound sound = ReadSound(buttonNode);
                sound.Row = row;
                sound.Column = column;
                page.Set(sound);
            }

            return page;
        }

        private static Sound ReadSound(XmlNode node)
        {
            var sound = new Sound();

            string path = node.Attributes?["path"]?.Value;

            // An empty cell carries no other meaningful data. Its stored id is intentionally not restored: the UI has always
            // given empty buttons a fresh id on load, so nothing can reference one.
            if (string.IsNullOrEmpty(path))
            {
                return sound;
            }

            sound.Path = path;
            sound.Name = node.Attributes?["name"]?.Value ?? string.Empty;

            if (node.Attributes?["color"]?.Value is string colorString && !string.IsNullOrEmpty(colorString))
            {
                sound.Color = SoundColor.Parse(colorString);
            }

            if (TryReadInt(node, "volumeOffset", out int volumeOffset))
            {
                sound.VolumeOffset = volumeOffset;
            }

            if (node.Attributes?["loop"]?.Value is string loopString && bool.TryParse(loopString, out bool loop))
            {
                sound.Loop = loop;
            }

            if (node.Attributes?["stopAllSounds"]?.Value is string stopAllSoundsString && bool.TryParse(stopAllSoundsString, out bool stopAllSounds))
            {
                sound.StopAllSounds = stopAllSounds;
            }

            if (node.Attributes?["nextSound"]?.Value is string nextSound && !string.IsNullOrEmpty(nextSound))
            {
                sound.NextSoundId = nextSound;
            }

            if (node.Attributes?["id"]?.Value is string id && !string.IsNullOrEmpty(id))
            {
                sound.Id = id;
            }

            if (node.Attributes?["localHotkey"]?.Value is string localHotkey && !string.IsNullOrEmpty(localHotkey))
            {
                sound.LocalHotkey = Hotkey.FromString(localHotkey);
            }

            if (node.Attributes?["globalHotkey"]?.Value is string globalHotkey && !string.IsNullOrEmpty(globalHotkey))
            {
                sound.GlobalHotkey = Hotkey.FromString(globalHotkey);
            }

            return sound;
        }

        private static bool TryReadInt(XmlNode node, string attributeName, out int value)
        {
            value = 0;
            return node.Attributes?[attributeName]?.Value is string s && !string.IsNullOrEmpty(s) && int.TryParse(s, out value);
        }

        #endregion

        #region Write

        /// <summary>
        /// Writes a config to a file, replacing any existing content.
        /// </summary>
        public static void Write(SoundBoardConfig config, string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create))
            {
                Write(config, stream);
            }
        }

        /// <summary>
        /// Writes a config to a stream as UTF-8 (no BOM). The stream is left open.
        /// </summary>
        public static void Write(SoundBoardConfig config, Stream stream)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));
            if (stream is null) throw new ArgumentNullException(nameof(stream));

            // StreamWriter's default encoding is UTF-8 without a BOM, which is what every previous release wrote.
            using (StreamWriter streamWriter = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 4096, leaveOpen: true))
            using (XmlTextWriter writer = new XmlTextWriter(streamWriter))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;

                writer.WriteStartDocument();
                writer.WriteStartElement(RootElement);
                writer.WriteAttributeString(SchemaVersionAttribute, SoundBoardConfig.CurrentSchemaVersion.ToString());

                BoardSettings settings = config.Settings ?? new BoardSettings();
                writer.WriteStartElement(GlobalSettingsElement);
                writer.WriteAttributeString(OutputDeviceGuidAttribute, string.Join(",", settings.GetOutputDeviceGuidsOrDefault()));
                writer.WriteAttributeString(InputDeviceGuidAttribute, string.Join(",", settings.InputDevices));
                writer.WriteAttributeString(PassthroughOutputDeviceGuidAttribute, string.Join(",", settings.PassthroughOutputDevices));
                writer.WriteAttributeString(AudioPassthroughLatencyAttribute, settings.AudioPassthroughLatency.ToString());
                writer.WriteAttributeString(NewPageDefaultRowsAttribute, settings.NewPageDefaultRows.ToString());
                writer.WriteAttributeString(NewPageDefaultColumnsAttribute, settings.NewPageDefaultColumns.ToString());
                writer.WriteEndElement();

                foreach (Page page in config.Pages)
                {
                    writer.WriteStartElement(TabElement);
                    writer.WriteAttributeString("focused", page.IsFocused.ToString());
                    writer.WriteAttributeString("rows", page.Rows.ToString());
                    writer.WriteAttributeString("columns", page.Columns.ToString());

                    writer.WriteElementString(NameElement, page.Name);

                    int index = 0;
                    foreach (Sound sound in page.Sounds)
                    {
                        writer.WriteStartElement(ButtonElementPrefix + index++);
                        writer.WriteAttributeString("name", sound.Name);
                        writer.WriteAttributeString("path", sound.Path);
                        writer.WriteAttributeString("color", sound.Color?.ToHtml() ?? string.Empty);
                        writer.WriteAttributeString("volumeOffset", sound.VolumeOffset.ToString());
                        writer.WriteAttributeString("loop", sound.Loop.ToString());
                        writer.WriteAttributeString("stopAllSounds", sound.StopAllSounds.ToString());
                        writer.WriteAttributeString("nextSound", sound.NextSoundId);
                        writer.WriteAttributeString("id", sound.Id);
                        writer.WriteAttributeString("localHotkey", sound.LocalHotkey?.ToString() ?? string.Empty);
                        writer.WriteAttributeString("globalHotkey", sound.GlobalHotkey?.ToString() ?? string.Empty);
                        writer.WriteAttributeString("row", sound.Row.ToString());
                        writer.WriteAttributeString("column", sound.Column.ToString());
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
            }
        }

        #endregion
    }
}
