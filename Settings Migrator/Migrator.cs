/**
 * Migrator.cs
 * 
 * Creates a modified version of a settings file for migration
 * between two versions of Stoffi Music Player.
 * 
 * * * * * * * * *
 * 
 * Copyright 2012 Simplare
 * 
 * This code is part of the Stoffi Music Player Project.
 * Visit our website at: stoffiplayer.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version
 * 3 of the License, or (at your option) any later version.
 * 
 * See stoffiplayer.com/license for more information.
 **/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Stoffi
{
    /// <summary>
    /// Migrates settings between two versions of Stoffi.
    /// </summary>
    public class SettingsMigrator : IMigrator
    {
        #region Members

        private Utility utility;

        #endregion

        #region Methods

        #region Public

        /// <summary>
        /// Migrates a settings file.
        /// </summary>
        /// <param name="fromFile">The original file</param>
        /// <param name="toFile">The output file</param>
        public void Migrate(String fromFile, String toFile)
        {
			utility = new Utility();
			utility.LogFile = "migrator.log";
			utility.Mode = LogMode.Debug;

			NewSettings settings = new NewSettings();

			utility.LogMessageToFile(LogMode.Information, "Reading configuration");
			ReadConfig(settings, fromFile);

			utility.LogMessageToFile(LogMode.Information, "Modifying configuration");

			foreach (SourceData source in settings.Sources)
				FixSource(source);

			utility.LogMessageToFile(LogMode.Information, "Writing configuration");
			WriteConfig(settings, toFile);
		}

        #endregion

        #region Private

		private void FixSource(SourceData item)
		{
			item.Icon = item.Icon.Replace(
				"pack://application:,,,/GUI/Images/Icons/",
				"pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/"
			);
		}

		private KeyboardShortcut GetKeyboardShortcut(KeyboardShortcutProfile profile, String keysAsText)
		{
			foreach (KeyboardShortcut s in profile.Shortcuts)
				if (s.Keys == keysAsText)
					return s;
			return null;
		}

		private KeyboardShortcut GetKeyboardShortcut(KeyboardShortcutProfile profile, String category, String name)
		{
			foreach (KeyboardShortcut s in profile.Shortcuts)
				if (s.Name == name && s.Category == category)
					return s;
			return null;
		}

		private void SetKeyboardShortcut(KeyboardShortcut sc, String keysAsText, String isGlobal = "false")
		{
			sc.Keys = keysAsText;
			sc.IsGlobal = isGlobal;
		}

		private void SetKeyboardShortcut(KeyboardShortcutProfile profile, String category, String name, String keysAsText, String isGlobal = "false")
		{
			KeyboardShortcut sc = GetKeyboardShortcut(profile, category, name);
			if (sc == null)
			{
				sc = new KeyboardShortcut();
				sc.Category = category;
				sc.Name = name;
				profile.Shortcuts.Add(sc);
			}
			SetKeyboardShortcut(sc, keysAsText, isGlobal);
		}

		private void InitShortcutProfile(KeyboardShortcutProfile profile, String name, String isprotected)
		{
			profile.Name = name;
			profile.IsProtected = isprotected;
			profile.Shortcuts = new List<KeyboardShortcut>();

			// set the default shortcuts
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Application", Name = "Add track", IsGlobal = "false", Keys = "Ctrl+T" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Application", Name = "Add folder", IsGlobal = "false", Keys = "Ctrl+F" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Application", Name = "Open playlist", IsGlobal = "false", Keys = "Ctrl+O" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Application", Name = "Minimize", IsGlobal = "false", Keys = "Ctrl+M" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Application", Name = "Restore", IsGlobal = "true", Keys = "Ctrl+Shift+R" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Application", Name = "Help", IsGlobal = "false", Keys = "F1" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Application", Name = "Close", IsGlobal = "false", Keys = "Ctrl+W" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Library", IsGlobal = "false", Keys = "Alt+L" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Queue", IsGlobal = "false", Keys = "Alt+Q" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "History", IsGlobal = "false", Keys = "Alt+H" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Playlists", IsGlobal = "false", Keys = "Alt+P" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Now playing", IsGlobal = "false", Keys = "Alt+W" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Tracklist", IsGlobal = "false", Keys = "Alt+T" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Search", IsGlobal = "false", Keys = "Alt+F" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "General preferences", IsGlobal = "false", Keys = "Alt+G" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Services", IsGlobal = "false", Keys = "Alt+V" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Library sources", IsGlobal = "false", Keys = "Alt+S" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Keyboard shortcuts", IsGlobal = "false", Keys = "Alt+K" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "About", IsGlobal = "false", Keys = "Alt+A" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Toggle details pane", IsGlobal = "false", Keys = "Alt+D" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Toggle menu bar", IsGlobal = "false", Keys = "Alt+M" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MainWindow", Name = "Create playlist", IsGlobal = "false", Keys = "Alt+N" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Play or pause", IsGlobal = "false", Keys = "Alt+5 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Next", IsGlobal = "false", Keys = "Alt+6 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Previous", IsGlobal = "false", Keys = "Alt+4 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Toggle shuffle", IsGlobal = "false", Keys = "Alt+7 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Toggle repeat", IsGlobal = "false", Keys = "Alt+9 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Increase volume", IsGlobal = "false", Keys = "Alt+8 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Decrease volume", IsGlobal = "false", Keys = "Alt+2 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Seek forward", IsGlobal = "false", Keys = "Alt+3 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Seek backward", IsGlobal = "false", Keys = "Alt+1 (numpad)" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Add bookmark", IsGlobal = "false", Keys = "Alt+B" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to previous bookmark", IsGlobal = "false", Keys = "Alt+Left" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to next bookmark", IsGlobal = "false", Keys = "Alt+Right" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to first bookmark", IsGlobal = "false", Keys = "Alt+Home" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to last bookmark", IsGlobal = "false", Keys = "Alt+End" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 1", IsGlobal = "false", Keys = "Alt+1" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 2", IsGlobal = "false", Keys = "Alt+2" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 3", IsGlobal = "false", Keys = "Alt+3" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 4", IsGlobal = "false", Keys = "Alt+4" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 5", IsGlobal = "false", Keys = "Alt+5" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 6", IsGlobal = "false", Keys = "Alt+6" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 7", IsGlobal = "false", Keys = "Alt+7" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 8", IsGlobal = "false", Keys = "Alt+8" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 9", IsGlobal = "false", Keys = "Alt+9" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to bookmark 10", IsGlobal = "false", Keys = "Alt+0" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to current track", IsGlobal = "false", Keys = "Alt+C" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "MediaCommands", Name = "Jump to selected track", IsGlobal = "false", Keys = "Alt+X" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Track", Name = "Queue and dequeue", IsGlobal = "false", Keys = "Shift+Q" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Track", Name = "Remove track", IsGlobal = "false", Keys = "Delete" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Track", Name = "Play track", IsGlobal = "false", Keys = "Enter" });
			profile.Shortcuts.Add(new KeyboardShortcut { Category = "Track", Name = "View information", IsGlobal = "false", Keys = "Shift+I" });
		}

		/// <summary>
		/// Creates a ViewDetailsConfig with default values
		/// </summary>
		/// <returns>The newly created config</returns>
		public static ViewDetailsConfig CreateListConfig()
		{
			Stoffi.ViewDetailsConfig config = new Stoffi.ViewDetailsConfig();
			config.HasNumber = true;
			config.IsNumberVisible = false;
			config.Filter = "";
			config.IsClickSortable = true;
			config.IsDragSortable = true;
			config.LockSortOnNumber = false;
			config.SelectedIndices = new List<int>();
			config.UseIcons = true;
			config.AcceptFileDrops = true;
			config.Columns = new ObservableCollection<Stoffi.ViewDetailsColumn>();
			config.NumberColumn = CreateListColumn("#", "#", "Number", "Number", 60, "Right", false);
			return config;
		}

		/// <summary>
		/// Creates a ViewDetailsColumn
		/// </summary>
		/// <param name="name">The name of the column</param>
		/// <param name="text">The displayed text</param>
		/// <param name="binding">The value to bind to</param>
		/// <param name="width">The width</param>
		/// <param name="isVisible">Whether the column is visible</param>
		/// <param name="alignment">The alignment of the text</param>
		/// <param name="isAlwaysVisible">Whether the column is always visible</param>
		/// <param name="isSortable">Whether the column is sortable</param>
		/// <returns>The newly created column</returns>
		public static ViewDetailsColumn CreateListColumn(string name, string text, string binding, int width,
														 string alignment = "Left",
														 bool isVisible = true,
														 bool isAlwaysVisible = false,
														 bool isSortable = true)
		{

			ViewDetailsColumn column = new ViewDetailsColumn();
			column.Name = name;
			column.Text = text;
			column.Binding = binding;
			column.Width = width;
			column.Alignment = alignment;
			column.IsAlwaysVisible = isAlwaysVisible;
			column.IsSortable = isSortable;
			column.IsVisible = isVisible;
			column.SortField = binding;
			return column;
		}

		/// <summary>
		/// Creates a ViewDetailsColumn
		/// </summary>
		/// <param name="name">The name of the column</param>
		/// <param name="text">The displayed text</param>
		/// <param name="binding">The value to bind to</param>
		/// <param name="sortField">The column to sort on</param>
		/// <param name="width">The width</param>
		/// <param name="isVisible">Whether the column is visible</param>
		/// <param name="alignment">The alignment of the text</param>
		/// <param name="isAlwaysVisible">Whether the column is always visible</param>
		/// <param name="isSortable">Whether the column is sortable</param>
		/// <returns>The newly created column</returns>
		public static ViewDetailsColumn CreateListColumn(string name, string text, string binding, string sortField, int width,
														 string alignment = "Left",
														 bool isVisible = true,
														 bool isAlwaysVisible = false,
														 bool isSortable = true)
		{

			ViewDetailsColumn column = new ViewDetailsColumn();
			column.Name = name;
			column.Text = text;
			column.Binding = binding;
			column.Width = width;
			column.Alignment = alignment;
			column.IsAlwaysVisible = isAlwaysVisible;
			column.IsSortable = isSortable;
			column.IsVisible = isVisible;
			column.SortField = sortField;
			return column;
		}


		private void ReadConfig(NewSettings settings, String file)
		{
			utility.LogMessageToFile(LogMode.Debug, "Reading config");
			if (File.Exists(file) == false)
			{
				utility.LogMessageToFile(LogMode.Error, "Could not find data file " + file);
				return;
			}


			XmlTextReader xmlReader = new XmlTextReader(file);
			xmlReader.WhitespaceHandling = WhitespaceHandling.None;
			xmlReader.Read();

			while (xmlReader.Read())
			{
				if (xmlReader.NodeType == XmlNodeType.Element)
				{
					if (xmlReader.Name == "setting")
					{
						String name = "";
						for (int i = 0; i < xmlReader.AttributeCount; xmlReader.MoveToAttribute(i), i++)
							if (xmlReader.Name == "name") name = xmlReader.Value;

						xmlReader.Read();
						if (!xmlReader.IsEmptyElement)
							xmlReader.Read();

						utility.LogMessageToFile(LogMode.Debug, "Parsing attribute '" + name + "'");

						if (name == "WinHeight")
							settings.WinHeight = xmlReader.Value;

						else if (name == "WinWidth")
							settings.WinWidth = xmlReader.Value;

						else if (name == "WinTop")
							settings.WinTop = xmlReader.Value;

						else if (name == "WinLeft")
							settings.WinLeft = xmlReader.Value;

						else if (name == "WinState")
							settings.WinState = xmlReader.Value;

						else if (name == "EqualizerHeight")
							settings.EqualizerHeight = xmlReader.Value;

						else if (name == "EqualizerWidth")
							settings.EqualizerWidth = xmlReader.Value;

						else if (name == "EqualizerTop")
							settings.EqualizerTop = xmlReader.Value;

						else if (name == "EqualizerLeft")
							settings.EqualizerLeft = xmlReader.Value;

						else if (name == "MediaState")
							settings.MediaState = xmlReader.Value;

						else if (name == "Language")
							settings.Language = xmlReader.Value;

						else if (name == "NavigationPaneWidth")
							settings.NavigationPaneWidth = xmlReader.Value;

						else if (name == "DetailsPaneHeight")
							settings.DetailsPaneHeight = xmlReader.Value;

						else if (name == "Repeat")
							settings.Repeat = xmlReader.Value;

						else if (name == "Shuffle")
							settings.Shuffle = xmlReader.Value;

						else if (name == "Volume")
							settings.Volume = xmlReader.Value;

						else if (name == "EqualizerProfiles")
							settings.EqualizerProfiles = ReadSetting<List<EqualizerProfile>>(xmlReader);

						else if (name == "CurrentEqualizerProfile")
							settings.CurrentEqualizerProfile = ReadSetting<EqualizerProfile>(xmlReader);

						else if (name == "Seek")
							settings.Seek = xmlReader.Value;

						else if (name == "CurrentTrack")
							settings.CurrentTrack = ReadSetting<TrackData>(xmlReader);

						else if (name == "HistoryIndex")
							settings.HistoryIndex = xmlReader.Value;

						else if (name == "CurrentSelectedNavigation")
							settings.CurrentSelectedNavigation = xmlReader.Value;

						else if (name == "CurrentActiveNavigation")
							settings.CurrentActiveNavigation = xmlReader.Value;

						else if (name == "CurrentVisualizer")
							settings.CurrentVisualizer = xmlReader.Value;

						else if (name == "MenuBarVisibility" || name == "MenuBarVisible")
							settings.MenuBarVisible = xmlReader.Value;

						else if (name == "DetailsPaneVisibility" || name == "DetailsPaneVisible")
							settings.DetailsPaneVisible = xmlReader.Value;

						else if (name == "FirstRun")
							settings.FirstRun = xmlReader.Value;

						else if (name == "PluginSettings")
							settings.PluginSettings = ReadSetting<List<PluginSettings>>(xmlReader);

						else if (name == "PluginListConfig")
							settings.PluginListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "Sources")
							settings.Sources = ReadSetting<List<SourceData>>(xmlReader);

						else if (name == "SourceListConfig")
							settings.SourceListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "LibraryListConfig")
							settings.LibraryListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "RadioListConfig")
							settings.RadioListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "DiscListConfig")
							settings.DiscListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "HistoryListConfig")
							settings.HistoryListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "QueueListConfig")
							settings.QueueListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "YouTubeListConfig")
							settings.YouTubeListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "SoundCloudListConfig")
							settings.SoundCloudListConfig = ReadSetting<ViewDetailsConfig>(xmlReader);

						else if (name == "LibraryTracks")
							settings.LibraryTracks = ReadSetting<List<TrackData>>(xmlReader);

						else if (name == "RadioTracks")
							settings.RadioTracks = ReadSetting<List<TrackData>>(xmlReader);

						else if (name == "HistoryTracks")
							settings.HistoryTracks = ReadSetting<List<TrackData>>(xmlReader);

						else if (name == "QueueTracks")
							settings.QueueTracks = ReadSetting<List<TrackData>>(xmlReader);

						else if (name == "Playlists")
							settings.Playlists = ReadSetting<List<PlaylistData>>(xmlReader);

						else if (name == "OpenAddPolicy")
							settings.OpenAddPolicy = xmlReader.Value;

						else if (name == "OpenPlayPolicy")
							settings.OpenPlayPolicy = xmlReader.Value;

						else if (name == "UpgradePolicy")
							settings.UpgradePolicy = xmlReader.Value;

						else if (name == "SearchPolicy")
							settings.SearchPolicy = xmlReader.Value;

						else if (name == "ShowOSD")
							settings.ShowOSD = xmlReader.Value;

						else if (name == "MinimizeToTray")
							settings.MinimizeToTray = xmlReader.Value;

						else if (name == "ID")
							settings.ID = xmlReader.Value;

						else if (name == "UpgradeCheck")
							settings.UpgradeCheck = xmlReader.Value;

						else if (name == "ShortcutProfiles")
							settings.ShortcutProfiles = ReadSetting<List<KeyboardShortcutProfile>>(xmlReader);

						else if (name == "OAuthToken")
							settings.OAuthToken = xmlReader.Value;

						else if (name == "OAuthSecret")
							settings.OAuthSecret = xmlReader.Value;

						else if (name == "SubmitSongs")
							settings.SubmitSongs = xmlReader.Value;

						else if (name == "SubmitPlaylists")
							settings.SubmitPlaylists = xmlReader.Value;

						else if (name == "PauseWhenLocked" || name == "PauseWhileLocked")
							settings.PauseWhenLocked = xmlReader.Value;

						else if (name == "PauseWhenSongEnds")
							settings.PauseWhenSongEnds = xmlReader.Value;

						else if (name == "CloudIdentities")
							settings.CloudIdentities = ReadSetting<List<CloudIdentity>>(xmlReader);

						utility.LogMessageToFile(LogMode.Debug, "Done");
					}
				}
			}

			xmlReader.Close();
		}

		private void WriteConfig(NewSettings settings, String file)
		{
			utility.LogMessageToFile(LogMode.Debug, "Writing config");
			XmlTextWriter xmlWriter = new XmlTextWriter(file, Encoding.UTF8);
			xmlWriter.WriteStartDocument();
			xmlWriter.WriteWhitespace("\n");
			xmlWriter.WriteStartElement("configuration");
			xmlWriter.WriteWhitespace("\n    ");

			xmlWriter.WriteStartElement("configSections");
			xmlWriter.WriteWhitespace("\n        ");
			xmlWriter.WriteStartElement("sectionGroup");
			xmlWriter.WriteStartAttribute("name");
			xmlWriter.WriteString("userSettings");
			xmlWriter.WriteEndAttribute();

			xmlWriter.WriteWhitespace("\n            ");
			xmlWriter.WriteStartElement("section");
			xmlWriter.WriteStartAttribute("name");
			xmlWriter.WriteString("Stoffi.Properties.Settings");
			xmlWriter.WriteEndAttribute();
			xmlWriter.WriteStartAttribute("type");
			xmlWriter.WriteString("System.Configuration.ClientSettingsSection, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			xmlWriter.WriteEndAttribute();
			xmlWriter.WriteStartAttribute("allowExeDefinition");
			xmlWriter.WriteString("MachineToLocalUser");
			xmlWriter.WriteEndAttribute();
			xmlWriter.WriteStartAttribute("requirePermission");
			xmlWriter.WriteString("false");
			xmlWriter.WriteEndAttribute();
			xmlWriter.WriteEndElement();

			xmlWriter.WriteWhitespace("\n        ");
			xmlWriter.WriteEndElement();

			xmlWriter.WriteWhitespace("\n    ");
			xmlWriter.WriteEndElement();


			xmlWriter.WriteWhitespace("\n    ");
			xmlWriter.WriteStartElement("userSettings");
			xmlWriter.WriteWhitespace("\n        ");
			xmlWriter.WriteStartElement("Stoffi.Properties.Settings");

			WriteSetting(xmlWriter, "WinWidth", 3, settings.WinWidth);
			WriteSetting(xmlWriter, "WinHeight", 3, settings.WinHeight);
			WriteSetting(xmlWriter, "WinTop", 3, settings.WinTop);
			WriteSetting(xmlWriter, "WinLeft", 3, settings.WinLeft);
			WriteSetting(xmlWriter, "WinWidth", 3, settings.EqualizerWidth);
			WriteSetting(xmlWriter, "WinState", 3, settings.WinState);
			WriteSetting(xmlWriter, "EqualizerHeight", 3, settings.EqualizerHeight);
			WriteSetting(xmlWriter, "EqualizerTop", 3, settings.EqualizerTop);
			WriteSetting(xmlWriter, "EqualizerLeft", 3, settings.EqualizerLeft);
			WriteSetting(xmlWriter, "MediaState", 3, settings.MediaState);
			WriteSetting(xmlWriter, "Language", 3, settings.Language);
			WriteSetting(xmlWriter, "NavigationPaneWidth", 3, settings.NavigationPaneWidth);
			WriteSetting(xmlWriter, "DetailsPaneHeight", 3, settings.DetailsPaneHeight);
			WriteSetting(xmlWriter, "Repeat", 3, settings.Repeat);
			WriteSetting(xmlWriter, "Shuffle", 3, settings.Shuffle);
			WriteSetting(xmlWriter, "Volume", 3, settings.Volume);
			WriteSetting<List<EqualizerProfile>>(xmlWriter, "EqualizerProfiles", "Xml", 3, settings.EqualizerProfiles);
			WriteSetting<EqualizerProfile>(xmlWriter, "CurrentEqualizerProfile", "Xml", 3, settings.CurrentEqualizerProfile);
			WriteSetting(xmlWriter, "Seek", 3, settings.Seek);
			WriteSetting<TrackData>(xmlWriter, "CurrentTrack", "Xml", 3, settings.CurrentTrack);
			WriteSetting(xmlWriter, "HistoryIndex", 3, settings.HistoryIndex);
			WriteSetting(xmlWriter, "CurrentSelectedNavigation", 3, settings.CurrentSelectedNavigation);
			WriteSetting(xmlWriter, "CurrentActiveNavigation", 3, settings.CurrentActiveNavigation);
			WriteSetting(xmlWriter, "CurrentVisualizer", 3, settings.CurrentVisualizer);
			WriteSetting(xmlWriter, "MenuBarVisible", 3, settings.MenuBarVisible);
			WriteSetting(xmlWriter, "DetailsPaneVisibile", 3, settings.DetailsPaneVisible);
			WriteSetting(xmlWriter, "FirstRun", 3, settings.FirstRun);
			WriteSetting<List<PluginSettings>>(xmlWriter, "PluginSettings", "Xml", 3, settings.PluginSettings);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "PluginListConfig", "Xml", 3, settings.PluginListConfig);
			WriteSetting<List<SourceData>>(xmlWriter, "Sources", "Xml", 3, settings.Sources);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "SourceListConfig", "Xml", 3, settings.SourceListConfig);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "LibraryListConfig", "Xml", 3, settings.LibraryListConfig);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "RadioListConfig", "Xml", 3, settings.RadioListConfig);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "DiscListConfig", "Xml", 3, settings.DiscListConfig);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "HistoryListConfig", "Xml", 3, settings.HistoryListConfig);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "QueueListConfig", "Xml", 3, settings.QueueListConfig);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "YouTubeListConfig", "Xml", 3, settings.YouTubeListConfig);
			WriteSetting<ViewDetailsConfig>(xmlWriter, "SoundCloudListConfig", "Xml", 3, settings.SoundCloudListConfig);
			WriteSetting<List<TrackData>>(xmlWriter, "LibraryTracks", "Xml", 3, settings.LibraryTracks);
			WriteSetting<List<TrackData>>(xmlWriter, "RadioTracks", "Xml", 3, settings.RadioTracks);
			WriteSetting<List<TrackData>>(xmlWriter, "HistoryTracks", "Xml", 3, settings.HistoryTracks);
			WriteSetting<List<TrackData>>(xmlWriter, "QueueTracks", "Xml", 3, settings.QueueTracks);
			WriteSetting<List<PlaylistData>>(xmlWriter, "Playlists", "Xml", 3, settings.Playlists);
			WriteSetting(xmlWriter, "OpenAddPolicy", 3, settings.OpenAddPolicy);
			WriteSetting(xmlWriter, "OpenPlayPolicy", 3, settings.OpenPlayPolicy);
			WriteSetting(xmlWriter, "UpgradePolicy", 3, settings.UpgradePolicy);
			WriteSetting(xmlWriter, "SearchPolicy", 3, settings.SearchPolicy);
			WriteSetting(xmlWriter, "ShowOSD", 3, settings.ShowOSD);
			WriteSetting(xmlWriter, "MinimizeToTray", 3, settings.MinimizeToTray);
			WriteSetting(xmlWriter, "ID", 3, settings.ID);
			WriteSetting(xmlWriter, "UpgradeCheck", 3, settings.UpgradeCheck);
			WriteSetting<List<KeyboardShortcutProfile>>(xmlWriter, "ShortcutProfiles", "Xml", 3, settings.ShortcutProfiles);
			WriteSetting(xmlWriter, "CurrentShortcutProfile", 3, settings.CurrentShortcutProfile);
			WriteSetting(xmlWriter, "OAuthToken", 3, settings.OAuthToken);
			WriteSetting(xmlWriter, "OAuthSecret", 3, settings.OAuthSecret);
			WriteSetting(xmlWriter, "SubmitSongs", 3, settings.SubmitSongs);
			WriteSetting(xmlWriter, "SubmitPlaylists", 3, settings.SubmitPlaylists);
			WriteSetting(xmlWriter, "PauseWhenLocked", 3, settings.PauseWhenLocked);
			WriteSetting(xmlWriter, "PauseWhenSongEnds", 3, settings.PauseWhenSongEnds);
			WriteSetting<List<CloudIdentity>>(xmlWriter, "CloudIdentities", "Xml", 3, settings.CloudIdentities);

			xmlWriter.WriteWhitespace("\n        ");
			xmlWriter.WriteEndElement();
			xmlWriter.WriteWhitespace("\n    ");
			xmlWriter.WriteEndElement();
			xmlWriter.WriteWhitespace("\n");
			xmlWriter.WriteEndElement();
			xmlWriter.WriteEndDocument();
			utility.LogMessageToFile(LogMode.Debug, "Write done");
			xmlWriter.Close();
		}

		/// <summary>
		/// Writes a settings to the XML settings file
		/// </summary>
		/// <typeparam name="T">The type of the settings</typeparam>
		/// <param name="xmlWriter">The XmlWriter</param>
		/// <param name="setting">The name of the setting</param>
		/// <param name="serializeAs">A string describing how the setting is serialized</param>
		/// <param name="indent">The number of spaces used for indentation</param>
		/// <param name="value">The value</param>
		private void WriteSetting<T>(XmlWriter xmlWriter, String setting, String serializeAs, int indent, T value)
		{
			String indentString = "";
			for (int i = 0; i < indent; i++)
				indentString += "    ";

			xmlWriter.WriteWhitespace("\n" + indentString);
			xmlWriter.WriteStartElement("setting");
			xmlWriter.WriteStartAttribute("name");
			xmlWriter.WriteString(setting);
			xmlWriter.WriteEndAttribute();
			xmlWriter.WriteStartAttribute("serializeAs");
			xmlWriter.WriteString(serializeAs);
			xmlWriter.WriteEndAttribute();

			xmlWriter.WriteWhitespace("\n" + indentString + "    ");
			xmlWriter.WriteStartElement("value");

			if (value != null)
			{
				XmlSerializer ser = new XmlSerializer(typeof(T));
				ser.Serialize(xmlWriter, value);
			}

			xmlWriter.WriteEndElement();

			xmlWriter.WriteWhitespace("\n" + indentString);
			xmlWriter.WriteEndElement();
		}

		/// <summary>
		/// Reads a setting from the XML settings file
		/// </summary>
		/// <typeparam name="T">The type of the setting</typeparam>
		/// <param name="xmlReader">The xml reader</param>
		/// <returns>The deserialized setting</returns>
		private T ReadSetting<T>(XmlTextReader xmlReader)
		{
			try
			{
				XmlSerializer ser = new XmlSerializer(typeof(T));
				return (T)ser.Deserialize(xmlReader);
			}
			catch (Exception e)
			{
				utility.LogMessageToFile(LogMode.Error, e.Message);
				return (T)(null as object);
			}
		}

		/// <summary>
		/// Reads an old list data structure and uses it to populate
		/// a list of tracks and a config structure.
		/// </summary>
		/// <param name="data">The old list data structure</param>
		/// <param name="tracks">The track list to populate</param>
		/// <param name="config">The config to populate</param>
		private void ListDataToListAndConfig(TrackListData data, List<TrackData> tracks, ViewDetailsConfig config)
		{
			tracks.AddRange(data.Tracks);

			config.SelectedIndices = new List<int>();
			foreach (string index in data.SelectedIndices)
				config.SelectedIndices.Add(Convert.ToInt32(index));

			config.Filter = data.SearchText;

			Dictionary<string, string> columnIndices = StringToDictionary(data.ColumnIndices);
			Dictionary<string, string> columnWidths = StringToDictionary(data.ColumnWidths);
			Dictionary<string, string> columnVisibilities = StringToDictionary(data.ColumnVisibilities);

			int artistWidth = columnWidths.ContainsKey("Artist") ? Convert.ToInt32(columnWidths["Artist"]) : 150;
			int albumWidth = columnWidths.ContainsKey("Album") ? Convert.ToInt32(columnWidths["Album"]) : 150;
			int titleWidth = columnWidths.ContainsKey("Title") ? Convert.ToInt32(columnWidths["Title"]) : 150;
			int genreWidth = columnWidths.ContainsKey("Genre") ? Convert.ToInt32(columnWidths["Genre"]) : 100;
			int lengthWidth = columnWidths.ContainsKey("Length") ? Convert.ToInt32(columnWidths["Length"]) : 100;
			int lpWidth = columnWidths.ContainsKey("LastPlayed") ? Convert.ToInt32(columnWidths["LastPlayed"]) : 150;
			int pcWidth = columnWidths.ContainsKey("PlayCount") ? Convert.ToInt32(columnWidths["PlayCount"]) : 100;
			int pathWidth = columnWidths.ContainsKey("Path") ? Convert.ToInt32(columnWidths["Path"]) : 300;

			bool artistVis = columnVisibilities.ContainsKey("Artist") && columnVisibilities["Artist"] == "visible";
			bool albumVis = columnVisibilities.ContainsKey("Album") && columnVisibilities["Album"] == "visible";
			bool titleVis = columnVisibilities.ContainsKey("Title") && columnVisibilities["Title"] == "visible";
			bool genreVis = columnVisibilities.ContainsKey("Genre") && columnVisibilities["Genre"] == "visible";
			bool lengthVis = columnVisibilities.ContainsKey("Length") && columnVisibilities["Length"] == "visible";
			bool lpVis = columnVisibilities.ContainsKey("LastPlayed") && columnVisibilities["LastPlayed"] == "visible";
			bool pcVis = columnVisibilities.ContainsKey("PlayCount") && columnVisibilities["PlayCount"] == "visible";
			bool pathVis = columnVisibilities.ContainsKey("Path") && columnVisibilities["Path"] == "visible";

			config.Columns = new ObservableCollection<ViewDetailsColumn>();
			Dictionary<string, ViewDetailsColumn> columns = new Dictionary<string, ViewDetailsColumn>();
			columns.Add("Artist", CreateListColumn("Artist", "Artist", "Artist", artistWidth, "Left", artistVis));
			columns.Add("Album", CreateListColumn("Album", "Album", "Album", albumWidth, "Left", albumVis));
			columns.Add("Title", CreateListColumn("Title", "Title", "Title", titleWidth, "Left", titleVis));
			columns.Add("Genre", CreateListColumn("Genre", "Genre", "Genre", genreWidth, "Left", genreVis));
			columns.Add("Length", CreateListColumn("Length", "Length", "Length", "RawLength", lengthWidth, "Right", lengthVis));
			columns.Add("Year", CreateListColumn("Year", "Year", "Year", "Year", 100, "Right", false));
			columns.Add("Last Played", CreateListColumn("LastPlayed", "Last played", "LastPlayed", lpWidth, "Left", lpVis));
			columns.Add("Play Count", CreateListColumn("PlayCount", "Play count", "PlayCount", pcWidth, "Right", pcVis));
			columns.Add("Track", CreateListColumn("Track", "Track", "Track", 100, "Right", false));
			columns.Add("Path", CreateListColumn("Path", "Path", "Path", pathWidth, "Left", pathVis));

			foreach (string column in columnIndices.Keys)
			{
				config.Columns.Add(columns[column]);
				columns.Remove(column);
			}

			foreach (ViewDetailsColumn column in columns.Values)
				config.Columns.Add(column);

			config.AcceptFileDrops = true;
			config.HasNumber = true;
			config.UseIcons = true;
			config.IsClickSortable = true;
			config.IsDragSortable = true;
			config.IsNumberVisible = false;
			config.LockSortOnNumber = false;
			config.Sorts = new List<string>();
			foreach (string sortPair in data.ColumnSorts)
			{
				string[] s = sortPair.Split(':');
				string dir = s[1] == "Ascending" ? "asc" : "dsc";
				string name = s[0];
				if (name == "Last Played") name = "LastPlayed";
				if (name == "Play Count") name = "PlayCount";
				config.Sorts.Add(dir + ":" + name);
			}
		}

		private Dictionary<string,string> StringToDictionary(string str)
		{
			Dictionary<string, string> ret = new Dictionary<string, string>();
			foreach (string pair in str.Split(';'))
			{
				string[] s = pair.Split(':');
				if (ret.ContainsKey(s[0]))
					ret.Remove(s[0]);
				ret.Add(s[0], s[1]);
			}
			return ret;
		}

		private void WriteSetting(XmlWriter xmlWriter, String setting, int indent, String value)
		{
			if (value == "" || value == null) return;
			String indentString = "";
			for (int i = 0; i < indent; i++)
				indentString += "    ";

			xmlWriter.WriteWhitespace("\n" + indentString);
			xmlWriter.WriteStartElement("setting");
			xmlWriter.WriteStartAttribute("name");
			xmlWriter.WriteString(setting);
			xmlWriter.WriteEndAttribute();
			xmlWriter.WriteStartAttribute("serializeAs");
			xmlWriter.WriteString("String");
			xmlWriter.WriteEndAttribute();

			xmlWriter.WriteWhitespace("\n" + indentString + "    ");
			xmlWriter.WriteStartElement("value");
			xmlWriter.WriteString(value);
			xmlWriter.WriteEndElement();

			xmlWriter.WriteWhitespace("\n" + indentString);
			xmlWriter.WriteEndElement();
		}

        #endregion

        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
	public class TrackData : ViewDetailsItemData
    {
        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public String Artist { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Album { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Title { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Genre { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Length { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String RawLength { get; set; }

		/// <summary>
		/// Gets the amount of views on YouTube in a localized,
		/// human readable format.
		/// </summary>
		public string Views { get; set; }

		/// <summary>
		/// Gets or sets the amount of views on YouTube.
		/// </summary>
		public int RawViews { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String LastPlayed { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String RawLastPlayed { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String PlayCount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String LastWrite { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Processed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Track { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Path { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String Year { get; set; }

		/// <summary>
		/// Gets or sets the bitrate of the track
		/// </summary>
		public int Bitrate { get; set; }

		/// <summary>
		/// Gets or sets the number of channels of the track
		/// </summary>
		public int Channels { get; set; }

		/// <summary>
		/// Gets or sets the sample rate of the track
		/// </summary>
		public int SampleRate { get; set; }

		/// <summary>
		/// Gets or sets the codecs of the track
		/// </summary>
		public string Codecs { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String Source { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public List<String> Bookmarks { get; set; }

        #endregion
    }

	/// <summary>
	/// Describes an equalizer profile
	/// </summary>
	public class EqualizerProfile
	{
		/// <summary>
		/// Get or sets the name of the profile
		/// </summary>
		public String Name { get; set; }

		/// <summary>
		/// Get or sets whether the user can modify the profile
		/// </summary>
		public Boolean IsProtected { get; set; }

		/// <summary>
		/// Get or sets the levels.
		/// </summary>
		/// <remarks>
		/// Is a list with 10 floats between 0 and 10,
		/// where each float represents the maximum level
		/// on a frequency band going from lower to higher.
		/// </remarks>
		public float[] Levels;

		/// <summary>
		/// Get or sets the echo level.
		/// A float ranging from 0 to 10 going from
		/// dry to wet.
		/// </summary>
		public string EchoLevel;
	}

	/// <summary>
	/// 
	/// </summary>
	public class TrackListData
	{
		#region Properties

		/// <summary>
		/// 
		/// </summary>
		public String ColumnWidths { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String ColumnIndices { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String ColumnVisibilities { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public List<String> SelectedIndices { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public List<String> ColumnSorts { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String SearchText { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public List<TrackData> Tracks { get; set; }

		#endregion
	}

    /// <summary>
    /// 
    /// </summary>
	public class PlaylistData
    {
        #region Properites

        /// <summary>
        /// 
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public String Time { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public List<TrackData> Tracks { get; set; }

		/// <summary>
		/// Get or sets the configuration of the list view
		/// </summary>
		public ViewDetailsConfig ListConfig { get; set; }

		/// <summary>
		/// Deprecated!
		/// </summary>
		public TrackListData ListData { get; set; }

        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
	public class SourceData : ViewDetailsItemData
    {
		#region Properties

		/// <summary>
		/// 
		/// </summary>
		public String Include { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String Ignore { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String Type { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public String HumanType { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String Data { get; set; }

        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
	public class KeyboardShortcut
    {
        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String Category { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String Keys { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String IsGlobal { get; set; }

        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
	public class KeyboardShortcutProfile
    {
        #region Members

        public List<KeyboardShortcut> Shortcuts; // TODO: rename to shortcuts

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public String IsProtected { get; set; }

        #endregion
    }

	public class PluginSettings
	{
		public String PluginID { get; set; }
		public List<Setting> Settings { get; set; }
		public String Enabled { get; set; }
		public String Installed { get; set; }
	}

	public class Setting
	{
		public String ID { get; set; }
		public String Type { get; set; }
		public String Value { get; set; }
		public String Maximum { get; set; }
		public String Minimum { get; set; }
		public List<String> PossibleValues { get; set; }
		public String IsVisible { get; set; }
	}

	public class CloudIdentity
	{
		public String UserID { get; set; }
		public String ConfigurationID { get; set; }
		public String Synchronize { get; set; }
		public String SynchronizePlaylists { get; set; }
		public String SynchronizeConfig { get; set; }
		public String SynchronizeQueue { get; set; }
		public String SynchronizeFiles { get; set; }
		public String DeviceID { get; set; }
	}

	/// <summary>
	/// The settings after the migration
	/// </summary>
	public class NewSettings
	{
		#region Properties

		public String WinHeight { get; set; }
		public String WinWidth { get; set; }
		public String WinTop { get; set; }
		public String WinLeft { get; set; }
		public String WinState { get; set; }
		public String EqualizerHeight { get; set; }
		public String EqualizerWidth { get; set; }
		public String EqualizerTop { get; set; }
		public String EqualizerLeft { get; set; }
		public String MediaState { get; set; }
		public String Language { get; set; }
		public String NavigationPaneWidth { get; set; }
		public String DetailsPaneHeight { get; set; }
		public String Shuffle { get; set; }
		public String Repeat { get; set; }
		public String Volume { get; set; }
		public List<EqualizerProfile> EqualizerProfiles { get; set; }
		public EqualizerProfile CurrentEqualizerProfile { get; set; }
		public String Seek { get; set; }
		public TrackData CurrentTrack { get; set; }
		public String HistoryIndex { get; set; }
		public String CurrentSelectedNavigation { get; set; }
		public String CurrentActiveNavigation { get; set; }
		public String CurrentVisualizer { get; set; }
		public String MenuBarVisible { get; set; }
		public String DetailsPaneVisible { get; set; }
		public String FirstRun { get; set; }
		public List<PluginSettings> PluginSettings { get; set; }
		public ViewDetailsConfig PluginListConfig { get; set; }
		public List<SourceData> Sources { get; set; }
		public ViewDetailsConfig SourceListConfig { get; set; }
		public ViewDetailsConfig LibraryListConfig { get; set; }
		public ViewDetailsConfig RadioListConfig { get; set; }
		public ViewDetailsConfig DiscListConfig { get; set; }
		public ViewDetailsConfig HistoryListConfig { get; set; }
		public ViewDetailsConfig QueueListConfig { get; set; }
		public ViewDetailsConfig YouTubeListConfig { get; set; }
		public ViewDetailsConfig SoundCloudListConfig { get; set; }
		public List<TrackData> LibraryTracks { get; set; }
		public List<TrackData> RadioTracks { get; set; }
		public List<TrackData> HistoryTracks { get; set; }
		public List<TrackData> QueueTracks { get; set; }
		public List<PlaylistData> Playlists { get; set; }
		public String OpenAddPolicy { get; set; }
		public String OpenPlayPolicy { get; set; }
		public String UpgradePolicy { get; set; }
		public String SearchPolicy { get; set; }
		public String ShowOSD { get; set; }
		public String MinimizeToTray { get; set; }
		public String ID { get; set; }
		public String UpgradeCheck { get; set; }
		public List<KeyboardShortcutProfile> ShortcutProfiles { get; set; }
		public String CurrentShortcutProfile { get; set; }
		public String OAuthToken { get; set; }
		public String OAuthSecret { get; set; }
		public String SubmitSongs { get; set; }
		public String SubmitPlaylists { get; set; }
		public String PauseWhenLocked { get; set; }
		public String PauseWhenSongEnds { get; set; }
		public List<CloudIdentity> CloudIdentities { get; set; }

		#endregion
	}

	/// <summary>
	/// Describes the data source of an item inside the ViewDetails list
	/// </summary>
	public class ViewDetailsItemData
	{
		#region Properties

		/// <summary>
		/// Gets or sets the index number of the item
		/// </summary>
		public int Number { get; set; }

		/// <summary>
		/// Gets or sets whether the item is marked as active or not
		/// </summary>
		public bool IsActive { get; set; }

		/// <summary>
		/// Gets or sets the icon of the item
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// Gets or sets whether the items should feature a strikethrough
		/// </summary>
		public bool Strike { get; set; }

		#endregion
	}

	/// <summary>
	/// Describes a configuration for the ViewDetails class
	/// </summary>
	public class ViewDetailsConfig
	{
		#region Properties

		/// <summary>
		/// Gets or sets the columns
		/// </summary>
		public ObservableCollection<ViewDetailsColumn> Columns { get; set; }

		/// <summary>
		/// Gets or sets the number column configuration
		/// </summary>
		public ViewDetailsColumn NumberColumn { get; set; }

		/// <summary>
		/// Gets or sets the indices of the selected items
		/// </summary>
		public List<int> SelectedIndices { get; set; }

		/// <summary>
		/// Gets or sets the the sort orders
		/// Each sort is represented as a string on the format
		/// "asc/dsc:ColumnName"
		/// </summary>
		public List<string> Sorts { get; set; }

		/// <summary>
		/// Gets or sets text used to filter the list
		/// </summary>
		public string Filter { get; set; }

		/// <summary>
		/// Gets or sets whether the number column should be enabled
		/// </summary>
		public bool HasNumber { get; set; }

		/// <summary>
		/// Gets or sets whether the number column should be visible
		/// </summary>
		public bool IsNumberVisible { get; set; }

		/// <summary>
		/// Gets or sets the position of the number column
		/// </summary>
		public int NumberIndex { get; set; }

		/// <summary>
		/// Gets or sets whether to display icons or not
		/// </summary>
		public bool UseIcons { get; set; }

		/// <summary>
		/// Gets or sets whether files can be dropped onto the list
		/// </summary>
		public bool AcceptFileDrops { get; set; }

		/// <summary>
		/// Gets or sets whether the list can be resorted via drag and drop
		/// </summary>
		public bool IsDragSortable { get; set; }

		/// <summary>
		/// Gets or sets whether the list can be resorted by clicking on a column
		/// </summary>
		public bool IsClickSortable { get; set; }

		/// <summary>
		/// Gets or sets whether only the number column can be used to sort the list
		/// </summary>
		public bool LockSortOnNumber { get; set; }

		#endregion
	}

	/// <summary>
	/// Represents a column of a details list
	/// </summary>
	public class ViewDetailsColumn
	{
		#region Properties

		/// <summary>
		/// Gets or sets the name of the column
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the displayed text
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Gets or sets the value to bind to
		/// </summary>
		public string Binding { get; set; }

		/// <summary>
		/// Gets or sets the value to sort on
		/// </summary>
		public string SortField { get; set; }

		/// <summary>
		/// Gets or sets whether the column is always visible
		/// </summary>
		public bool IsAlwaysVisible { get; set; }

		/// <summary>
		/// Gets or sets whether the column is sortable
		/// </summary>
		public bool IsSortable { get; set; }

		/// <summary>
		/// Gets or sets the width of the column
		/// </summary>
		public double Width { get; set; }

		/// <summary>
		/// Gets or sets whether the column is visible (only effective if IsAlwaysVisible is false)
		/// </summary>
		public bool IsVisible { get; set; }

		/// <summary>
		/// Gets or sets the text alignment of the displayed text
		/// </summary>
		public String Alignment { get; set; }

		#endregion
	}

    /// <summary>
    /// 
    /// </summary>
    interface IMigrator
    {
        #region Constructor

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fromFile"></param>
        /// <param name="toFile"></param>
        void Migrate(String fromFile, String toFile);

        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
	public class Utility
    {
        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public string LogFile { get; set; }

        /// <summary>
        /// 
        /// </summary>
		public LogMode Mode { get; set; }

        /// <summary>
        /// 
        /// </summary>
		private DateTime InitTime { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// 
        /// </summary>
        public Utility()
		{
			LogFile = "Stoffi.log";
			Mode = LogMode.Warning;
			InitTime = DateTime.Now;
		}

        #endregion

        #region Methods

        #region Public

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="message"></param>
        public void LogMessageToFile(LogMode mode, string message)
		{
			if (ModeToInt(mode) < ModeToInt(Mode)) return;

			TimeSpan ts = (DateTime.Now - InitTime);

#if (!DEBUG)
			System.IO.StreamWriter sw = System.IO.File.AppendText(LogFile);
#endif
			try
			{
				string logLine = String.Format("{0} {1}:{2:00}:{3:00}.{4:000} ({5:G}) [{6}] {7}",
					ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds, DateTime.Now, ModeToString(mode), message);
#if (!DEBUG)
				sw.WriteLine(logLine);
#endif
				if (Mode == LogMode.Debug)
					Console.WriteLine(logLine);
			}
			finally
			{
#if (!DEBUG)
				sw.Close();
#endif
			}
		}

        #endregion

        #region Private

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        private int ModeToInt(LogMode mode)
		{
			if (mode == LogMode.Debug) return 1;
			else if (mode == LogMode.Information) return 2;
			else if (mode == LogMode.Warning) return 3;
			else if (mode == LogMode.Error) return 4;
			else return 0;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
		private string ModeToString(LogMode mode)
		{
			if (mode == LogMode.Debug) return "DEBUG";
			else if (mode == LogMode.Information) return "INFO";
			else if (mode == LogMode.Warning) return "OOPS";
			else if (mode == LogMode.Error) return "SHIT";
			else return "HUH?";
        }

        #endregion

        #endregion
    }

	public enum LogMode
	{
        /// <summary>
        /// 
        /// </summary>
		Debug,

        /// <summary>
        /// 
        /// </summary>
		Information,

        /// <summary>
        /// 
        /// </summary>
		Warning,

        /// <summary>
        /// 
        /// </summary>
		Error
	}

	/// <summary>
	/// The type of a source
	/// </summary>
	public enum SourceType
	{
		/// <summary>
		/// A single file
		/// </summary>
		File,

		/// <summary>
		/// A folder
		/// </summary>
		Folder,

		/// <summary>
		/// A Windows 7 Library
		/// </summary>
		Library
	}
}
