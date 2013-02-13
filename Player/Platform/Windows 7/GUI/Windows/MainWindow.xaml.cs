﻿/***
 * MainWindow.xaml.cs
 * 
 * The logic behind the main window. Contains
 * the code that connects the different part
 * of Stoffi. Sort of like the spider in the net.
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
 ***/

using GlassLib;
using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Markup;

using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Taskbar;
using Microsoft.Win32;

using Tomers.WPF.Localization;

using Stoffi.Plugins;
using Stoffi.Core;
using Stoffi.Platform.Windows7.GUI;
using Stoffi.Platform.Windows7.GUI.Controls;

namespace Stoffi
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class StoffiWindow : Window, INotifyPropertyChanged
	{
		#region Members

		private KeyboardListener kListener = new KeyboardListener();
		private ContextMenu trayMenu;
		private MenuItem trayMenuShow;
		private MenuItem trayMenuPlay;
		private MenuItem trayMenuNext;
		private MenuItem trayMenuPrev;
		private MenuItem trayMenuExit;
		private MenuItem showMenuDetailsPane;
		private MenuItem showMenuMenuBar;
		private Microsoft.WindowsAPICodePack.Taskbar.ThumbnailToolbarButton taskbarPrev;
		private Microsoft.WindowsAPICodePack.Taskbar.ThumbnailToolbarButton taskbarPlay;
		private Microsoft.WindowsAPICodePack.Taskbar.ThumbnailToolbarButton taskbarNext;
		public TaskbarIcon trayIcon;
		private WindowState oldWindowState = WindowState.Minimized;
		private WindowState currentWindowState = WindowState.Normal;
		private System.Windows.Shell.JumpList jumpList;
		private System.Windows.Shell.JumpTask jumpTaskPlay;
		private System.Windows.Shell.JumpTask jumpTaskNext;
		private System.Windows.Shell.JumpTask jumpTaskPrev;
		private bool glassEffect = true;
		private List<Key> currentPressedKeys = new List<Key>();
		private EditableTextBlock etbInEdit = null;
		private Equalizer equalizer = null;
		private DispatcherTimer resortDelay = new DispatcherTimer();
		private List<string> pathsThatWasChanged = new List<string>();
		private MenuItem listMenuPlay = new MenuItem();
		private MenuItem listMenuRemove = new MenuItem();
		private MenuItem listMenuDelete = new MenuItem();
		private MenuItem listMenuInfo = new MenuItem();
		private MenuItem listMenuQueue = new MenuItem();
		private MenuItem listMenuMove = new MenuItem();
		private MenuItem listMenuCopy = new MenuItem();
		private Separator listMenuFilesystemSeparator = new Separator();
		private MenuItem listMenuAddToNew = new MenuItem();
		private MenuItem listMenuAddToPlaylist = new MenuItem();
		private MenuItem listMenuRemoveFromPlaylist = new MenuItem();
		private MenuItem listMenuWatchOnYouTube = new MenuItem();
		private MenuItem listMenuListenOnSoundCloud = new MenuItem();
		private MenuItem listMenuListenOnJamendo = new MenuItem();
		private MenuItem listMenuOpenFolder = new MenuItem();
		private MenuItem listMenuShareSong = new MenuItem();
		private MenuItem listMenuVisitWebsite = new MenuItem();
		private ContextMenu listMenu = new ContextMenu();
		private string dialogPath = @"C:\";
		private DispatcherTimer sourceModifiedDelay = new DispatcherTimer();
		private Hashtable sourceModifiedTracks = new Hashtable();
		private bool doRestart = false;
		private TrackData currentlySelectedTrack = null;
		private List<KeyValuePair<ScannerCallback, object>> sourceModifiedCallbacks = new List<KeyValuePair<ScannerCallback,object>>();
		private bool resumeWhenBack = false; // whether or not to resume playback at unlock
		private bool showMediaError = false; // whether or not to show errors from media manager as popup
		private bool temporarilyShowMenuBar = false;
		private DispatcherTimer trackSwitchDelay = new DispatcherTimer();
		private DispatcherTimer loadedTrackDelay = new DispatcherTimer();
		private Thread detailsThread;
		public ControlPanel ControlPanel;
		private bool forceShutdown = false;
		private Thread collectionCopyThread;
		private ItemCollection currentTrackCollection;

		private ViewDetails FileTracks;
		private ViewDetails QueueTracks;
		private ViewDetails HistoryTracks;
		private ViewDetails RadioTracks;
		private ViewDetails DiscTracks;
		private SoundCloudTracks SoundCloudTracks;
		private YouTubeTracks YouTubeTracks;
		private JamendoTracks JamendoTracks;
		private Video VideoContainer;
		private Stoffi.Platform.Windows7.GUI.Controls.Visualizer VisualizerContainer;

		#endregion

		#region Properties

		/// <summary>
		/// The total time of all tracks in the history (in seconds)
		/// </summary>
		public double HistoryTime { get; private set; }

		/// <summary>
		/// The total time of all tracks in the queue (in seconds)
		/// </summary>
		public double QueueTime { get; private set; }

		/// <summary>
		/// The total time of all tracks in the library (in seconds)
		/// </summary>
		public double LibraryTime { get; private set; }

		/// <summary>
		/// A table of the TrackList for each playlist
		/// </summary>
		public Hashtable PlaylistTrackLists { get; private set; }

		/// <summary>
		/// The height of the details pane
		/// </summary>
		public double DetailsPaneHeight
		{
			get { return SettingsManager.DetailsPaneHeight; }
			set { SettingsManager.DetailsPaneHeight = value; }
		}

		/// <summary>
		/// Sets whether the details pane should be visible or not
		/// </summary>
		public bool DetailsPaneVisible
		{
			set
			{
				DetailsRow.MinHeight = (value ? 53 : 0);
				DetailsRow.Height = new GridLength((value ? SettingsManager.DetailsPaneHeight : 0));
				DetailsPane.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
				HorizontalSplitter.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		#endregion

		#region Constructor

		/// <summary>
		/// Creates an instance of the main window.
		/// </summary>
		public StoffiWindow()
		{
			//SettingsManager.FirstRun = true;
			////*********** Next ***********/
			//// TODO: In-place track edit
			//// TODO: Images (currently playing, queue) in front of track + style
			//// TODO: Images (currently playing) in front of playlists/library
			//// TODO: MusicBrainz
			//// TODO: Jumplist: playlists
			//// TODO: Drag search text
			//// TODO: Track score (how good is it?)
			//// TODO: Last.fm support
			//// TODO: Spotify
			//// TODO: Spectrum
			//// TODO: Views (album, artist, genre, all)
			//// TODO: Playlist view
			//// TODO: Network sources
			//// TODO: Device sources
			//// TODO: Tools for cutting track
			//// TODO: Lyrics
			//// TODO: Wikipedia
			//// TODO: Social music

			FrameworkElement.LanguageProperty.OverrideMetadata(
				typeof(FrameworkElement),
				new FrameworkPropertyMetadata(
				XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag)));

			if (!Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.IsPlatformSupported)
			{
				MessageBox.Show(U.T("MessageOldWindows", "Message"), U.T("MessageOldWindows", "Title"), MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
				return;
			}

			SettingsManager.Initialize();
			PlaylistManager.Initialize();
			ServiceManager.Initialize();
			MediaManager.Initialize();
			MediaManager.FetchCollectionCallback = FetchActiveTrackCollection;
			UpgradeManager.Initialize(new TimeSpan(1, 0, 0), 60);
			FilesystemManager.Initialize();

			////U.L(LogLevel.Debug, "MAIN", "Send any app arguments to media manager");
			String[] arguments = Environment.GetCommandLineArgs();
			if (arguments.Length > 1)
			{
				Thread at = new Thread(delegate()
				{
					if (arguments[1] == "/play") MediaManager.Play();
					else if (arguments[1] == "/pause") MediaManager.Pause();
					else if (arguments[1] == "/stop") MediaManager.Stop();
					else if (arguments[1] == "/next") MediaManager.Next(true);
					else if (arguments[1] == "/previous") MediaManager.Previous();

					else if (MediaManager.IsSupported(arguments[1]))
						Open(arguments[1], true);

					else if (PlaylistManager.IsSupported(arguments[1]))
					{
						PlaylistData pl = PlaylistManager.LoadPlaylist(arguments[1]);
						if (pl != null)
						{
							SettingsManager.CurrentSelectedNavigation = "Playlist:" + pl.Name;
							if (pl.Tracks.Count > 0)
							{
								MediaManager.Load(pl.Tracks[0]);
								MediaManager.Play();
							}
						}
					}
				});
				at.Name = "Argument thread";
				at.Priority = ThreadPriority.Highest;
				at.Start();
			}

			PlaylistTrackLists = new Hashtable();

			#region Context menus

			listMenuAddToPlaylist.Header = U.T("MenuAddToPlaylist", "Header");
			listMenuDelete.Header = U.T("MenuDelete");
			listMenuDelete.Click += new RoutedEventHandler(listMenuDelete_Click);
			listMenuInfo.Header = U.T("MenuInfo");
			listMenuInfo.Click += new RoutedEventHandler(listMenuInfo_Click);
			listMenuPlay.Header = U.T("MenuPlay");
			listMenuPlay.Click += new RoutedEventHandler(listMenuPlay_Click);
			listMenuPlay.FontWeight = FontWeights.Bold;
			listMenuWatchOnYouTube.Header = U.T("MenuWatchOnYouTube");
			listMenuWatchOnYouTube.Click += new RoutedEventHandler(listMenuWatchOnYouTube_Click);
			listMenuListenOnSoundCloud.Header = U.T("MenuListenOnSoundCloud");
			listMenuListenOnSoundCloud.Click += new RoutedEventHandler(listMenuListenOnSoundCloud_Click);
			listMenuListenOnJamendo.Header = U.T("MenuListenOnJamendo");
			listMenuListenOnJamendo.Click += new RoutedEventHandler(listMenuListenOnJamendo_Click);
			listMenuOpenFolder.Header = U.T("MenuOpenFolder");
			listMenuOpenFolder.Click += new RoutedEventHandler(listMenuOpenFolder_Click);
			listMenuShareSong.Header = U.T("MenuShareSong");
			listMenuShareSong.Click += new RoutedEventHandler(listMenuShareSong_Click);
			listMenuVisitWebsite.Header = U.T("MenuVisitWebsite", "Header");
			listMenuVisitWebsite.Click += new RoutedEventHandler(listMenuVisitWebsite_Click);
			listMenuQueue.Header = U.T("MenuQueue");
			listMenuQueue.Click += new RoutedEventHandler(listMenuQueue_Click);
			listMenuCopy.Header = U.T("MenuCopy");
			listMenuCopy.Click += new RoutedEventHandler(listMenuCopyMove_Click);
			listMenuMove.Header = U.T("MenuMove");
			listMenuMove.Click += new RoutedEventHandler(listMenuCopyMove_Click);
			listMenuRemove.Header = U.T("MenuRemove");
			listMenuRemove.Click += new RoutedEventHandler(listMenuRemove_Click);
			listMenuRemoveFromPlaylist.Header = U.T("MenuRemoveFromPlaylist", "Header");
			listMenuRemoveFromPlaylist.Visibility = System.Windows.Visibility.Collapsed;

			listMenuAddToNew = new MenuItem();
			listMenuAddToNew.Header = U.T("MenuCreateNew", "Header");
			listMenuAddToNew.FontStyle = FontStyles.Italic;
			listMenuAddToNew.Click += new RoutedEventHandler(listMenuAddToPlaylist_Click);
			listMenuAddToPlaylist.Items.Add(listMenuAddToNew);

			listMenu.Items.Add(listMenuPlay);
			listMenu.Items.Add(listMenuQueue);
			listMenu.Items.Add(new Separator());
			listMenu.Items.Add(listMenuAddToPlaylist);
			listMenu.Items.Add(listMenuRemoveFromPlaylist);
			listMenu.Items.Add(new Separator());
			listMenu.Items.Add(listMenuOpenFolder);
			listMenu.Items.Add(listMenuCopy);
			listMenu.Items.Add(listMenuMove);
			listMenu.Items.Add(listMenuRemove);
			listMenu.Items.Add(listMenuDelete);
			listMenu.Items.Add(listMenuFilesystemSeparator);
			listMenu.Items.Add(listMenuWatchOnYouTube);
			listMenu.Items.Add(listMenuListenOnSoundCloud);
			listMenu.Items.Add(listMenuListenOnJamendo);
			listMenu.Items.Add(listMenuVisitWebsite);
			listMenu.Items.Add(listMenuShareSong);
			listMenu.Items.Add(listMenuInfo);

			#endregion

			//U.L(LogLevel.Debug, "MAIN", "Initialize");
			InitializeComponent();
			//U.L(LogLevel.Debug, "MAIN", "Initialized");

			PowerManager.Initialize();
			PowerManager.UIDispatcher = Dispatcher;

			((Image)HelpButton.Content).Source = Utilities.GetIcoImage("pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/Help.ico", 16, 16);
			((Image)UpgradeButton.Content).Source = Utilities.GetIcoImage("pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/Upgrade.ico", 16, 16);

			showMenuDetailsPane = ShowButton.ArrowMenu.Items[0] as MenuItem;
			showMenuMenuBar = ShowButton.ArrowMenu.Items[1] as MenuItem;

			if (Left < -10000)
				Left = 0;
			if (Top < -10000)
				Top = 0;

			// place in middle of primary screen
			if (Properties.Settings.Default.FirstRun)
			{
				Left = (SystemParameters.PrimaryScreenWidth / 2) - (Width / 2);
				Top = (SystemParameters.PrimaryScreenHeight / 2) - (Height / 2);
			}

			SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(SystemEvents_UserPreferenceChanged);
		}

		#endregion

		#region Methods

		#region Public

		/// <summary>
		/// Restarts the application
		/// </summary>
		public void Restart()
		{
			doRestart = true;
			this.Close();
		}

		/// <summary>
		/// Turns a list of key modifiers into human readable text.
		/// </summary>
		/// <param name="modifiers">A list of the keys</param>
		/// <returns>The keys in human readable text, ex: "Ctrl+Alt" or "Win+Shift"</returns>
		public String GetModifiersAsText(List<Key> modifiers)
		{
			String txt = "";
			foreach (Key k in modifiers)
			{
				if (k == Key.LeftCtrl || k == Key.RightCtrl) txt += "Ctrl+";
				else if (k == Key.LeftAlt || k == Key.RightAlt) txt += "Alt+";
				else if (k == Key.LWin || k == Key.RWin) txt += "Win+";
				else if (k == Key.LeftShift || k == Key.RightShift) txt += "Shift+";
			}
			if (txt.Length > 0) return txt.Substring(0, txt.Length - 1);
			else return "";
		}

		/// <summary>
		/// Turns a key into human readable text.
		/// </summary>
		/// <param name="k">The key</param>
		/// <returns>The key as human readable text, ex: "8 (numpad)" or "Backspace"</returns>
		public String KeyToString(Key k)
		{
			switch (k)
			{
				case Key.NumPad0:
					return "0 (numpad)";
				case Key.NumPad1:
					return "1 (numpad)";
				case Key.NumPad2:
					return "2 (numpad)";
				case Key.NumPad3:
					return "3 (numpad)";
				case Key.NumPad4:
					return "4 (numpad)";
				case Key.NumPad5:
					return "5 (numpad)";
				case Key.NumPad6:
					return "6 (numpad)";
				case Key.NumPad7:
					return "7 (numpad)";
				case Key.NumPad8:
					return "8 (numpad)";
				case Key.NumPad9:
					return "9 (numpad)";
				case Key.D0:
					return "0";
				case Key.D1:
					return "1";
				case Key.D2:
					return "2";
				case Key.D3:
					return "3";
				case Key.D4:
					return "4";
				case Key.D5:
					return "5";
				case Key.D6:
					return "6";
				case Key.D7:
					return "7";
				case Key.D8:
					return "8";
				case Key.D9:
					return "9";
				case Key.OemComma:
					return ",";
				case Key.OemPeriod:
					return ".";
				case Key.Subtract:
					return "- (numpad)";
				case Key.Multiply:
					return "* (numpad)";
				case Key.Divide:
					return "/ (numpad)";
				case Key.Add:
					return "+ (numpad)";
				case Key.Back:
					return "Backspace";
				case Key.OemMinus:
					return "-";
				case Key.CapsLock:
					return "CapsLock";
				case Key.Scroll:
					return "ScrollLock";
				case Key.PrintScreen:
					return "PrintScreen";
				case Key.Return:
					return "Enter";
				case Key.PageDown:
					return "PageDown";
				case Key.PageUp:
					return "PageUp";

				// TODO: hardcoded temporary fix
				case Key.Oem3:
					return "´";
				case Key.OemPlus:
					return "=";
				case Key.OemOpenBrackets:
					return "[";
				case Key.Oem6:
					return "]";
				case Key.Oem5:
					return @"\";
				case Key.Oem1:
					return ";";
				case Key.OemQuotes:
					return "'";
				case Key.OemQuestion:
					return "/";

				default:
					return k.ToString();
			}
		}

		/// <summary>
		/// Invoked when the application is called from a second instance.
		/// </summary>
		/// <param name="argument">Arguments send from second instance</param>
		public void CallFromSecondInstance(String argument)
		{
			argument = argument.Trim();
			U.L(LogLevel.Debug, "MAIN", "Got call from second instance with argument: |" + argument + "|");

			if (argument == "/play")
				PlayPause();

			else if (argument == "/next")
				MediaManager.Next(true);

			else if (argument == "/previous")
				MediaManager.Previous();

			else if (String.IsNullOrWhiteSpace(argument))
			{
				U.L(LogLevel.Debug, "MAIN", "No arguments, trying to raise window to foreground");
				if (SettingsManager.FastStart)
				{
					ShowInTaskbar = true;
					Show();
					trayIcon.Visibility = Visibility.Visible;
				}
			}
			else
			{
				// argument is a playlist
				if (PlaylistManager.IsSupported(argument))
				{
					PlaylistData pl = PlaylistManager.LoadPlaylist(argument);
					if (pl != null)
					{
						SettingsManager.CurrentSelectedNavigation = "Playlist:" + pl.Name;
						if (pl.Tracks.Count > 0)
						{
							if (SettingsManager.MediaState == Core.MediaState.Playing)
								MediaManager.Pause();
							MediaManager.Load(pl.Tracks[0]);
							MediaManager.Play();
						}
					}
				}

				// argument is a track
				else if (MediaManager.IsSupported(argument))
					Open(argument);
			}
		}

		/// <summary>
		/// Retrives the currently selected track list's source.
		/// </summary>
		/// <returns>The collection used as ItemsSource for the current track list</returns>
		public ObservableCollection<TrackData> GetCurrentTrackCollection()
		{
			if (SettingsManager.CurrentSelectedNavigation == "History")
				return SettingsManager.HistoryTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "Queue")
				return SettingsManager.QueueTracks;
			else if (SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
			{
				PlaylistData p = PlaylistManager.FindPlaylist(SettingsManager.CurrentSelectedNavigation.Split(new[] { ':' }, 2)[1]);
				if (p == null) return null;
				return p.Tracks;
			}
			else if (SettingsManager.CurrentSelectedNavigation == "Files")
				return SettingsManager.FileTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "Radio")
				return SettingsManager.RadioTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "YouTube")
				return YouTubeManager.TrackSource;
			else if (SettingsManager.CurrentSelectedNavigation == "SoundCloud")
				return SoundCloudManager.TrackSource;
			else if (SettingsManager.CurrentSelectedNavigation == "Jamendo")
				return JamendoManager.TrackSource;
			else
				return null;
		}

		/// <summary>
		/// Gets the currently selected track list.
		/// </summary>
		/// <returns>The selected track list</returns>
		public ViewDetails GetCurrentTrackList()
		{
			if (SettingsManager.CurrentSelectedNavigation == "History")
				return HistoryTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "Queue")
				return QueueTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "Radio")
				return RadioTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "Files")
				return FileTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "YouTube")
				return YouTubeTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "SoundCloud")
				return SoundCloudTracks;
			else if (SettingsManager.CurrentSelectedNavigation == "Jamendo")
				return JamendoTracks;
			else if (SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
				return (ViewDetails)PlaylistTrackLists[SettingsManager.CurrentSelectedNavigation.Split(new[] { ':' }, 2)[1]];
			else
				return null;
		}

		/// <summary>
		/// Gets the currently active track list (ie the one from which tracks are playing).
		/// </summary>
		/// <returns>The active track list</returns>
		public ViewDetails GetActiveTrackList()
		{
			string can = SettingsManager.CurrentActiveNavigation;
			if (can == "History" || can == "Queue")
				can = SettingsManager.CurrentTrack == null ? "Files" : SettingsManager.CurrentTrack.Source;

			if (can != null && can.StartsWith("Playlist:"))
			{
				PlaylistData p = PlaylistManager.FindPlaylist(can.Split(new[] { ':' }, 2)[1]);
				if (p != null)
					return (ViewDetails)PlaylistTrackLists[p.Name];
			}
			if (can == "YouTube")
				return YouTubeTracks;
			else if (can == "SoundCloud")
				return SoundCloudTracks;
			else if (can == "Radio")
				return RadioTracks;
			else if (can == "Jamendo")
				return JamendoTracks;
			else
				return FileTracks;
		}

		/// <summary>
		/// Dispatches the PropertyChanged event.
		/// </summary>
		/// <param name="name">The name of the property that was changed</param>
		public void OnPropertyChanged(string name)
		{
			if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
		}

		#endregion

		#region Private

		/// <summary>
		/// Play the currently selected track
		/// </summary>
		/// <param name="resume">Resume at the current position if the selected track is the CurrentTrack</param>
		private void Play(bool resume = false)
		{
			Cursor = Cursors.Wait;

			showMediaError = true;

			if (resume && SettingsManager.CurrentTrack != null)
				MediaManager.Play();

			else if (GetCurrentTrackList() != null)
			{
				TrackData track = null;
				ViewDetails vd = GetCurrentTrackList();
				if (vd.SelectedItems.Count > 0)
					track = (TrackData)vd.SelectedItem;
				else if (vd.Items.Count > 0)
					track = (TrackData)vd.Items[0];
				if (track != null)
				{
					if (SettingsManager.CurrentSelectedNavigation == "History")
						SettingsManager.HistoryIndex = SettingsManager.HistoryTracks.IndexOf(track);
					else
						SettingsManager.HistoryIndex = SettingsManager.HistoryTracks.Count-1;

					SettingsManager.CurrentActiveNavigation = SettingsManager.CurrentSelectedNavigation;

					if (SettingsManager.CurrentActiveNavigation.StartsWith("Playlist:"))
						PlaylistManager.CurrentPlaylist = SettingsManager.CurrentActiveNavigation.Split(new[] { ':' }, 2)[1];
					else
						PlaylistManager.CurrentPlaylist = "";

					MediaManager.Stop();
					MediaManager.Load(track);
					MediaManager.Play();
				}
				else
					Cursor = Cursors.Arrow;
			}
			else
				Cursor = Cursors.Arrow;
		}

		/// <summary>
		/// Toggle the play and pause state
		/// </summary>
		private void PlayPause()
		{
			if (SettingsManager.MediaState == Core.MediaState.Playing)
				MediaManager.Pause();
			else
				Play(true);
		}

		/// <summary>
		/// Removes a set of tracks from a given collection of tracks.
		/// </summary>
		/// <param name="tracksToRemove">The tracks to remove</param>
		/// <param name="tracks">The collection from where to remove the tracks</param>
		/// <param name="progressDelta">The amount to increase the scanner progressbar for total scan</param>
		private void RemoveTracks(SortedList<string, TrackData> tracksToRemove, ObservableCollection<TrackData> tracks, double progressDelta = -1)
		{
			//double progress = 0;
			//double trackDelta = 0;
			//if (progressDelta > 0)
			//{
			//    Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			//    {
			//        trackDelta = progressDelta / tracks.Count;
			//        progress = ScanProgressBar.Value;
			//    }));
			//}
			for (int i = 0, j=0; i < tracks.Count; i++,j++)
			{
				if (tracksToRemove.ContainsKey(tracks[i].Path))
				{
					Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
					{
						tracks.RemoveAt(i--);
					}));
				}
				//if (progressDelta > 0)
				//{
				//    if (j % 100 == 0 || (int)progress % 10 == 0)
				//        Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
				//        {
				//            ScanProgressBar.Value += progressDelta;
				//        }));
				//    progress += trackDelta;
				//}
			}
		}

		/// <summary>
		/// Removes a set of tracks from a given collection of tracks.
		/// </summary>
		/// <param name="tracksToRemove">The tracks to remove</param>
		/// <param name="tracks">The collection from where to remove the tracks</param>
		private void RemoveTracks(SortedList<string, TrackData> tracksToRemove, List<TrackData> tracks)
		{
			for (int i = 0; i < tracks.Count; i++)
			{
				if (tracksToRemove.ContainsKey(tracks[i].Path))
				{
					tracks.RemoveAt(i--);
				}
			}
		}

		/// <summary>
		/// Removes tracks with a given filename for a specific collection of tracks
		/// </summary>
		/// <param name="filename">The filename of the tracks to remove</param>
		/// <param name="tracks">The collection of tracks from which to remove</param>
		private void RemoveTrack(String filename, ObservableCollection<TrackData> tracks)
		{
			for (int i = 0; i < tracks.Count; i++)
			{
				if (tracks[i].Path == filename)
				{
					tracks.RemoveAt(i);
					break;
				}
			}
		}

		/// <summary>
		/// Removes tracks with a given filename for a specific list of tracks
		/// </summary>
		/// <param name="filename">The filename of the tracks to remove</param>
		/// <param name="tracks">The list of tracks from which to remove</param>
		private void RemoveTrack(String filename, List<TrackData> tracks)
		{
			for (int i = 0; i < tracks.Count; i++)
			{
				if (tracks[i].Path == filename)
				{
					tracks.RemoveAt(i);
					break;
				}
			}
		}

		/// <summary>
		/// Reapply the sorting on a tracklist
		/// </summary>
		/// <param name="tl">The tracklist to re-sort</param>
		/// <param name="paths">A list of the paths that was changed</param>
		private void ResortTracklist(ViewDetails tl, List<string> paths)
		{
			if (tl == null) return;

			bool resort = false;
			foreach (TrackData t in SettingsManager.FileTracks)
			{
				if (paths.Contains(t.Path))
				{
					resort = true;
					break;
				}
			}
			if (resort)
			{
				if (tl.Items.SortDescriptions.Count > 0)
				{
					SortDescription sd = tl.Items.SortDescriptions[0];
					tl.Items.SortDescriptions.Remove(sd);
					tl.Items.SortDescriptions.Insert(0, sd);
					tl.UpdateScrollPosition();
				}
			}
		}

		/// <summary>
		/// Load a file and add it according to the AddPolicy and play 
		/// it according to the PlayPolicy
		/// </summary>
		/// <param name="path">The filename of the track</param>
		/// <param name="forcePlay">Whether or not to override the PlayPolicy</param>
		private void Open(string path, bool forcePlay = false)
		{
			U.L(LogLevel.Debug, "MAIN", "Opening " + path);
			OpenAddPolicy DoAdd = SettingsManager.OpenAddPolicy;
			OpenPlayPolicy DoPlay = SettingsManager.OpenPlayPolicy;
			TrackData track = null;

			// if not playing and no queue we just play the track
			if (SettingsManager.MediaState != Core.MediaState.Playing && SettingsManager.QueueTracks.Count == 0)
				forcePlay = true;

			string[] param = new string[] { path, forcePlay.ToString() };

			// add track
			if (DoAdd == OpenAddPolicy.Library || DoAdd == OpenAddPolicy.LibraryAndPlaylist)
			{
				// add track to library if needed
				track = MediaManager.GetTrack(path);
				if (track == null && MediaManager.IsSupported(path) &&
					MediaManager.GetType(path) != TrackType.YouTube &&
					MediaManager.GetType(path) != TrackType.SoundCloud &&
					MediaManager.GetType(path) != TrackType.Jamendo)
				{
					U.L(LogLevel.Debug, "MAIN", "Adding to collection");
					FilesystemManager.AddSource(new SourceData()
					{
						Data = path,
						Type = SourceType.File,
						Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/FileAudio.ico",
						Include = true
					}, FinishOpen, param);
					return;
				}
			}
			FinishOpen(param);
		}

		/// <summary>
		/// Called when the Open() function has added the song to the collection.
		/// </summary>
		/// <param name="param">
		/// An array of two strings representing the filename that was added and
		/// a bool value specifying whether we should force playing the track
		/// </param>
		private void FinishOpen(object param)
		{
			string[] p = param as string[];
			if (p.Length != 2)
			{
				U.L(LogLevel.Warning, "MAIN", "Parameters from Open callback does not meet expected format.");
				return;
			}
			string filename = p[0];
			bool forcePlay = p[1].ToLower() == "true";

			OpenAddPolicy DoAdd = SettingsManager.OpenAddPolicy;
			OpenPlayPolicy DoPlay = SettingsManager.OpenPlayPolicy;
			TrackData track = null;

			// add track
			if (DoAdd == OpenAddPolicy.Library || DoAdd == OpenAddPolicy.LibraryAndPlaylist)
			{
				// add track to library if needed
				track = FilesystemManager.GetTrack(filename);

				if (track == null)
					U.L(LogLevel.Warning, "MAIN", "Could not add track to collection: " + filename);

				// add track to current playlist as well
				if (DoAdd == OpenAddPolicy.LibraryAndPlaylist && track != null)
				{
					if (SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
					{
						String playlistName = SettingsManager.CurrentSelectedNavigation.Split(new[] { ':' }, 2)[1];
						U.L(LogLevel.Debug, "MAIN", "Adding to current playlist: " + playlistName);
						List<object> tracks = new List<object>();
						tracks.Add(track);
						PlaylistManager.AddToPlaylist(tracks, playlistName);
					}
				}
			}

			// play track
			if (DoPlay != OpenPlayPolicy.DoNotPlay || forcePlay)
			{
				// create track if it is null
				if (track == null)
				{
					U.L(LogLevel.Debug, "MAIN", "Creating track");
					track = MediaManager.CreateTrack(filename);
					if (track == null)
					{
						U.L(LogLevel.Warning, "MAIN", "Could not create track for: " + filename);
						return;
					}

					TrackType type = MediaManager.GetType(filename);
					if (MediaManager.IsSupported(filename) && (type == TrackType.File || type == TrackType.WebRadio))
						FilesystemManager.UpdateTrack(track);
				}

				if (DoPlay == OpenPlayPolicy.Play || forcePlay)
				{
					U.L(LogLevel.Debug, "MAIN", "Playing track");
					MediaManager.Stop();
					MediaManager.Load(track);
					MediaManager.Play();

					if (DoAdd == OpenAddPolicy.LibraryAndPlaylist && SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
						SettingsManager.CurrentActiveNavigation = SettingsManager.CurrentSelectedNavigation;
					else if (DoAdd == OpenAddPolicy.Library)
						SettingsManager.CurrentActiveNavigation = "Files";
				}

				else if (DoPlay == OpenPlayPolicy.BackOfQueue)
					MediaManager.Queue(new List<TrackData>() { track });

				else if (DoPlay == OpenPlayPolicy.FrontOfQueue)
					MediaManager.Queue(new List<TrackData>() { track }, 0);
			}
		}

		/// <summary>
		/// Creates a playlist track list and navigation items
		/// </summary>
		/// <param name="playlist">The data for the playlist to create</param>
		/// <param name="select">Whether to select the playlist after it has been created</param>
		private void CreatePlaylist(PlaylistData playlist, bool select = true)
		{
			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				if (playlist.ListConfig == null)
				{
					ViewDetailsConfig vdc = SettingsManager.CreateListConfig();
					SettingsManager.InitViewDetailsConfig(vdc);
					vdc.HasNumber = true;
					vdc.Sorts.Add("asc:Title");
					vdc.Sorts.Add("asc:Track");
					vdc.Sorts.Add("asc:Album");
					vdc.Sorts.Add("asc:Artist");

					if (SettingsManager.SearchPolicy == SearchPolicy.Global)
						vdc.Filter = SettingsManager.FileListConfig.Filter;
					else if (SettingsManager.SearchPolicy == SearchPolicy.Partial && SettingsManager.Playlists.Count > 1)
						vdc.Filter = SettingsManager.Playlists[0].ListConfig.Filter;

					playlist.ListConfig = vdc;
				}

				PlaylistTrackLists.Add(playlist.Name, null);
				playlist.Tracks.CollectionChanged +=
					new NotifyCollectionChangedEventHandler(PlaylistTracks_CollectionChanged);

				// create the item in the navigation tree
				TreeViewItem item = new TreeViewItem();
				item.Selected += NavigationPane.Playlist_Selected;
				item.Drop += NavigationPane.Playlist_Drop;
				item.KeyDown += NavigationPlaylist_KeyDown;
				item.Tag = playlist.Name;
				item.Padding = new Thickness(8, 0, 0, 0);
				item.HorizontalAlignment = HorizontalAlignment.Stretch;
				item.HorizontalContentAlignment = HorizontalAlignment.Stretch;

				DockPanel dp = new DockPanel();
				dp.LastChildFill = false;
				dp.HorizontalAlignment = HorizontalAlignment.Stretch;

				Image img = new Image();
				img.Source = Utilities.GetIcoImage("pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/DiscAudio.ico", 16, 16);
				img.Width = 16;
				img.Height = 16;
				img.Style = (Style)TryFindResource("HandHover");
				dp.Children.Add(img);

				EditableTextBlock etb = new EditableTextBlock();
				etb.EnteredEditMode += new EventHandler(EditableTextBlock_EnteredEditMode);
				etb.Text = playlist.Name;
				etb.Margin = new Thickness(5, 0, 5, 0);
				etb.Edited += NavigationPane.Playlist_Edited;
				etb.Canceled += NavigationPane.Playlist_Canceled;
				etb.HandHover = true;
				dp.Children.Add(etb);

				Image simg = new Image();
				simg.Source = Utilities.GetIcoImage("pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/Search.ico", 16, 16);
				simg.Width = 16;
				simg.Height = 16;
				simg.Margin = new Thickness(5, 0, 5, 0);
				simg.Visibility = Visibility.Collapsed;
				DockPanel.SetDock(simg, Dock.Right);
				dp.Children.Add(simg);

				item.Header = dp;
				item.ContextMenuOpening += new ContextMenuEventHandler(NavigationPane.Playlist_ContextMenuOpening);
				item.ContextMenuClosing += new ContextMenuEventHandler(NavigationPane.Playlist_ContextMenuClosing);
				item.ContextMenu = NavigationPane.playlistMenu;
				NavigationPane.Playlists.Items.Insert(NavigationPane.Playlists.Items.Count - 1, item);
				if (select)
					item.Focus();

				// create list context menu items
				MenuItem miAdd = new MenuItem();
				miAdd.Header = playlist.Name;
				miAdd.Click += new RoutedEventHandler(listMenuAddToPlaylist_Click);
				listMenuAddToPlaylist.Visibility = System.Windows.Visibility.Visible;
				listMenuAddToPlaylist.Items.Insert(listMenuAddToPlaylist.Items.Count - 1, miAdd);

				MenuItem miDel = new MenuItem();
				miDel.Header = playlist.Name;
				miDel.Click += new RoutedEventHandler(listMenuRemoveFromPlaylist_Click);
				listMenuRemoveFromPlaylist.Visibility = System.Windows.Visibility.Visible;
				listMenuRemoveFromPlaylist.Items.Add(miDel);

				PlaybackControls.Search.AddPlaylist(playlist);

				NavigationPane.SetSearchIndicator("Playlist:" + playlist.Name, playlist.ListConfig);
			}));
		}

		/// <summary>
		/// Updates the visibility of a given element according to the settings
		/// </summary>
		/// <param name="element">The element to update (menubar or details)</param>
		private void UpdateVisibility(string element)
		{
			switch (element)
			{
				case "menubar":
					bool mv = SettingsManager.MenuBarVisible;
					showMenuMenuBar.IsChecked = mv;
					MenuItemViewMenuBar.IsChecked = mv;
					MenuBar.Visibility = mv ? Visibility.Visible : Visibility.Collapsed;
					break;

				case "details":
					bool dv = SettingsManager.DetailsPaneVisible;
					showMenuDetailsPane.IsChecked = dv;
					MenuItemViewDetailsPane.IsChecked = dv;
					DetailsPaneVisible = dv;
					break;
			}
		}

		/// <summary>
		/// Updates the strings around the GUI, which are set programmatically, according to the current Language
		/// </summary>
		private void RefreshStrings()
		{
			// change column headers of list views
			foreach (ViewDetailsColumn c in SettingsManager.SourceListConfig.Columns)
			{
				if (c.Binding == "HumanType") c.Text = U.T("ColumnType");
				else if (c.Binding == "Data") c.Text = U.T("ColumnLocation");
			}
			foreach (ViewDetailsColumn c in SettingsManager.PluginListConfig.Columns)
			{
				if (c.Binding == "Name")		   c.Text = U.T("ColumnName");
				else if (c.Binding == "Author")	c.Text = U.T("ColumnAuthor");
				else if (c.Binding == "HumanType") c.Text = U.T("ColumnType");
				else if (c.Binding == "Installed") c.Text = U.T("ColumnInstalled");
				else if (c.Binding == "Version")   c.Text = U.T("ColumnVersion");
			}
			foreach (ViewDetailsColumn c in SettingsManager.YouTubeListConfig.Columns)
			{
				if (c.Binding == "Artist")	  c.Text = U.T("ColumnArtist");
				else if (c.Binding == "Title")  c.Text = U.T("ColumnTitle");
				else if (c.Binding == "Path")   c.Text = U.T("ColumnPath");
				else if (c.Binding == "Length") c.Text = U.T("ColumnLength");
				else if (c.Binding == "Views")  c.Text = U.T("ColumnViews");
			}
			foreach (ViewDetailsColumn c in SettingsManager.SoundCloudListConfig.Columns)
			{
				if (c.Binding == "Artist")	  c.Text = U.T("ColumnArtist");
				else if (c.Binding == "Title")  c.Text = U.T("ColumnTitle");
				else if (c.Binding == "Album")  c.Text = U.T("ColumnAlbum");
				else if (c.Binding == "Genre")  c.Text = U.T("ColumnGenre");
				else if (c.Binding == "Length") c.Text = U.T("ColumnLength");
				else if (c.Binding == "Path")   c.Text = U.T("ColumnPath");
			}
			foreach (ViewDetailsColumn c in SettingsManager.JamendoListConfig.Columns)
			{
				if (c.Binding == "Artist") c.Text = U.T("ColumnArtist");
				else if (c.Binding == "Title") c.Text = U.T("ColumnTitle");
				else if (c.Binding == "Album") c.Text = U.T("ColumnAlbum");
				else if (c.Binding == "Genre") c.Text = U.T("ColumnGenre");
				else if (c.Binding == "Length") c.Text = U.T("ColumnLength");
				else if (c.Binding == "Path") c.Text = U.T("ColumnPath");
			}
			foreach (ViewDetailsColumn c in SettingsManager.RadioListConfig.Columns)
			{
				if (c.Binding == "Title")	   c.Text = U.T("ColumnTitle");
				else if (c.Binding == "Genre")  c.Text = U.T("ColumnGenre");
				else if (c.Binding == "URL")	c.Text = U.T("ColumnURL");
				else if (c.Binding == "Path")   c.Text = U.T("ColumnPath");
			}
			foreach (ViewDetailsColumn c in SettingsManager.FileListConfig.Columns)
			{
				if (c.Binding == "Artist")		  c.Text = U.T("ColumnArtist");
				else if (c.Binding == "Title")	  c.Text = U.T("ColumnTitle");
				else if (c.Binding == "Album")	  c.Text = U.T("ColumnAlbum");
				else if (c.Binding == "Genre")	  c.Text = U.T("ColumnGenre");
				else if (c.Binding == "LastPlayed") c.Text = U.T("ColumnLastPlayed");
				else if (c.Binding == "Length")	 c.Text = U.T("ColumnLength");
				else if (c.Binding == "PlayCount")  c.Text = U.T("ColumnPlayCount");
				else if (c.Binding == "Path")	   c.Text = U.T("ColumnPath");
				else if (c.Binding == "Track")	  c.Text = U.T("ColumnTrack");
				else if (c.Binding == "Year")	   c.Text = U.T("ColumnYear");
			}
			foreach (ViewDetailsColumn c in SettingsManager.QueueListConfig.Columns)
			{
				if (c.Binding == "Artist")		  c.Text = U.T("ColumnArtist");
				else if (c.Binding == "Title")	  c.Text = U.T("ColumnTitle");
				else if (c.Binding == "Album")	  c.Text = U.T("ColumnAlbum");
				else if (c.Binding == "Genre")	  c.Text = U.T("ColumnGenre");
				else if (c.Binding == "LastPlayed") c.Text = U.T("ColumnLastPlayed");
				else if (c.Binding == "Length")	 c.Text = U.T("ColumnLength");
				else if (c.Binding == "PlayCount")  c.Text = U.T("ColumnPlayCount");
				else if (c.Binding == "Path")	   c.Text = U.T("ColumnPath");
				else if (c.Binding == "Track")	  c.Text = U.T("ColumnTrack");
				else if (c.Binding == "Year")	   c.Text = U.T("ColumnYear");
			}
			foreach (ViewDetailsColumn c in SettingsManager.HistoryListConfig.Columns)
			{
				if (c.Binding == "Artist")		  c.Text = U.T("ColumnArtist");
				else if (c.Binding == "Title")	  c.Text = U.T("ColumnTitle");
				else if (c.Binding == "Album")	  c.Text = U.T("ColumnAlbum");
				else if (c.Binding == "Genre")	  c.Text = U.T("ColumnGenre");
				else if (c.Binding == "LastPlayed") c.Text = U.T("ColumnPlayed");
				else if (c.Binding == "Length")	 c.Text = U.T("ColumnLength");
				else if (c.Binding == "PlayCount")  c.Text = U.T("ColumnPlayCount");
				else if (c.Binding == "Path")	   c.Text = U.T("ColumnPath");
				else if (c.Binding == "Track")	  c.Text = U.T("ColumnTrack");
				else if (c.Binding == "Year")	   c.Text = U.T("ColumnYear");
			}
			foreach (PlaylistData p in SettingsManager.Playlists)
				foreach (ViewDetailsColumn c in p.ListConfig.Columns)
				{
					if (c.Binding == "Artist")		  c.Text = U.T("ColumnArtist");
					else if (c.Binding == "Title")	  c.Text = U.T("ColumnTitle");
					else if (c.Binding == "Album")	  c.Text = U.T("ColumnAlbum");
					else if (c.Binding == "Genre")	  c.Text = U.T("ColumnGenre");
					else if (c.Binding == "LastPlayed") c.Text = U.T("ColumnPlayed");
					else if (c.Binding == "Length")	 c.Text = U.T("ColumnLength");
					else if (c.Binding == "PlayCount")  c.Text = U.T("ColumnPlayCount");
					else if (c.Binding == "Path")	   c.Text = U.T("ColumnPath");
					else if (c.Binding == "Track")	  c.Text = U.T("ColumnTrack");
					else if (c.Binding == "Year")	   c.Text = U.T("ColumnYear");
				}

			if (YouTubeTracks != null)
				YouTubeTracks.Secondary2Format = U.T("ColumnViews", "Format");
			if (FileTracks != null)
				FileTracks.RefreshView();
			if (YouTubeTracks != null)
				YouTubeTracks.RefreshView();
			if (SoundCloudTracks != null)
				SoundCloudTracks.RefreshView();
			if (RadioTracks != null)
				RadioTracks.RefreshView();
			if (QueueTracks != null)
				QueueTracks.RefreshView();
			if (HistoryTracks != null)
				HistoryTracks.RefreshView();
			foreach (ViewDetails list in PlaylistTrackLists.Values)
				if (list != null)
					list.RefreshView();

			foreach (PluginItem p in SettingsManager.Plugins)
			{
				Plugin plugin;
				if ((plugin = PluginManager.Get(p.ID)) != null)
				{
					plugin.CurrentCulture = SettingsManager.Culture.IetfLanguageTag;
					p.Name = plugin.T("Name");
					p.Description = plugin.T("Description");
				}
			}
			foreach (PluginItem p in PluginManager.VisualizerSelector)
			{
				if (p.ID == null)
				{
					p.Name = U.T("NoVisualizer");
				}
				else
				{
					Plugin plugin;
					if ((plugin = PluginManager.Get(p.ID)) != null)
					{
						plugin.CurrentCulture = SettingsManager.Culture.IetfLanguageTag;
						p.Name = plugin.T("Name");
						p.Description = plugin.T("Description");
					}
				}
			}

			if (VisualizerContainer != null)
				VisualizerContainer.RefreshMeta();

			if (SettingsManager.CurrentSelectedNavigation == "Visualizer")
			{
				InfoPaneTitle.Text = VisualizerContainer.Title;
				InfoPaneSubtitle.Text = VisualizerContainer.Description;
			}

			// change menus
			trayMenuShow.Header = U.T("TrayShow");
			trayMenuPlay.Header = U.T("TrayPlay");
			trayMenuNext.Header = U.T("TrayNext");
			trayMenuPrev.Header = U.T("TrayPrev");
			trayMenuExit.Header = U.T("TrayExit");
			taskbarPlay.Tooltip = U.T("TaskbarPlay");
			taskbarNext.Tooltip = U.T("TaskbarNext");
			taskbarPrev.Tooltip = U.T("TaskbarPrev");
			jumpTaskPlay.Title = U.T("JumpPlay", "Title");
			jumpTaskNext.Title = U.T("JumpNext", "Title");
			jumpTaskPrev.Title = U.T("JumpPrev", "Title");
			jumpTaskPlay.Description = U.T("JumpPlay", "Description");
			jumpTaskNext.Description = U.T("JumpNext", "Description");
			jumpTaskPrev.Description = U.T("JumpPrev", "Description");
			listMenuAddToPlaylist.Header = U.T("MenuAddToPlaylist", "Header");
			listMenuAddToNew.Header = U.T("MenuCreateNew", "Header");
			listMenuDelete.Header = U.T("MenuDelete");
			listMenuCopy.Header = U.T("MenuCopy");
			listMenuMove.Header = U.T("MenuMove");
			listMenuInfo.Header = U.T("MenuInfo");
			listMenuPlay.Header = U.T("MenuPlay");
			listMenuQueue.Header = U.T("MenuQueue");
			listMenuRemove.Header = U.T("MenuRemove");
			listMenuRemoveFromPlaylist.Header = U.T("MenuRemoveFromPlaylist", "Header");
			listMenuWatchOnYouTube.Header = U.T("MenuWatchOnYouTube");
			listMenuListenOnSoundCloud.Header = U.T("MenuListenOnSoundCloud");
			listMenuListenOnJamendo.Header = U.T("MenuListenOnJamendo");
			listMenuOpenFolder.Header = U.T("MenuOpenFolder");
			listMenuShareSong.Header = U.T("MenuShareSong");
			listMenuVisitWebsite.Header = U.T("MenuVisitWebsite", "Header");
			NavigationPane.playlistMenuRemove.Header = U.T("MenuRemovePlaylist");
			NavigationPane.playlistMenuRename.Header = U.T("MenuRenamePlaylist");
			NavigationPane.playlistMenuSave.Header = U.T("MenuSavePlaylist");
			NavigationPane.playlistMenuShare.Header = U.T("MenuSharePlaylist");
			NavigationPane.CreateNewPlaylistETB.Text = U.T("NavigationCreateNew");

			// change tracklist header
			string csn = SettingsManager.CurrentSelectedNavigation;
			switch (csn)
			{
				case "Files":
					InfoPaneTitle.Text = U.T("NavigationFilesTitle");
					InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)LibraryTime));
					InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), SettingsManager.FileTracks.Count);
					break;

				case "Radio":
					InfoPaneTitle.Text = U.T("NavigationRadioTitle");
					InfoPaneDuration.Text = "";
					InfoPaneTracks.Text = String.Format(U.T("HeaderStations"), SettingsManager.RadioTracks.Count);
					break;

				case "Disc":
					InfoPaneTitle.Text = U.T("NavigationDiscTitle");
					MessageBox.Show("TODO");
					break;

				case "Queue":
					InfoPaneTitle.Text = U.T("NavigationQueueTitle");
					InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)QueueTime));
					InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), SettingsManager.QueueTracks.Count);
					break;

				case "History":
					InfoPaneTitle.Text = U.T("NavigationHistoryTitle");
					InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)HistoryTime));
					InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), SettingsManager.HistoryTracks.Count);
					break;

				default:
					if (csn.StartsWith("Playlist:"))
					{
						PlaylistData p = PlaylistManager.FindPlaylist(csn.Split(new[] { ':' }, 2)[1]);
						InfoPaneTitle.Text = U.T("NavigationPlaylistTitle") + " '" + p.Name + "'";
						InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)p.Time)); ;
						InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), p.Tracks.Count);
					}
					break;
			}

			if (ControlPanel != null)
				ControlPanel.RefreshStrings();
			RefreshDetails();
			PlaybackControls.UpdateInfo();
		}

		/// <summary>
		/// Will repaint the glass effect behind the playback pane.
		/// </summary>
		private void RefreshGlassEffect()
		{
			try
			{
				Point lt = MainContainer.TranslatePoint(new Point(0, 0), this);
				var source = PresentationSource.FromVisual(this);
				Matrix transformToDevice = source.CompositionTarget.TransformToDevice;
				Dwm.Glass[this].Enabled = true;
				Thickness foo = new Thickness(1, transformToDevice.Transform(lt).Y, 1, 1);
				Dwm.Glass[this].Margins = foo;
				Background = Brushes.Transparent;
				glassEffect = true;
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not set glass effect: " + e.Message);
				if (System.Windows.Forms.VisualStyles.VisualStyleInformation.DisplayName == "")
					Background = SystemColors.ControlBrush;
				else
					Background = SystemColors.GradientActiveCaptionBrush;
				glassEffect = false;
			}
		}

		/// <summary>
		/// Initializes the GUI
		/// </summary>
		private void InitGUI()
		{
			// create glass effect
			RefreshGlassEffect();

			#region Events handlers

			NavigationPane.GotFocus += new RoutedEventHandler(NavigationPane_GotFocus);

			SettingsManager.QueueTracks.CollectionChanged += new NotifyCollectionChangedEventHandler(QueueTracks_CollectionChanged);
			SettingsManager.HistoryTracks.CollectionChanged += new NotifyCollectionChangedEventHandler(HistoryTracks_CollectionChanged);
			SettingsManager.FileTracks.CollectionChanged += new NotifyCollectionChangedEventHandler(LibraryTracks_CollectionChanged);
			SettingsManager.RadioTracks.CollectionChanged += new NotifyCollectionChangedEventHandler(RadioTracks_CollectionChanged);
			if (SettingsManager.FileListConfig != null)
				SettingsManager.FileListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
			if (SettingsManager.YouTubeListConfig != null)
				SettingsManager.YouTubeListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
			if (SettingsManager.SoundCloudListConfig != null)
				SettingsManager.SoundCloudListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
			if (SettingsManager.RadioListConfig != null)
				SettingsManager.RadioListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
			if (SettingsManager.JamendoListConfig != null)
				SettingsManager.JamendoListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
			if (SettingsManager.QueueListConfig != null)
				SettingsManager.QueueListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
			if (SettingsManager.HistoryListConfig != null)
				SettingsManager.HistoryListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);

			FilesystemManager.SourceModified += new SourceModifiedEventHandler(FilesystemManager_SourceModified);
			FilesystemManager.TrackModified += new PropertyChangedEventHandler(FilesystemManager_TrackModified);
			FilesystemManager.PathModified += new PathModifiedEventHandler(FilesystemManager_PathModified);
			FilesystemManager.PathRenamed += new RenamedEventHandler(FilesystemManager_PathRenamed);
			FilesystemManager.ProgressChanged += new ProgressChangedEventHandler(FilesystemManager_ProgressChanged);
			FilesystemManager.SourceAdded += new SourcesModifiedEventHandler(FilesystemManager_SourceAdded);
			FilesystemManager.SourceRemoved += new SourcesModifiedEventHandler(FilesystemManager_SourceRemoved);

			MediaManager.TrackSwitched += new TrackSwitchedEventHandler(MediaManager_TrackSwitched);
			MediaManager.LoadedTrack += new LoadedTrackDelegate(MediaManager_LoadedTrack);
			MediaManager.Started += new EventHandler(MediaManager_Started);
			MediaManager.SearchMatch = TrackList_SearchMatch;

			UpgradeManager.Checked += new EventHandler(UpgradeManager_Checked);
			UpgradeManager.ErrorOccured += new Core.ErrorEventHandler(UpgradeManager_ErrorOccured);
			UpgradeManager.ProgressChanged += new ProgressChangedEventHandler(UpgradeManager_ProgressChanged);
			UpgradeManager.Upgraded += new EventHandler(UpgradeManager_Upgraded);
			UpgradeManager.UpgradeFound += new EventHandler(UpgradeManager_UpgradeFound);

			PluginManager.RefreshVisualizerSelector += new EventHandler(PluginManager_RefreshVisualizerSelector);
			PluginManager.Installed += new EventHandler<PluginEventArgs>(PluginManager_Installed);
			PluginManager.Uninstalled += new EventHandler<PluginEventArgs>(PluginManager_Uninstalled);
			PluginManager.Initialize();

			SettingsManager.PropertyChanged += new PropertyChangedWithValuesEventHandler(SettingsManager_PropertyChanged);
			ServiceManager.ModifyTracks += new EventHandler<ModifiedEventArgs>(ServiceManager_ModifyTracks);

			NavigationPane.CreateNewPlaylistETB.EnteredEditMode += new EventHandler(EditableTextBlock_EnteredEditMode);
			SystemEvents.SessionSwitch += new SessionSwitchEventHandler(SystemEvents_SessionSwitch);

			resortDelay.Interval = new TimeSpan(0, 0, 0, 0, 500);
			resortDelay.Tick += new EventHandler(ResortDelay_Tick);

			sourceModifiedDelay.Tick += new EventHandler(SourceModifiedDelay_Tick);
			sourceModifiedDelay.Interval = new TimeSpan(0, 0, 0, 1, 500);

			#endregion

			#region Tray icon

			// create system tray icon
			trayIcon = (TaskbarIcon)FindResource("NotifyIcon");
			trayIcon.TrayToolTip = new TrayToolTip(this);
			trayIcon.TrayLeftMouseUp += TaskbarClicked;
			trayMenu = new ContextMenu();
			trayMenuShow = new MenuItem();
			trayMenuExit = new MenuItem();
			trayMenuPlay = new MenuItem();
			trayMenuNext = new MenuItem();
			trayMenuPrev = new MenuItem();
			trayMenuShow.Header = U.T("TrayShow");
			trayMenuExit.Header = U.T("TrayExit");
			trayMenuPlay.Header = U.T("TrayPlay");
			trayMenuNext.Header = U.T("TrayNext");
			trayMenuPrev.Header = U.T("TrayPrev");
			trayMenuShow.Click += TrayShow_Clicked;
			trayMenuExit.Click += TrayExit_Clicked;
			trayMenuPlay.Click += TrayPlayPause_Clicked;
			trayMenuNext.Click += TrayNext_Clicked;
			trayMenuPrev.Click += TrayPrevious_Clicked;
			trayMenu.Items.Add(trayMenuShow);
			trayMenu.Items.Add(new Separator());
			trayMenu.Items.Add(trayMenuPlay);
			trayMenu.Items.Add(trayMenuNext);
			trayMenu.Items.Add(trayMenuPrev);
			trayMenu.Items.Add(new Separator());
			trayMenu.Items.Add(trayMenuExit);
			trayIcon.ContextMenu = trayMenu;

			#endregion

			#region Thumbnail buttons

			//// create thumbnail buttons
			taskbarPrev = new ThumbnailToolbarButton(Properties.Resources.Previous, U.T("TaskbarPrev"));
			taskbarPrev.Enabled = true;
			taskbarPrev.Click += TaskbarPrevious_Clicked;
			taskbarPlay = new ThumbnailToolbarButton(Properties.Resources.Play, U.T("TaskbarPlay"));
			taskbarPlay.Enabled = true;
			taskbarPlay.Click += TaskbarPlayPause_Clicked;
			taskbarNext = new ThumbnailToolbarButton(Properties.Resources.Next, U.T("TaskbarNext"));
			taskbarNext.Enabled = true;
			taskbarNext.Click += TaskbarNext_Clicked;
			TaskbarManager.Instance.ThumbnailToolbars.AddButtons(
				new WindowInteropHelper(this).Handle,
				new ThumbnailToolbarButton[] { taskbarPrev, taskbarPlay, taskbarNext }
			);

			#endregion

			#region Jump lists

			jumpTaskPlay = new JumpTask()
			{
				Title = U.T("JumpPlay", "Title"),
				Arguments = "/play",
				Description = U.T("JumpPlay", "Description"),
				IconResourceIndex = -1,
				ApplicationPath = Assembly.GetEntryAssembly().CodeBase,
			};

			jumpTaskNext = new JumpTask()
			{
				Title = U.T("JumpNext", "Title"),
				Arguments = "/next",
				Description = U.T("JumpNext", "Description"),
				IconResourceIndex = -1,
				ApplicationPath = Assembly.GetEntryAssembly().CodeBase,
			};

			jumpTaskPrev = new JumpTask()
			{
				Title = U.T("JumpPrev", "Title"),
				Arguments = "/previous",
				Description = U.T("JumpPrev", "Description"),
				IconResourceIndex = -1,
				ApplicationPath = Assembly.GetEntryAssembly().CodeBase,
			};

			jumpList = new System.Windows.Shell.JumpList();
			jumpList.JumpItems.Add(jumpTaskPlay);
			jumpList.JumpItems.Add(jumpTaskNext);
			jumpList.JumpItems.Add(jumpTaskPrev);
			jumpList.ShowRecentCategory = true;
			jumpList.ShowFrequentCategory = true;
			System.Windows.Shell.JumpList.SetJumpList(Application.Current, jumpList);

			#endregion

			#region Style

			Utilities.DefaultAlbumArt = "/Platform/Windows 7/GUI/Images/AlbumArt/Default.jpg";

			// rough detection of aero vs classic
			// you'll se a lot more of these around the code
			if (System.Windows.Forms.VisualStyles.VisualStyleInformation.DisplayName == "")
			{
				// applying classic theme
				SolidColorBrush scb = (SolidColorBrush)FindResource("DetailsPaneKey");
				scb.Color = SystemColors.ControlTextColor;
				scb = (SolidColorBrush)FindResource("DetailsPaneValue");
				scb.Color = SystemColors.ControlTextColor;
				scb = (SolidColorBrush)FindResource("InfoPaneTitle");
				scb.Color = SystemColors.ControlTextColor;
				scb = (SolidColorBrush)FindResource("InfoPaneText");
				scb.Color = SystemColors.ControlTextColor;

				MainFrame.BorderBrush = SystemColors.ControlBrush;
				MainFrame.Background = SystemColors.ControlBrush;
				MainContainer.Background = SystemColors.ControlBrush;
				InfoPane.Background = SystemColors.WindowBrush;
				VerticalSplitter.Background = SystemColors.ControlBrush;

				TopToolbar.Style = null;
				DetailsPane.Style = (Style)FindResource("ClassicDetailsPaneStyle");

				OuterBottomRight.BorderBrush = SystemColors.ControlLightLightBrush;
				OuterTopLeft.BorderBrush = SystemColors.ControlDarkBrush;
				InnerBottomRight.BorderBrush = SystemColors.ControlDarkBrush;
				InnerTopLeft.BorderBrush = SystemColors.ControlLightLightBrush;

				ControlPanel.AboutTitle.Style = (Style)FindResource("ClassicControlPanelTitleStyle");
				ControlPanel.ShortcutTitle.Style = (Style)FindResource("ClassicControlPanelTitleStyle");
				ControlPanel.GeneralTitle.Style = (Style)FindResource("ClassicControlPanelTitleStyle");
				Utilities.DefaultAlbumArt = "/Platform/Windows 7/GUI/Images/AlbumArt/Classic.jpg";
			}

			#endregion

			VisualizerList.ItemsSource = PluginManager.VisualizerSelector;

			LibraryTime = 0;
			QueueTime = 0;
			HistoryTime = 0;
			if (SettingsManager.FileTracks != null)
				foreach (TrackData track in SettingsManager.FileTracks)
					LibraryTime += track.Length;
			if (SettingsManager.QueueTracks != null)
				foreach (TrackData track in SettingsManager.QueueTracks)
					QueueTime += track.Length;
			if (SettingsManager.HistoryTracks != null)
				foreach (TrackData track in SettingsManager.HistoryTracks)
					HistoryTime += track.Length;

			NavigationColumn.Width = new GridLength(SettingsManager.NavigationPaneWidth);
			double h = SettingsManager.DetailsPaneHeight;
			DetailsRow.Height = new GridLength(h);

			UpdateVisibility("menubar");
			UpdateVisibility("details");

			RefreshStrings();
			U.ListenForShortcut = true;

			FilesystemManager.AddSystemFolders(true);

			#region Create playlists

			foreach (PlaylistData playlist in SettingsManager.Playlists)
				CreatePlaylist(playlist, false);

			PlaylistManager.PlaylistModified += new ModifiedEventHandler(PlaylistManager_PlaylistModified);
			PlaylistManager.PlaylistRenamed += new RenamedEventHandler(PlaylistManager_PlaylistRenamed);

			if (SettingsManager.CurrentSelectedNavigation == "YouTube")
				NavigationPane.Youtube.Focus();
			else if (SettingsManager.CurrentSelectedNavigation == "SoundCloud")
				NavigationPane.SoundCloud.Focus();
			else if (SettingsManager.CurrentSelectedNavigation == "Radio")
				NavigationPane.Radio.Focus();
			else if (SettingsManager.CurrentSelectedNavigation == "Jamendo")
				NavigationPane.Jamendo.Focus();
			else if (SettingsManager.CurrentSelectedNavigation == "Queue")
				NavigationPane.Queue.Focus();
			else if (SettingsManager.CurrentSelectedNavigation == "History")
				NavigationPane.History.Focus();
			else if (SettingsManager.CurrentSelectedNavigation == "Video")
				NavigationPane.Video.Focus();
			else if (SettingsManager.CurrentSelectedNavigation == "Visualizer")
				NavigationPane.Visualizer.Focus();
			else if (!SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
				NavigationPane.Files.Focus();
			else
			{
				string name = SettingsManager.CurrentSelectedNavigation.Split(new[] { ':' }, 2)[1];
				foreach (TreeViewItem tvi in NavigationPane.Playlists.Items)
				{
					if ((string)tvi.Tag == name)
					{
						tvi.Focus();
						break;
					}
				}
			}

			#endregion

			#region Load track lists

			U.L(LogLevel.Debug, "main", "Initialize track lists");
			switch (SettingsManager.CurrentActiveNavigation)
			{
				case "Queue":
					if (FileTracks == null)
						FileTracks = InitTrackList(new ViewDetails(), SettingsManager.FileListConfig, SettingsManager.FileTracks);
					break;

				case "Radio":
					if (RadioTracks == null)
						RadioTracks = InitTrackList(new ViewDetails(), SettingsManager.RadioListConfig, SettingsManager.RadioTracks);
					break;

				case "YouTube":
					if (YouTubeTracks == null)
						YouTubeTracks = (YouTubeTracks)InitTrackList(new YouTubeTracks(), SettingsManager.YouTubeListConfig);
					break;

				case "SoundCloud":
					if (SoundCloudTracks == null)
						SoundCloudTracks = (SoundCloudTracks)InitTrackList(new SoundCloudTracks(), SettingsManager.SoundCloudListConfig);
					break;

				case "Jamendo":
					if (JamendoTracks == null)
						JamendoTracks = (JamendoTracks)InitTrackList(new JamendoTracks(), SettingsManager.JamendoListConfig);
					break;

				default:
					if (SettingsManager.CurrentActiveNavigation.StartsWith("Playlist:"))
					{
						string pname = SettingsManager.CurrentActiveNavigation.Split(new[] { ':' }, 2)[1];
						PlaylistData p = PlaylistManager.FindPlaylist(pname);
						if (p != null)
						{
							ViewDetails vd = (ViewDetails)PlaylistTrackLists[pname];
							if (vd == null)
								vd = InitTrackList(new ViewDetails(), p.ListConfig, p.Tracks);
						}
					}
					break;
			}
			if (FileTracks == null)
				FileTracks = InitTrackList(new ViewDetails(), SettingsManager.FileListConfig, SettingsManager.FileTracks);

			#endregion

			#region File association prompt

			if (SettingsManager.FirstRun)
			{
				Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
				{
					// Show welcome dialog
					TaskDialogResult tdr = Welcome.Show(new WindowInteropHelper(this).Handle);
					Associations a = new Associations();

					ProcessStartInfo assProcInfo = new ProcessStartInfo();
					assProcInfo.FileName = U.FullPath;
					assProcInfo.Verb = "runas";
					string assProcArgs = "--associate {0}";

					try
					{
						switch (tdr)
						{
							case TaskDialogResult.Yes:
								assProcInfo.Arguments = String.Format(assProcArgs,
									string.Join(",", a.FullFileList.ToArray()));
								Process.Start(assProcInfo);
								break;

							case TaskDialogResult.CustomButtonClicked:
								a.ShowDialog();
								assProcInfo.Arguments = String.Format(assProcArgs,
									string.Join(",", a.FileList.ToArray()));
								Process.Start(assProcInfo);
								break;

							case TaskDialogResult.Cancel:
								break;
						}
					}
					catch (Exception e)
					{
						U.L(LogLevel.Warning, "MAIN", "Could not set associations: " + e.Message);
					}
				}));

				SettingsManager.FirstRun = false;
			}

			#endregion

			#region Commented
			//System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal);
			#endregion
		}

		/// <summary>
		/// Initializes a track list control.
		/// </summary>
		/// <param name="list">The track list to initialize</param>
		/// <param name="config">The configuration for the list</param>
		/// <param name="tracks">The track collection</param>
		/// <returns>The initialized list</returns>
		private ViewDetails InitTrackList(ViewDetails list, ViewDetailsConfig config, ObservableCollection<TrackData> tracks = null)
		{
			list.Config = config;
			list.ContextMenu = listMenu;
			list.Visibility = Visibility.Collapsed;
			list.BorderThickness = new Thickness(0);
			list.Config.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
			list.MouseDoubleClick += new MouseButtonEventHandler(TrackList_MouseDoubleClick);
			list.SelectionChanged += new SelectionChangedEventHandler(TrackList_SelectionChanged);
			list.ContextMenuOpening += new ContextMenuEventHandler(TrackList_ContextMenuOpening);
			list.GotFocus += new RoutedEventHandler(TrackList_GotFocus);
			list.MoveItem += new MoveItemEventHandler(TrackList_MoveItem);
			list.SelectIndices(config.SelectedIndices);

			if (config == SettingsManager.SoundCloudListConfig)
			{
				SoundCloudTracks sct = list as SoundCloudTracks;
				if (sct != null)
					sct.Search(config.Filter);
				list.Primary = "Title";
				list.Secondary1 = "Artist";
				list.Secondary2 = "Genre";
				list.Tertiary1 = "Length";
				list.Tertiary2 = "PlayCount";
				list.Tertiary1Converter = "Duration";
			}
			else if (config == SettingsManager.YouTubeListConfig)
			{
				YouTubeTracks ytt = list as YouTubeTracks;
				if (ytt != null)
					ytt.Search(config.Filter);
				list.Primary = "Title";
				list.Secondary1 = "Artist";
				list.Secondary2 = "Views";
				list.Tertiary1 = "Length";
				list.Tertiary2 = "Path";
				list.Secondary2Format = U.T("ColumnViews", "Format");
				list.Secondary2Converter = "Number";
				list.Tertiary1Converter = "Duration";
			}
			else if (config == SettingsManager.JamendoListConfig)
			{
				JamendoTracks jt = list as JamendoTracks;
				if (jt != null)
					jt.Search(config.Filter);
				list.Primary = "Title";
				list.Secondary1 = "Artist";
				list.Secondary2 = "Album";
				list.Tertiary1 = "Genre";
				list.Tertiary2 = "Length";
				list.Tertiary2Converter = "Duration";
			}
			else
			{
				if (config == SettingsManager.RadioListConfig)
				{
					list.Primary = "Title";
					list.Secondary1 = "Genre";
					list.Secondary2 = "URL";
					list.Tertiary1 = "PlayCount";
					list.Tertiary2 = "LastPlayed";
					list.Tertiary2Converter = "DateTime";
				}
				else
				{
					list.Primary = "Title";
					list.Secondary1 = "Artist";
					list.Secondary2 = "Album";
					list.Tertiary1 = "Genre";
					list.Tertiary2 = "Length";
					list.Tertiary2Converter = "Duration";
				}

				list.FilterMatch = TrackList_SearchMatch;
				list.ItemsSource = tracks;
				list.FilesDropped += new FileDropEventHandler(TrackList_FilesDropped);
				list.PropertyChanged += new PropertyChangedEventHandler(TrackList_PropertyChanged);
				if (tracks != null)
					tracks.CollectionChanged +=
						new NotifyCollectionChangedEventHandler(list.ItemsSource_CollectionChanged);
			}

			Grid.SetRow(list, 1);
			ContentContainer.Children.Add(list);

			return list;
		}

		/// <summary>
		/// Initializes the video control.
		/// </summary>
		private void InitVideo()
		{
			if (VideoContainer != null) return;

			VideoContainer = new Video();
			Grid.SetRow(VideoContainer, 1);
			ContentContainer.Children.Add(VideoContainer);

			// WebBrowser will not load if it's not visible
			try
			{
			    // warning for fugliness
				VideoContainer.Children.Remove(VideoContainer.Browser);
				ContentContainer.Children.Add(VideoContainer.Browser);
				ContentContainer.Children.Remove(VideoContainer.Browser);
				VideoContainer.Browser.Visibility = System.Windows.Visibility.Collapsed;

			}
			catch (Exception exc)
			{
			    U.L(LogLevel.Error, "MAIN", "There was a problem moving browsers into view for loading");
			    U.L(LogLevel.Error, "MAIN", exc.Message);
			}

			YouTubePlayerInterface ypi = new YouTubePlayerInterface();
			ypi.ErrorOccured += new Core.ErrorEventHandler(YouTube_ErrorOccured);
			ypi.NoFlashDetected += new EventHandler(YouTube_NoFlashDetected);
			ypi.PlayerReady += new EventHandler(YouTube_PlayerReady);

			VideoContainer.Browser.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
			VideoContainer.Browser.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
			VideoContainer.Browser.Width = double.NaN;
			VideoContainer.Browser.Height = double.NaN;
			VideoContainer.Browser.ObjectForScripting = ypi;
			VideoContainer.NoVideoMessage.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
			VideoContainer.NoVideoMessage.Visibility = System.Windows.Visibility.Visible;

			if (YouTubeManager.IsYouTube(SettingsManager.CurrentTrack))
			{
				VideoContainer.BrowserVisibility = Visibility.Visible;
			}
		}

		/// <summary>
		/// Initializes the control panel control.
		/// </summary>
		private void InitControlPanel()
		{
			if (ControlPanel != null) return;

			Cursor = Cursors.Wait;
			ControlPanel = new ControlPanel();

			ControlPanel.ParentWindow = this;
			ControlPanel.Services.BrowserBorder.Child = null;
			ContentContainer.Children.Add(ControlPanel.Services.Browser);
			ContentContainer.Children.Remove(ControlPanel.Services.Browser);
			ControlPanel.Services.BrowserBorder.Child = ControlPanel.Services.Browser;

			ControlPanel.InitShortcuts();

			ControlPanel.Sources.AddFileClick += new RoutedEventHandler(AddFile_Click);
			ControlPanel.Sources.AddFolderClick += new RoutedEventHandler(AddFolder_Click);
			ControlPanel.Sources.IgnoreFileClick += new RoutedEventHandler(IgnoreFile_Click);
			ControlPanel.Sources.IgnoreFolderClick += new RoutedEventHandler(IgnoreFolder_Click);

			Cursor = Cursors.Arrow;
		}

		/// <summary>
		/// Updates the details pane according to the currently selected navigation or tracks.
		/// </summary>
		private void RefreshDetails()
		{
			ViewDetails vd = GetCurrentTrackList();

			#region Navigation
			if (NavigationPane.ItemIsFocused)
			{
				DetailsPane.Clear();
				switch (SettingsManager.CurrentSelectedNavigation)
				{
					case "Now playing":
						if (SettingsManager.CurrentTrack != null)
						{
							ShowTrackDetails(SettingsManager.CurrentTrack);
							DetailsPane.Description = SettingsManager.CurrentTrack.Title;
						}
						DetailsPane.Images = Utilities.GetIcoImages("Library");
						DetailsPane.Title = U.T("NavigationNowPlaying");
						break;

					case "Video":
						DetailsPane.Images = Utilities.GetIcoImages("Video");
						DetailsPane.Title = U.T("NavigationVideo");
						break;

					case "Visualizer":
						DetailsPane.Images = Utilities.GetIcoImages("Visualizer");
						DetailsPane.Title = U.T("NavigationVisualizer");
						Plugins.Visualizer vis = PluginManager.Get(SettingsManager.CurrentVisualizer) as Plugins.Visualizer;
						if (vis != null)
						{
							DetailsPane.Description = vis.T("Name");
							DetailsPane.AddField(U.T("ColumnDescription"), vis.T("Description"));
							DetailsPane.AddField(U.T("ColumnAuthor"), vis.Author);
							DetailsPane.AddField(U.T("ColumnVersion"), vis.Version.ToString());
						}
						else
							DetailsPane.Description = U.T("NoVisualizer");
						break;

					case "Music":
						DetailsPane.Images = Utilities.GetIcoImages("Library");
						DetailsPane.Title = U.T("NavigationMusic");
						DetailsPane.Title = U.T("NavigationMusicDescription");
						DetailsPane.AddField(U.T("Tracks"), SettingsManager.FileTracks.Count);
						DetailsPane.AddField(U.T("Stations"), SettingsManager.RadioTracks.Count);
						break;

					case "Files":
						DetailsPane.Images = Utilities.GetIcoImages("FileAudio");
						DetailsPane.Title = U.T("NavigationFilesTitle");
						DetailsPane.Description = String.Format(U.T("HeaderTracks"), SettingsManager.FileTracks.Count);
						ShowTrackCollectionDetails(SettingsManager.FileTracks);
						break;

					case "Radio":
						DetailsPane.Images = Utilities.GetIcoImages("Radio");
						DetailsPane.Title = U.T("NavigationRadioTitle");
						DetailsPane.Description = String.Format(U.T("HeaderStations"), SettingsManager.RadioTracks.Count);
						break;

					case "YouTube":
						DetailsPane.Images = Utilities.GetIcoImages("YouTube");
						DetailsPane.Title = U.T("NavigationYouTubeTitle");
						DetailsPane.Description = U.T("NavigationYouTubeDescription");
						break;

					case "SoundCloud":
						DetailsPane.Images = Utilities.GetIcoImages("SoundCloud");
						DetailsPane.Title = U.T("NavigationSoundCloudTitle");
						DetailsPane.Description = U.T("NavigationSoundCloudDescription");
						break;

					case "Jamendo":
						DetailsPane.Image = Utilities.GetIcoImage("Jamendo", 64, 64);
						DetailsPane.Title = U.T("NavigationJamendoTitle");
						DetailsPane.Description = U.T("NavigationJamendoDescription");
						break;

					case "Queue":
						DetailsPane.Images = Utilities.GetIcoImages("Queue");
						DetailsPane.Title = U.T("NavigationQueueTitle");
						DetailsPane.Description = String.Format(U.T("HeaderTracks"), SettingsManager.QueueTracks.Count);
						ShowTrackCollectionDetails(SettingsManager.QueueTracks);
						break;

					case "History":
						DetailsPane.Images = Utilities.GetIcoImages("Clock");
						DetailsPane.Title = U.T("NavigationHistoryTitle");
						DetailsPane.Description = String.Format(U.T("HeaderTracks"), SettingsManager.HistoryTracks.Count);
						ShowTrackCollectionDetails(SettingsManager.HistoryTracks);
						break;

					case "Playlists":
						DetailsPane.Images = Utilities.GetIcoImages("DiscAudio");
						DetailsPane.Title = U.T("NavigationPlaylists");
						DetailsPane.Description = String.Format(U.T("HeaderPlaylists"), SettingsManager.Playlists.Count);
						double avgTracks = 0;
						foreach (PlaylistData p in SettingsManager.Playlists)
							avgTracks += p.Tracks.Count;
						avgTracks /= SettingsManager.Playlists.Count;
						DetailsPane.AddField(U.T("AverageTracks"), String.Format("{0:0.00}", avgTracks));
						break;

					// playlist
					default:
						if (SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
						{
							String playlistName = SettingsManager.CurrentSelectedNavigation.Split(new[] { ':' }, 2)[1];
							PlaylistData pl = PlaylistManager.FindPlaylist(playlistName);
							if (pl != null)
							{
								DetailsPane.Images = Utilities.GetIcoImages("DiscAudio");
								DetailsPane.Title = pl.Name;
								DetailsPane.Description = String.Format(U.T("HeaderTracks"), pl.Tracks.Count);
								ShowTrackCollectionDetails(pl.Tracks);
							}
						}
						break;
				}
			}
			#endregion

			#region Track
			else if (vd != null && vd.SelectedItemIsFocused)
			{
				#region Single track
				if (vd.SelectedItems.Count == 1)
				{
					TrackData track = vd.SelectedItem as TrackData;
					currentlySelectedTrack = track;
					DetailsPane.Clear();
					ShowTrackDetails(track);

					// set image in a background thread
					ThreadStart DetailsImageThread = delegate()
					{
						Dispatcher.Invoke(DispatcherPriority.SystemIdle, new Action(delegate()
						{
							DetailsPane.Image = Utilities.GetImageTag(track);
						}));
					};
					Thread di_thread = new Thread(DetailsImageThread);
					di_thread.Name = "Details Image";
					di_thread.IsBackground = true;
					di_thread.Priority = ThreadPriority.Lowest;
					di_thread.Start();
				}
				#endregion

				#region Multiple tracks
				else
				{
					DetailsPane.Clear();
					DetailsPane.Image = new BitmapImage(new Uri(Utilities.DefaultAlbumArt, UriKind.RelativeOrAbsolute));
					DetailsPane.Title = String.Format(U.T("MultipleSelectedTracks"), vd.SelectedItems.Count);

					double length = 0;
					long size = 0;
					uint playcount = 0;
					ulong views = 0;
					TrackData t = vd.SelectedItems[0] as TrackData;
					string artist = t.Artist;
					string album = t.Album;
					string genre = t.Genre;
					string type = MediaManager.HumanTrackType(t, true);
					bool aYouTube = YouTubeManager.IsYouTube(t);
					bool allYouTube = aYouTube;

					foreach (TrackData track in vd.SelectedItems)
					{
						if (track.Artist != artist) artist = null;
						if (track.Album != album) album = null;
						if (track.Genre != genre) genre = null;
						if (MediaManager.HumanTrackType(track, true) != type) type = null;
						length += track.Length;
						playcount += track.PlayCount;
						views += (ulong)track.Views;

						if (MediaManager.GetType(track) == TrackType.File)
						{
							FileInfo fi = new FileInfo(track.Path);
							size += fi.Length;
							allYouTube = false;
						}
						else
							aYouTube = true;
					}

					DetailsPane.AddField(U.T("ColumnPlayCount"), playcount.ToString());
					DetailsPane.AddField(U.T("ColumnLength"), U.TimeSpanToString(new TimeSpan(0, 0, 0, (int)length)));

					if (!allYouTube)
						DetailsPane.AddField(U.T("ColumnSize"), U.HumanSize(size));

					if (artist != null)
						DetailsPane.AddField(U.T("ColumnArtist"), artist);
					if (album != null)
						DetailsPane.AddField(U.T("ColumnAlbum"), album);
					if (genre != null)
						DetailsPane.AddField(U.T("ColumnGenre"), genre);

					if (aYouTube)
						DetailsPane.AddField(U.T("ColumnViews"), U.T(views));

					if (type != null)
						DetailsPane.Description = type;
					else if (!aYouTube)
						DetailsPane.Description = U.T("FileTypeLocal", "Plural");

				}
				#endregion
			}
			#endregion

			else
			{
			}
		}

		/// <summary>
		/// Shows the details of a set of tracks in the details pane.
		/// </summary>
		/// <param name="tracks">The track collection</param>
		private void ShowTrackCollectionDetails(ObservableCollection<TrackData> tracks)
		{
			if (detailsThread != null)
			{
				detailsThread.Abort();
				detailsThread = null;
			}
			ThreadStart DetailsAggregationThread = delegate()
			{
				double avgLength = 0;
				double totLength = 0;
				double avgSize = 0;
				double totSize = 0;
				int numSize = 0;
				for (int i=0; i < tracks.Count; i++)
				{
					TrackData t = tracks[i];
					totLength += t.Length;
					try
					{
						if (MediaManager.GetType(t) == TrackType.File)
						{
							FileInfo fi = new FileInfo(t.Path);
							totSize += fi.Length;
							numSize++;
						}
					}
					catch { }
				}
				if (tracks.Count > 0)
					avgLength = totLength / tracks.Count;
				if (numSize > 0)
					avgSize = totSize / numSize;

				Dispatcher.Invoke(DispatcherPriority.SystemIdle, new Action(delegate()
				{
					DetailsPane.ClearFields();
					DetailsPane.AddField(U.T("AverageLength"), U.TimeSpanToString(new TimeSpan(0, 0, (int)avgLength)));
					DetailsPane.AddField(U.T("AverageSize"), U.HumanSize((long)avgSize));
					DetailsPane.AddField(U.T("TotalLength"), U.TimeSpanToString(new TimeSpan(0, 0, (int)totLength)));
					DetailsPane.AddField(U.T("TotalSize"), U.HumanSize((long)totSize));
				}));
			};
			detailsThread = new Thread(DetailsAggregationThread);
			detailsThread.Name = "Details aggregation";
			detailsThread.IsBackground = true;
			detailsThread.Priority = ThreadPriority.Lowest;
			detailsThread.Start();
		}

		/// <summary>
		/// Shows the details of a given track in the details pane.
		/// </summary>
		/// <param name="track">The track</param>
		private void ShowTrackDetails(TrackData track)
		{
			if (track == null) return;
			DetailsPane.Description = MediaManager.HumanTrackType(track);
			switch (MediaManager.GetType(track))
			{
				case TrackType.File:
					DetailsPane.Title = Path.GetFileName(track.Path);
					DetailsPane.AddField(U.T("ColumnArtist"), track.Artist, true);
					DetailsPane.AddField(U.T("ColumnTitle"), track.Title, true);
					DetailsPane.AddField(U.T("ColumnAlbum"), track.Album, true);
					DetailsPane.AddTextField(U.T("ColumnGenre"), track, "Genre", true); // TODO: this binding doesn't work!
					DetailsPane.AddTextField(U.T("ColumnLength"), track, "Length", false, new DurationConverter());
					if (File.Exists(track.Path))
					{
						FileInfo fi = new FileInfo(track.Path);
						DetailsPane.AddField(U.T("ColumnSize"), U.HumanSize(fi.Length));
					}
					DetailsPane.AddField(U.T("ColumnBitrate"), String.Format(U.T("KilobitsPerSecond"), track.Bitrate));
					DetailsPane.AddField(U.T("ColumnYear"), track.Year.ToString(), true);
					DetailsPane.AddField(U.T("ColumnPlayCount"), track.PlayCount.ToString());
					DetailsPane.AddTextField(U.T("ColumnLastPlayed"), track, "LastPlayed", false, new DateTimeConverter());
					DetailsPane.AddField(U.T("ColumnChannels"), track.Channels.ToString());
					DetailsPane.AddField(U.T("ColumnSamplingRate"), String.Format(U.T("KiloHertz"), Math.Round(track.SampleRate / 1000.0, 1)));
					DetailsPane.AddField(U.T("ColumnCodecs"), track.Codecs);
					DetailsPane.AddField(U.T("ColumnPath"), track.Path);
					break;

				case TrackType.WebRadio:
					DetailsPane.Title = track.Title;
					DetailsPane.AddField(U.T("ColumnGenre"), track.Genre);
					DetailsPane.AddField(U.T("ColumnURL"), track.URL);
					break;

				case TrackType.YouTube:
					DetailsPane.Title = track.Title;
					DetailsPane.AddField(U.T("ColumnArtist"), track.Artist);
					DetailsPane.AddField(U.T("ColumnViews"), track.Views);
					DetailsPane.AddTextField(U.T("ColumnLength"), track, "Length", false, new DurationConverter());
					DetailsPane.AddField(U.T("ColumnPath"), track.URL);
					break;

				case TrackType.SoundCloud:
					DetailsPane.Title = track.Title;
					DetailsPane.AddField(U.T("ColumnArtist"), track.Artist);
					DetailsPane.AddTextField(U.T("ColumnLength"), track, "Length", false, new DurationConverter());
					DetailsPane.AddField(U.T("ColumnGenre"), track.Genre);
					DetailsPane.AddField(U.T("ColumnYear"), track.Year.ToString());
					DetailsPane.AddField(U.T("ColumnPath"), track.URL);
					break;

				case TrackType.Jamendo:
					DetailsPane.Title = track.Title;
					DetailsPane.AddField(U.T("ColumnArtist"), track.Artist);
					DetailsPane.AddField(U.T("ColumnAlbum"), track.Album);
					DetailsPane.AddTextField(U.T("ColumnLength"), track, "Length", false, new DurationConverter());
					DetailsPane.AddField(U.T("ColumnGenre"), track.Genre);
					DetailsPane.AddField(U.T("ColumnPath"), track.URL);
					break;
			}
		}

		/// <summary>
		/// Updates the view and shows a new navigation according to
		/// SettingsManager.CurrentSelectedNavigation.
		/// Assumes that the navigation pane already is up-to-date.
		/// </summary>
		public void SwitchNavigation()
		{
			FrameworkElement content = null;
			FrameworkElement info = null;
			ViewDetailsConfig vdc = null;
			string header, subtitle, tracks, time;
			header = subtitle = tracks = time = null;

			RefreshDetails();

			switch (SettingsManager.CurrentSelectedNavigation)
			{
				case "Now playing":
				case "Music":
				case "Playlists":
					return;

				case "Video":
					if (VideoContainer == null)
						InitVideo();
					content = VideoContainer;
					header = SettingsManager.CurrentTrack == null ? U.T("PlaybackEmpty") : SettingsManager.CurrentTrack.Title;
					subtitle = SettingsManager.CurrentTrack == null ? "" : SettingsManager.CurrentTrack.Artist;
					break;

				case "Visualizer":
					if (VisualizerContainer == null)
					{
						VisualizerContainer = new Platform.Windows7.GUI.Controls.Visualizer();
						Grid.SetRow(VisualizerContainer, 1);
						ContentContainer.Children.Add(VisualizerContainer);
						UpdateSelectedVisualizer();
					}
					content = VisualizerContainer;
					info = VisualizerList;
					header = VisualizerContainer.Title;
					subtitle = VisualizerContainer.Description;
					break;

				case "YouTube":
					vdc = SettingsManager.YouTubeListConfig;
					if (YouTubeTracks == null)
						YouTubeTracks = (YouTubeTracks)InitTrackList(new YouTubeTracks(), vdc);
					content = YouTubeTracks;
					header = U.T("NavigationYouTubeTitle");
					subtitle = U.T("NavigationYouTubeDescription");
					break;

				case "SoundCloud":
					vdc = SettingsManager.SoundCloudListConfig;
					if (SoundCloudTracks == null)
						SoundCloudTracks = (SoundCloudTracks)InitTrackList(new SoundCloudTracks(), vdc);
					content = SoundCloudTracks;
					header = U.T("NavigationSoundCloudTitle");
					subtitle = U.T("NavigationSoundCloudDescription");
					break;

				case "Jamendo":
					vdc = SettingsManager.JamendoListConfig;
					if (JamendoTracks == null)
						JamendoTracks = (JamendoTracks)InitTrackList(new JamendoTracks(), vdc);
					content = JamendoTracks;
					header = U.T("NavigationJamendoTitle");
					subtitle = U.T("NavigationJamendoDescription");
					break;

				case "Library":
				case "Files":
					vdc = SettingsManager.FileListConfig;
					if (FileTracks == null)
						FileTracks = InitTrackList(new ViewDetails(), vdc, SettingsManager.FileTracks);
					content = FileTracks;
					header = U.T("NavigationFilesTitle");
					time = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)LibraryTime));
					tracks = String.Format(U.T("HeaderTracks"), SettingsManager.FileTracks.Count);
					break;

				case "Radio":
					vdc = SettingsManager.RadioListConfig;
					if (RadioTracks == null)
						RadioTracks = InitTrackList(new ViewDetails(), vdc, SettingsManager.RadioTracks);
					content = RadioTracks;
					header = U.T("NavigationRadioTitle");
					time = "";
					tracks = String.Format(U.T("HeaderStations"), SettingsManager.RadioTracks.Count);
					break;

				case "Disc":
					vdc = SettingsManager.DiscListConfig;
					if (DiscTracks == null)
						DiscTracks = InitTrackList(new ViewDetails(), vdc);
					content = DiscTracks;
					header = U.T("NavigationDiscTitle");
					time = U.TimeSpanToLongString(new TimeSpan(0, 0, 0));
					tracks = String.Format(U.T("HeaderTracks"), 0);
					break;

				case "History":
					vdc = SettingsManager.HistoryListConfig;
					if (HistoryTracks == null)
						HistoryTracks = InitTrackList(new ViewDetails(), vdc, SettingsManager.HistoryTracks);
					content = HistoryTracks;
					header = U.T("NavigationHistoryTitle");
					time = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)HistoryTime));
					tracks = String.Format(U.T("HeaderTracks"), SettingsManager.HistoryTracks.Count);
					break;

				case "Queue":
					vdc = SettingsManager.QueueListConfig;
					if (QueueTracks == null)
						QueueTracks = InitTrackList(new ViewDetails(), vdc, SettingsManager.QueueTracks);
					content = QueueTracks;
					header = U.T("NavigationQueueTitle");
					time = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)QueueTime));
					tracks = String.Format(U.T("HeaderTracks"), SettingsManager.QueueTracks.Count);
					break;

				// playlist
				default:
					foreach (DictionaryEntry pltl in PlaylistTrackLists)
					{
						PlaylistData playlist = PlaylistManager.FindPlaylist((string)pltl.Key);
						if (playlist != null && SettingsManager.CurrentSelectedNavigation == "Playlist:"+playlist.Name)
						{
							vdc = playlist.ListConfig;
							if (pltl.Value == null)
								PlaylistTrackLists[pltl.Key] = InitTrackList(new ViewDetails(), vdc, playlist.Tracks);
							content = (ViewDetails)PlaylistTrackLists[pltl.Key];
							header = U.T("NavigationPlaylistTitle") + " '" + playlist.Name + "'";
							time = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)playlist.Time));
							tracks = String.Format(U.T("HeaderTracks"), playlist.Tracks.Count);
							break;
						}
					}
					break;
			}

			// switch back from ControlPanel if needed

			if (MainContainer.Children.Contains(ControlPanel))
				MainContainer.Children.Remove(ControlPanel);

			PlaybackControls.Search.Box.IsEnabled = true;
			MusicPanel.Visibility = System.Windows.Visibility.Visible;

			// set visibility of content elements
			var contentElements = new FrameworkElement[] { FileTracks, HistoryTracks, QueueTracks,
			    RadioTracks, SoundCloudTracks, JamendoTracks, YouTubeTracks, VideoContainer, VisualizerContainer };
			foreach (FrameworkElement e in contentElements)
				if (e != null)
					e.Visibility = e == content ? Visibility.Visible : Visibility.Collapsed;
			foreach (DictionaryEntry pltl in PlaylistTrackLists)
				if (pltl.Value != null)
					((ViewDetails)pltl.Value).Visibility = ((ViewDetails)pltl.Value) == content ? Visibility.Visible : Visibility.Collapsed;

			// set visibility of info elements
			var infoElements = new FrameworkElement[] { VisualizerList };
			foreach (FrameworkElement e in infoElements)
				e.Visibility = e == info ? Visibility.Visible : Visibility.Collapsed;

			// set info text
			InfoPaneTitle.Text = header;
			if (subtitle != null)
			{
				InfoPaneDuration.Visibility = Visibility.Collapsed;
				InfoPaneTracks.Visibility = Visibility.Collapsed;
				InfoPaneSubtitle.Visibility = Visibility.Visible;
				InfoPaneSubtitle.Text = subtitle;
			}
			else
			{
				InfoPaneDuration.Visibility = Visibility.Visible;
				InfoPaneTracks.Visibility = Visibility.Visible;
				InfoPaneSubtitle.Visibility = Visibility.Collapsed;
				InfoPaneDuration.Text = time;
				InfoPaneTracks.Text = tracks;
			}

			// set search text
			if (vdc == null || vdc.Filter == null || vdc.Filter == "" || vdc.Filter == U.T("PlaybackSearch"))
				PlaybackControls.Search.IsActive = false;
			else
			{
				PlaybackControls.Search.IsActive = true;
				PlaybackControls.Search.IsConnected = false;
				PlaybackControls.Search.Text = vdc.Filter;
				PlaybackControls.Search.IsConnected = true;
			}

			RefreshViewButton();
		}

		/// <summary>
		/// Opens the control panel view.
		/// </summary>
		/// <param name="tab">The control panel tab to switch to</param>
		private void OpenControlPanel(ControlPanel.Tab tab = Stoffi.ControlPanel.Tab.General)
		{
			InitControlPanel();

			MusicPanel.Visibility = System.Windows.Visibility.Collapsed;

			var elements = new FrameworkElement[] { FileTracks, HistoryTracks, QueueTracks,
				RadioTracks, SoundCloudTracks, JamendoTracks, YouTubeTracks, VideoContainer, VisualizerContainer };
			foreach (FrameworkElement element in elements)
				if (element != null && element != FileTracks)
					element.Visibility = Visibility.Collapsed;
			foreach (DictionaryEntry pltl in PlaylistTrackLists)
				if (pltl.Value != null)
					((ViewDetails)pltl.Value).Visibility = Visibility.Collapsed;

			if (!MainContainer.Children.Contains(ControlPanel))
				MainContainer.Children.Add(ControlPanel);

			PlaybackControls.Search.IsEnabled = false;
			ControlPanel.SwitchTab(tab);
		}

		/// <summary>
		/// Updates the selected visualizer according to SettingsManager.CurrentVisualizer
		/// </summary>
		private void UpdateSelectedVisualizer()
		{
			foreach (PluginItem v in VisualizerList.Items)
				if (v.ID == SettingsManager.CurrentVisualizer)
				{
					VisualizerList.SelectedItem = v;
					break;
				}
			if (VisualizerList.SelectedItem == null && VisualizerList.Items.Count > 0)
				VisualizerList.SelectedIndex = 0;
		}

		/// <summary>
		/// Refreshes the view button to correspond to the view mode of the
		/// currently visible track list if there is one, otherwise the button
		/// is hidden.
		/// </summary>
		private void RefreshViewButton()
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd == null)
				ViewButton.Visibility = Visibility.Collapsed;
			else
			{
				ViewButton.Visibility = Visibility.Visible;
				Image img = ViewButton.Content as Image;

				string icon = "Details";
				switch (vd.Mode)
				{
					case ViewDetails.ViewMode.Icons:
						if (vd.IconSize >= 256)
							icon = "ExtraLarge";
						else if (vd.IconSize >= 96)
							icon = "Large";
						else if (vd.IconSize >= 48)
							icon = "Medium";
						else
							icon = "Small";
						icon += "Icons";
						break;

					case ViewDetails.ViewMode.List:
						icon = "List";
						break;

					case ViewDetails.ViewMode.Details:
					default:
						icon = "Details";
						break;

					case ViewDetails.ViewMode.Tiles:
						icon = "Tiles";
						break;

					case ViewDetails.ViewMode.Content:
						icon = "Content";
						break;
				}

				icon = String.Format("pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/View{0}.png", icon);
				img.Source = new BitmapImage(new Uri(icon, UriKind.RelativeOrAbsolute));
			}
		}

		/// <summary>
		/// Fetches a copy of the currently active collection of tracks,
		/// used to select the next track to play.
		/// </summary>
		/// <returns>The list of the current track collection</returns>
		private List<TrackData> FetchActiveTrackCollection()
		{
			List<TrackData> l = new List<TrackData>();
			ViewDetails vd = GetActiveTrackList();
			if (vd == null) vd = FileTracks;
			foreach (TrackData track in vd.Items)
				l.Add(track);
			return l;
		}

		#endregion

		#region Event handlers

		/// <summary>
		/// Invoked when the window is loaded.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			U.L(LogLevel.Debug, "MAIN", "Started load");

			// check if we should start minimized
			bool hide = false;
			if (SettingsManager.FastStart)
			{
				String[] arguments = Environment.GetCommandLineArgs();
				foreach (string arg in arguments)
					if (arg == "-minimized")
					{
						hide = true;
						break;
					}
				if (hide)
				{
					ShowInTaskbar = false;
					Hide();
				}
			}

			System.Windows.Forms.Application.EnableVisualStyles();

			if (!SettingsManager.FastStart)
				WindowState = (WindowState)Enum.Parse(typeof(WindowState), SettingsManager.WinState);

			kListener.KeyDown += new RawKeyEventHandler(KListener_KeyDown);
			kListener.KeyUp += new RawKeyEventHandler(KListener_KeyUp);

			InitGUI();

			if (hide)
				trayIcon.Visibility = Visibility.Collapsed;

			U.L(LogLevel.Debug, "MAIN", "Load complete");

			//long timestamp = (long)((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
			//string newVersion = timestamp.ToString();
			//Console.WriteLine(String.Format("Timestamp: {0}", timestamp));

			//using (StreamWriter sw = File.AppendText(@"H:\bench.txt"))
			//{
			//    TimeSpan _ts = (DateTime.Now - U.initTime);
			//    try { sw.WriteLine(_ts.TotalMilliseconds); }
			//    catch { }
			//}

			//Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			//{
			//Application.Current.Shutdown();
			//}));
		}

		/// <summary>
		/// Invoked when a property changes inside a view details config.
		/// </summary>
		/// <remarks>
		/// Will refresh the visibility of search indicators in NavigationPane.
		/// </remarks>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewDetailsConfig_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Filter")
			{
				ViewDetailsConfig config = sender as ViewDetailsConfig;
				string s = "";
				if (FileTracks != null && config == FileTracks.Config)
					s = "Files";
				else if (RadioTracks != null && config == RadioTracks.Config)
					s = "Radio";
				else if (DiscTracks != null && config == DiscTracks.Config)
					s = "Disc";
				else if (YouTubeTracks != null && config == YouTubeTracks.Config)
					s = "YouTube";
				else if (SoundCloudTracks != null && config == SoundCloudTracks.Config)
					s = "SoundCloud";
				else if (JamendoTracks != null && config == JamendoTracks.Config)
					s = "Jamendo";
				else if (QueueTracks != null && config == QueueTracks.Config)
					s = "Queue";
				else if (HistoryTracks != null && config == HistoryTracks.Config)
					s = "History";
				else
					foreach (DictionaryEntry d in PlaylistTrackLists)
						if (d.Value != null && config == (d.Value as ViewDetails).Config)
							s = "Playlist:"+(string)d.Key;

				if (s != "")
					NavigationPane.SetSearchIndicator(s, config);
			}
		}

		/// <summary>
		/// Invoked when the user switches the session.
		/// Eg: lock or log off
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
		{
			if ((e.Reason == SessionSwitchReason.SessionLock && SettingsManager.PauseWhenLocked) ||
				(e.Reason == SessionSwitchReason.SessionLogoff && SettingsManager.PauseWhenLocked))
			{
				resumeWhenBack = SettingsManager.MediaState == Core.MediaState.Playing;
				MediaManager.Pause();
			}
			else if (resumeWhenBack &&
					(e.Reason == SessionSwitchReason.SessionLogon || e.Reason == SessionSwitchReason.SessionUnlock))
			{
				MediaManager.Play();
				resumeWhenBack = false;
			}
		}

		/// <summary>
		/// Invoked when a playlist is modified
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void PlaylistManager_PlaylistModified(object sender, ModifiedEventArgs e)
		{
			PlaylistData playlist = sender as PlaylistData;

			if (e.Type == ModifyType.Created)
			{
				CreatePlaylist(playlist, e.Interactive);
			}
			else if (e.Type == ModifyType.Removed)
			{
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
				{
					((ViewDetails)PlaylistTrackLists[playlist.Name]).Visibility = Visibility.Collapsed;
					PlaylistTrackLists.Remove(playlist.Name);

					// remove from context menu
					for (int i = 0; i < listMenuAddToPlaylist.Items.Count; i++)
					{
						if ((string)((MenuItem)listMenuAddToPlaylist.Items[i]).Header == playlist.Name)
						{
							listMenuAddToPlaylist.Items.RemoveAt(i);
							break;
						}
					}
					for (int i = 0; i < listMenuRemoveFromPlaylist.Items.Count; i++)
					{
						if ((string)((MenuItem)listMenuRemoveFromPlaylist.Items[i]).Header == playlist.Name)
						{
							listMenuRemoveFromPlaylist.Items.RemoveAt(i);
							break;
						}
					}
					if (listMenuRemoveFromPlaylist.Items.Count == 0)
						listMenuRemoveFromPlaylist.Visibility = System.Windows.Visibility.Collapsed;

					PlaybackControls.Search.RemovePlaylist(playlist);

					for (int i = 0; i < NavigationPane.Playlists.Items.Count; i++)
					{
						TreeViewItem tvi = (TreeViewItem)NavigationPane.Playlists.Items[i];
						if (tvi.Header is DockPanel)
						{
							EditableTextBlock tb = ((DockPanel)tvi.Header).Children[1] as EditableTextBlock;
							if (tb.Text == playlist.Name)
							{
								NavigationPane.historyList.Remove(tvi);
								if (tvi.IsSelected)
								{
									if (NavigationPane.historyList.Count > 0)
									{
										TreeViewItem prevItem = NavigationPane.historyList[NavigationPane.historyList.Count - 1];
										prevItem.IsSelected = true;
										prevItem.Focus();
									}
									else
									{
										if (SettingsManager.Playlists.Count > 0)
											((TreeViewItem)NavigationPane.Playlists.Items[0]).IsSelected = true;
										else
											NavigationPane.Music.IsSelected = true;
									}
								}
								NavigationPane.Playlists.Items.Remove(tvi);
								break;
							}
						}
					}

					// fix navigation references
					if (SettingsManager.CurrentSelectedNavigation == "Playlist:" + playlist.Name)
						SettingsManager.CurrentSelectedNavigation = "Files";
					if (SettingsManager.CurrentActiveNavigation == "Playlist:" + playlist.Name)
						SettingsManager.CurrentActiveNavigation = "Files";
				}));
			}
		}

		/// <summary>
		/// Invoked when a playlist is renamed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void PlaylistManager_PlaylistRenamed(object sender, RenamedEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				PlaylistData playlist = sender as PlaylistData;

				// rename key value in hashtable of tracklists
				PlaylistTrackLists.Add(e.Name, PlaylistTrackLists[e.OldName]);
				PlaylistTrackLists.Remove(e.OldName);

				// rename list context menu items
				foreach (MenuItem mi in listMenuAddToPlaylist.Items)
					if ((string)mi.Header == e.OldName)
						mi.Header = e.Name;
				foreach (MenuItem mi in listMenuRemoveFromPlaylist.Items)
					if ((string)mi.Header == e.OldName)
						mi.Header = e.Name;
				PlaybackControls.Search.RenamePlaylist(e.OldName, e.Name);

				// rename navigation items
				foreach (TreeViewItem item in NavigationPane.Playlists.Items)
				{
					DockPanel dp = item.Header as DockPanel;
					EditableTextBlock etb = dp.Children[1] as EditableTextBlock;
					if (etb.Text == e.OldName)
					{
						etb.Text = e.Name;
						etb.Focus();
						break;
					}
				}

				playlist.Name = e.Name;

				// fix navigation references
				if (SettingsManager.CurrentSelectedNavigation == "Playlist:" + e.OldName)
					SettingsManager.CurrentSelectedNavigation = "Playlist:" + e.Name;
				if (SettingsManager.CurrentActiveNavigation == "Playlist:" + e.OldName)
					SettingsManager.CurrentActiveNavigation = "Playlist:" + e.Name;
			}));
		}

		/// <summary>
		/// Invoked when the progress of an upgrade changes
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void UpgradeManager_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				ProgressState ps = e.UserState as ProgressState;
				if (ControlPanel != null)
				{
					ControlPanel.UpgradeProgressInfo.Text = ps.Message;
					ControlPanel.UpgradeProgressBar.Value = e.ProgressPercentage;
					ControlPanel.UpgradeProgressBar.IsIndeterminate = ps.IsIndetermined;
				}
				TaskbarItemInfo.ProgressValue = (double)e.ProgressPercentage / 100;
				TaskbarItemInfo.ProgressState = (ps.IsIndetermined ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.Normal);
			}));
		}

		/// <summary>
		/// Invoked when an error occurs during an upgrade
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="message">The error message</param>
		private void UpgradeManager_ErrorOccured(object sender, string message)
		{
			U.L(LogLevel.Debug, "MAIN", "Upgrade manager encountered an error: " + message);
			Dispatcher.Invoke(new Action(delegate
			{
				if (TaskbarItemInfo != null)
					TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

				if (SettingsManager.UpgradePolicy == UpgradePolicy.Manual || UpgradeManager.ForceDownload)
				{
					ControlPanel.UpgradeMessageText.Text = message;
					ControlPanel.UpgradeMessageIcon.Source = new BitmapImage(new Uri("/Platform/Windows 7/GUI/Images/Icons/Error.ico", UriKind.RelativeOrAbsolute));
					ControlPanel.UpgradeMessage.Visibility = System.Windows.Visibility.Visible;

					if (UpgradeManager.Policy == UpgradePolicy.Manual)
						ControlPanel.EnableUpgradeCheck();
					else
						ControlPanel.EnableUpgradeDo();
				}
			}));
		}

		/// <summary>
		/// Invoked when an upgrade is found
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void UpgradeManager_UpgradeFound(object sender, EventArgs e)
		{
			U.L(LogLevel.Debug, "MAIN", "Upgrade manager found upgrade");
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				ControlPanel.UpgradeMessageText.Text = U.T("UpgradeFound");
				ControlPanel.UpgradeMessageIcon.Source = new BitmapImage(new Uri("/Platform/Windows 7/GUI/Images/Icons/Info.ico", UriKind.RelativeOrAbsolute));
				ControlPanel.UpgradeMessage.Visibility = System.Windows.Visibility.Visible;
				ControlPanel.EnableUpgradeDo();
				if (TaskbarItemInfo != null)
					TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

				if (SettingsManager.UpgradePolicy == UpgradePolicy.Notify)
				{
					TrayNotification notification = new TrayNotification(this);
					trayIcon.ShowCustomBalloon(notification, System.Windows.Controls.Primitives.PopupAnimation.Fade, 3600000);
					ControlPanel.PrefDoUpgrade.Visibility = Visibility.Visible;
				}
			}));
		}

		/// <summary>
		/// Invoked when a check for upgrades has been performed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void UpgradeManager_Checked(object sender, EventArgs e)
		{
			U.L(LogLevel.Debug, "MAIN", "Upgrade manager completed check");
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				TimeSpan _ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0));
				SettingsManager.UpgradeCheck = (long)_ts.TotalSeconds;
				if (ControlPanel != null)
					ControlPanel.UpdateUpgradeCheck();
				if (TaskbarItemInfo != null)
					TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

				if (UpgradeManager.Policy == UpgradePolicy.Manual && !UpgradeManager.Found && ControlPanel != null)
				{
					ControlPanel.EnableUpgradeCheck();
					ControlPanel.UpgradeMessageIcon.Source = new BitmapImage(new Uri("/Platform/Windows 7/GUI/Images/Icons/Info.ico", UriKind.RelativeOrAbsolute));
					ControlPanel.UpgradeMessageText.Text = U.T("UpgradeNotFound");
					ControlPanel.UpgradeMessage.Visibility = System.Windows.Visibility.Visible;
				}
			}));
		}

		/// <summary>
		/// Invoked when the application has been upgraded
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void UpgradeManager_Upgraded(object sender, EventArgs e)
		{
			U.L(LogLevel.Debug, "MAIN", "Upgrade manager has upgraded Stoffi");
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				ControlPanel.PrefDoUpgrade.Visibility = Visibility.Collapsed;
				ControlPanel.UpgradeProgress.Visibility = Visibility.Collapsed;
				if (TaskbarItemInfo != null)
					TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
				if (SettingsManager.UpgradePolicy == UpgradePolicy.Manual)
				{
					ControlPanel.EnableRestart();
					ControlPanel.UpgradeMessageText.Text = U.T("UpgradePending");
					ControlPanel.UpgradeMessageIcon.Source = new BitmapImage(new Uri("/Platform/Windows 7/GUI/Images/Icons/Info.ico", UriKind.RelativeOrAbsolute));
					ControlPanel.UpgradeMessage.Visibility = System.Windows.Visibility.Visible;
				}
				else
					ControlPanel.UpgradeMessage.Visibility = Visibility.Collapsed;
			}));
		}

		/// <summary>
		/// Invoked when the media manager switches from one track to another
		/// </summary>
		/// <param name="e">The event data</param>
		private void MediaManager_TrackSwitched(TrackSwitchedEventArgs e)
		{
			if (trackSwitchDelay != null)
				trackSwitchDelay.Stop();
			trackSwitchDelay = new DispatcherTimer();
			trackSwitchDelay.Interval = new TimeSpan(0, 0, 1);
			trackSwitchDelay.Tick += delegate(object s, EventArgs ea) { MediaManager_TrackSwitchedDelayed(e); };
			trackSwitchDelay.IsEnabled = true;

			// follow track
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				ViewDetails tl = GetCurrentTrackList();
				if (tl == null) return;

				ListViewItem lvi = tl.ItemContainerGenerator.ContainerFromItem(e.OldTrack) as ListViewItem;
				if (lvi != null)
				{
					Point trackLoc = lvi.TranslatePoint(new Point(0, 0), tl);
					double trackListHeight = tl.ActualHeight;
					double trackHeight = lvi.ActualHeight;

					if (trackLoc.Y + trackHeight > 0 || trackLoc.Y <= trackListHeight)
						tl.ScrollIntoView(MediaManager.GetSourceTrack(e.NewTrack));
				}
			}));
		}

		/// <summary>
		/// Invoked after a short delay when a track is switched to.
		/// </summary>
		/// <param name="e">The event data</param>
		private void MediaManager_TrackSwitchedDelayed(TrackSwitchedEventArgs e)
		{
			if (trackSwitchDelay != null)
				trackSwitchDelay.Stop();

			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				if (trayIcon != null) ((TrayToolTip)trayIcon.TrayToolTip).SetTrack(e.NewTrack);

				if (SettingsManager.ShowOSD)
				{
					TrayNotification notification = new TrayNotification(e.NewTrack, this);
					if (trayIcon != null)
						trayIcon.ShowCustomBalloon(notification, System.Windows.Controls.Primitives.PopupAnimation.Fade, 4000);
				}

				JumpTask jumpTask = new JumpTask()
				{
					Title = e.NewTrack.Artist + " - " + e.NewTrack.Title,
					Arguments = e.NewTrack.Path,
					Description = e.NewTrack.Artist + " - " + e.NewTrack.Title,
					IconResourcePath = "C:\\Windows\\System32\\imageres.dll",
					IconResourceIndex = 190,
					ApplicationPath = Assembly.GetEntryAssembly().CodeBase,
				};

				System.Windows.Shell.JumpList.AddToRecentCategory(jumpTask);
			}));
		}

		/// <summary>
		/// Invoked when a new track is loaded, that is a track which
		/// moves the history index forward.
		/// </summary>
		/// <param name="track">The track that was loaded</param>
		public void MediaManager_LoadedTrack(TrackData track)
		{
			if (loadedTrackDelay != null)
				loadedTrackDelay.Stop();
			loadedTrackDelay = new DispatcherTimer();
			loadedTrackDelay.Interval = new TimeSpan(0, 0, 2);
			loadedTrackDelay.Tick += delegate(object s, EventArgs ea) { MediaManager_LoadedTrackDelayed(track); };
			loadedTrackDelay.IsEnabled = true;
		}

		/// <summary>
		/// Invoked after a short delay when a track is loaded.
		/// </summary>
		/// <param name="track">The track that was loaded</param>
		private void MediaManager_LoadedTrackDelayed(TrackData track)
		{
			if (loadedTrackDelay != null)
				loadedTrackDelay.Stop();

			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				if (SettingsManager.HistoryIndex >= SettingsManager.HistoryTracks.Count)
				{
					// we create a new track (this allows us to have multiple tracks in history with different "last played"
					TrackData historyTrack = new TrackData();
					historyTrack.Artist = track.Artist;
					historyTrack.Album = track.Album;
					historyTrack.Title = track.Title;
					historyTrack.Track = track.Track;
					historyTrack.PlayCount = track.PlayCount;
					historyTrack.Path = track.Path;
					historyTrack.Year = track.Year;
					historyTrack.Length = track.Length;
					historyTrack.Genre = track.Genre;
					historyTrack.LastPlayed = DateTime.Now;
					historyTrack.PropertyChanged += new PropertyChangedEventHandler(FilesystemManager.Track_PropertyChanged);
					historyTrack.Source = track.Source;
					historyTrack.Bookmarks = track.Bookmarks;
					historyTrack.Icon = track.Icon;
					SettingsManager.HistoryTracks.Add(historyTrack);
				}
				SettingsManager.HistoryIndex++;
			}));
		}

		/// <summary>
		/// Invoked when the media manager has finished the attempt
		/// to start playback.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void MediaManager_Started(object sender, EventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				Cursor = Cursors.Arrow;
			}));
		}

		/// <summary>
		/// Invoked when the user clicks Next inside the Properties window
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Properties_NextClick(object sender, EventArgs e)
		{
			PropertiesWindow pw = sender as PropertiesWindow;
			ViewDetails trackList = GetCurrentTrackList();
			ItemCollection items = trackList.Items;
			List<TrackData> t = new List<TrackData>();

			if (trackList.Items.Count == pw.Tracks.Count)
				return;

			for (int i = (trackList.IndexOf(pw.Tracks[pw.Tracks.Count - 1]) + 1) % items.Count; // use % to wrap
				t.Count < pw.Tracks.Count;
				i = ++i % items.Count) // use % to wrap
			{
				// find next non-youtube
				TrackData track = (TrackData)trackList.GetItemAt(i);
				int end = (i - 1 + items.Count) % items.Count; // stop when checking this far
				while (YouTubeManager.IsYouTube(track) && i != end)
				{
					i = ++i % items.Count; // use % to wrap
					track = (TrackData)trackList.GetItemAt(i);
				}

				// add if any non-youtube
				if (i < items.Count)
					t.Add(track);
			}

			if (t.Count > 0)
				pw.AddTracks(t);
			// else?
		}

		/// <summary>
		/// Invoked when the user clicks Previous inside the Properties window
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Properties_PrevClick(object sender, EventArgs e)
		{
			PropertiesWindow pw = sender as PropertiesWindow;
			ViewDetails trackList = GetCurrentTrackList();
			ItemCollection items = trackList.Items;
			List<TrackData> t = new List<TrackData>();

			if (trackList.Items.Count == pw.Tracks.Count)
				return;

			for (int i = (trackList.IndexOf(pw.Tracks[0]) - 1 + items.Count) % items.Count; // use % to wrap
				t.Count < pw.Tracks.Count;
				i = --i + items.Count % items.Count) // use % to wrap
			{
				// find next non-youtube
				TrackData track = (TrackData)trackList.GetItemAt(i);
				int end = (i + 1) % items.Count; // stop when checking this far
				while (YouTubeManager.IsYouTube(track) && i != end)
				{
					i = --i + items.Count % items.Count; // use % to wrap
					if (i < 0) i = items.Count + i;
					track = (TrackData)trackList.GetItemAt(i);
				}

				// add if any non-youtube
				if (i < items.Count)
					t.Add(track);
			}

			t.Reverse();
			if (t.Count > 0)
				pw.AddTracks(t);
		}

		/// <summary>
		/// Invoked when an editable text block enters edit mode
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void EditableTextBlock_EnteredEditMode(object sender, EventArgs e)
		{
			etbInEdit = sender as EditableTextBlock;
		}

		/// <summary>
		/// Invoked when the user doubleclicks a track in a track list
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrackList_MouseDoubleClick(object sender, RoutedEventArgs e)
		{
			Play();
		}

		/// <summary>
		/// Invoked when the user right-clicks the track list
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrackList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (SettingsManager.CurrentSelectedNavigation == "Queue")
				listMenuQueue.Visibility = System.Windows.Visibility.Collapsed;

			else
				listMenuQueue.Visibility = System.Windows.Visibility.Visible;

			bool containsYouTube = false;
			bool containsFiles = false;
			bool containsRadio = false;
			bool containsSoundCloud = false;
			bool containsJamendo = false;
			bool isPlaying = true;
			bool isQueued = false;
			ViewDetails vd = sender as ViewDetails;
			List<TrackData> tracks = new List<TrackData>();
			foreach (TrackData t in vd.SelectedItems)
			{
				tracks.Add(t);
				switch (MediaManager.GetType(t))
				{
					case TrackType.File:
						containsFiles = true;
						break;

					case TrackType.WebRadio:
						containsRadio = true;
						break;

					case TrackType.YouTube:
						containsYouTube = true;
						break;

					case TrackType.SoundCloud:
						containsSoundCloud = true;
						break;

					case TrackType.Jamendo:
						containsJamendo = true;
						break;
				}

				if (SettingsManager.CurrentTrack == null || SettingsManager.CurrentTrack.Path != t.Path || SettingsManager.MediaState != Core.MediaState.Playing)
					isPlaying = false;
				if (!isQueued)
					foreach (TrackData u in SettingsManager.QueueTracks)
						if (t.Path == u.Path)
							isQueued = true;
			}

			bool disableAll = true;
			foreach (MenuItem mi in listMenuRemoveFromPlaylist.Items)
			{
				PlaylistData p = PlaylistManager.FindPlaylist(mi.Header as string);
				mi.IsEnabled = p != null && PlaylistManager.ContainsAny(p, tracks);
				if (mi.IsEnabled) disableAll = false;
			}
			listMenuRemoveFromPlaylist.IsEnabled = !disableAll;

			foreach (MenuItem mi in listMenuAddToPlaylist.Items)
			{
				PlaylistData p = PlaylistManager.FindPlaylist(mi.Header as string);
				mi.IsEnabled = p != null && !PlaylistManager.ContainsAll(p, tracks);
			}
			listMenuAddToNew.IsEnabled = true;

			bool onlyFiles = !(containsYouTube || containsRadio || containsSoundCloud || containsJamendo);
			bool onlyYouTube = !(containsFiles || containsRadio || containsSoundCloud || containsJamendo);
			bool onlyRadio = !(containsFiles || containsYouTube || containsSoundCloud || containsJamendo);
			bool onlySoundCloud = !(containsFiles || containsYouTube || containsRadio || containsJamendo);
			bool onlyJamendo = !(containsFiles || containsYouTube || containsRadio || containsSoundCloud);
			bool onlySharable = !containsFiles;
			bool inPlaylist = SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:");

			// only files
			listMenuCopy.Visibility = onlyFiles ? Visibility.Visible : Visibility.Collapsed;
			listMenuMove.Visibility = onlyFiles ? Visibility.Visible : Visibility.Collapsed;
			listMenuInfo.Visibility = onlyFiles ? Visibility.Visible : Visibility.Collapsed;
			listMenuDelete.Visibility = onlyFiles ? Visibility.Visible : Visibility.Collapsed;

			// only youtube
			listMenuWatchOnYouTube.Visibility = onlyYouTube ? Visibility.Visible : Visibility.Collapsed;

			// only radio
			listMenuVisitWebsite.Visibility = onlyRadio ? Visibility.Visible : Visibility.Collapsed;

			// only soundcloud
			listMenuListenOnSoundCloud.Visibility = onlySoundCloud ? Visibility.Visible : Visibility.Collapsed;

			// only jamendo
			listMenuListenOnJamendo.Visibility = onlyJamendo ? Visibility.Visible : Visibility.Collapsed;

			// only files
			listMenuOpenFolder.Visibility = onlyFiles ? Visibility.Visible : Visibility.Collapsed;

			// only sharable
			listMenuShareSong.Visibility = ServiceManager.Linked && onlySharable ? Visibility.Visible : Visibility.Collapsed;

			// if files, radio or in playlist
			listMenuRemove.Visibility = containsFiles || containsRadio || inPlaylist ? Visibility.Visible : Visibility.Collapsed;
			listMenuFilesystemSeparator.Visibility = containsFiles || containsRadio ? Visibility.Visible : Visibility.Collapsed;

			listMenuPlay.Header = (isPlaying ? U.T("MenuPause") : U.T("MenuPlay"));
			listMenuQueue.Header = (isQueued ? U.T("MenuDequeue") : U.T("MenuQueue"));
		}

		/// <summary>
		/// Invoked when the property of a track list changes.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrackList_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == GetCurrentTrackList() && sender != null && e.PropertyName == "Mode")
				RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks play in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuPlay_Click(object sender, RoutedEventArgs e)
		{
			MenuItem mi = sender as MenuItem;
			if (mi == null || (string)mi.Header == U.T("MenuPlay"))
				Play(false);
			else
				MediaManager.Pause();
		}

		/// <summary>
		/// Invoked when the user clicks remove in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuRemove_Click(object sender, RoutedEventArgs e)
		{
			int i = GetCurrentTrackList().SelectedIndex;
			bool inPlaylist = SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:");
			bool inQueue = SettingsManager.CurrentSelectedNavigation == "Queue";
			bool inHistory = SettingsManager.CurrentSelectedNavigation == "History";
			List<TrackData> tracks = new List<TrackData>();
			foreach (TrackData track in GetCurrentTrackList().SelectedItems)
				if (MediaManager.GetType(track) == TrackType.File ||
					MediaManager.GetType(track) == TrackType.WebRadio ||
					inPlaylist || inQueue || inHistory)
					tracks.Add(track);

			// stop playing if removing active tracks
			foreach (TrackData t in tracks)
			{
				if (t.IsActive)
				{
					MediaManager.Stop();
					break;
				}
			}

			// remove from whole collection
			if (SettingsManager.CurrentSelectedNavigation == "Files")
			{
				foreach (TrackData track in tracks)
				{
					RemoveTrack(track.Path, SettingsManager.FileTracks);
					RemoveTrack(track.Path, SettingsManager.QueueTracks);
					RemoveTrack(track.Path, SettingsManager.HistoryTracks);
					foreach (PlaylistData p in SettingsManager.Playlists)
						RemoveTrack(track.Path, p.Tracks);

					SourceData source = FilesystemManager.GetSource(track.Path);

					// remove from sources?
					if (FilesystemManager.PathIsAdded(track.Path) && source != null)
						FilesystemManager.RemoveSource(source);
					else if (!FilesystemManager.PathIsIgnored(track.Path))
						FilesystemManager.AddSource(new SourceData
						{
							Data = track.Path,
							Ignore = true,
							Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/FileAudio.ico",
							Type = SourceType.File
						});
				}
			}

			// remove radio station
			else if (SettingsManager.CurrentSelectedNavigation == "Radio")
			{
				foreach (TrackData track in tracks)
					SettingsManager.RadioTracks.Remove(track);
			}

			// dequeue
			else if (SettingsManager.CurrentSelectedNavigation == "Queue")
				MediaManager.Dequeue(tracks);

			// remove from history
			else if (SettingsManager.CurrentSelectedNavigation == "History")
			{
				foreach (TrackData track in tracks)
				{
					if (SettingsManager.HistoryTracks.IndexOf(track) <= SettingsManager.HistoryIndex)
						SettingsManager.HistoryIndex--;
					RemoveTrack(track.Path, SettingsManager.HistoryTracks);
				}

				if (SettingsManager.HistoryIndex > SettingsManager.HistoryTracks.Count-1)
					SettingsManager.HistoryIndex = SettingsManager.HistoryTracks.Count-1;
			}

			// remove from playlist
			else
			{
				string pl = SettingsManager.CurrentSelectedNavigation.Split(new[] { ':' }, 2)[1];
				PlaylistManager.RemoveFromPlaylist(tracks, pl);
			}

			// refresh selection
			if (GetCurrentTrackList().Items.Count > 0)
			{
				if (i >= GetCurrentTrackList().Items.Count)
					GetCurrentTrackList().SelectedIndex = GetCurrentTrackList().Items.Count - 1;
				else
					GetCurrentTrackList().SelectedIndex = i;
			}

		}

		/// <summary>
		/// Invoked when the user clicks delete in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuDelete_Click(object sender, RoutedEventArgs e)
		{
			if (MessageBox.Show(U.T("MessageConfirmDelete", "Message"), U.T("MessageConfirmDelete", "Title"),
								MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
			{
				int i = GetCurrentTrackList().SelectedIndex;
				List<TrackData> tracks = new List<TrackData>();
				foreach (TrackData track in GetCurrentTrackList().SelectedItems)
					if (MediaManager.GetType(track) == TrackType.File)
						tracks.Add(track);
				foreach (TrackData t in tracks)
				{
					if (t.IsActive)
					{
						MediaManager.Stop();
						break;
					}
				}
				foreach (TrackData track in tracks)
				{
					try
					{
						File.Delete(track.Path);
					}
					catch (Exception exc)
					{
						MessageBox.Show(exc.Message, U.T("MessageErrorDeleting", "Title"), MessageBoxButton.OK, MessageBoxImage.Error);
					}
				}

				if (GetCurrentTrackList().Items.Count > 0)
				{
					if (i >= GetCurrentTrackList().Items.Count)
						GetCurrentTrackList().SelectedIndex = GetCurrentTrackList().Items.Count - 1;
					else
						GetCurrentTrackList().SelectedIndex = i;
				}
			}
		}

		/// <summary>
		/// Invoked when the user clicks information in the track list's 
		/// context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuInfo_Click(object sender, RoutedEventArgs e)
		{
			if (GetCurrentTrackList().SelectedItems.Count > 0)
			{
				List<TrackData> tracks = new List<TrackData>();
				foreach (TrackData t in GetCurrentTrackList().SelectedItems)
					if (MediaManager.GetType(t) == TrackType.File)
						tracks.Add(t);

				PropertiesWindow dialog = new PropertiesWindow(tracks);
				dialog.NextClick += new EventHandler(Properties_NextClick);
				dialog.PreviousClick += new EventHandler(Properties_PrevClick);
				dialog.ShowDialog();
			}
		}

		/// <summary>
		/// Invoked when the user clicks add to a playlist in the track 
		/// list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuAddToPlaylist_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem)
			{
				MenuItem item = sender as MenuItem;
				String name = item.Header.ToString();

				if (name == U.T("MenuCreateNew", "Header"))
				{
					NavigationPane.AddToPlaylistQueue.Clear();
					foreach (TrackData track in GetCurrentTrackList().SelectedItems)
						NavigationPane.AddToPlaylistQueue.Add(track);
					NavigationPane.CreateNewPlaylistETB.IsInEditMode = true;
				}
				else
				{
					List<object> tracks = new List<object>();
					foreach (TrackData track in GetCurrentTrackList().SelectedItems)
						tracks.Add(track);
					PlaylistManager.AddToPlaylist(tracks, name);
				}
			}
		}

		/// <summary>
		/// Invoked when the user clicks add to a new playlist in the 
		/// track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuAddToNew_Click(object sender, RoutedEventArgs e)
		{
			NavigationPane.AddToPlaylistQueue.Clear();
			foreach (TrackData track in GetCurrentTrackList().SelectedItems)
				NavigationPane.AddToPlaylistQueue.Add(track);

			NavigationPane.CreateNewPlaylistETB.IsInEditMode = true;
		}

		/// <summary>
		/// Invoked when the user clicks remove from a playlist in 
		/// the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuRemoveFromPlaylist_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem)
			{
				MenuItem item = sender as MenuItem;
				String name = item.Header.ToString();

				List<TrackData> tracks = new List<TrackData>();
				foreach (TrackData track in GetCurrentTrackList().SelectedItems)
					tracks.Add(track);
				PlaylistManager.RemoveFromPlaylist(tracks, name);
			}
		}

		/// <summary>
		/// Invoked when the user clicks queue/dequeue in the track 
		/// list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuQueue_Click(object sender, RoutedEventArgs e)
		{
			var vd = GetCurrentTrackList();
			var mi = sender as MenuItem;

			if (vd != null && mi != null)
			{
				List<TrackData> tracks = new List<TrackData>();
				foreach (int i in vd.Config.SelectedIndices)
					tracks.Add(vd.GetItemAt(i) as TrackData);

				if ((string)mi.Header == U.T("MenuQueue"))
					MediaManager.Queue(tracks);
				else
					MediaManager.Dequeue(tracks);
			}			
		}

		/// <summary>
		/// Invoked when the user clicks move/copy in the track 
		/// list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuCopyMove_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.SelectedPath = dialogPath;
			System.Windows.Forms.DialogResult result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				if (FilesystemManager.PathIsAdded(dialog.SelectedPath))
					MessageBox.Show(
						U.T("MessageAlreadyAddedSource", "Message"),
						U.T("MessageAlreadyAddedSource", "Title"),
						MessageBoxButton.OK, MessageBoxImage.Information);
				else
				{
					List<TrackData> tracks = new List<TrackData>();
					foreach (TrackData track in GetCurrentTrackList().SelectedItems)
						if (MediaManager.GetType(track) == TrackType.File)
							tracks.Add(track);

					MenuItem mi = sender as MenuItem;
					string mode = "Move";
					if (mi == listMenuCopy)
						mode = "Copy";

					foreach (TrackData track in tracks)
					{
						try
						{
							string src = track.Path;
							string dst = Path.Combine(dialog.SelectedPath, Path.GetFileName(track.Path));
							if (mode == "Copy")
								File.Copy(src, dst);
							else
								File.Move(src, dst);
						}
						catch (Exception exc)
						{
							U.L(LogLevel.Error, "MAIN", "Could not move file: " + exc.Message);

							if (mode == "Copy")
								MessageBox.Show(exc.Message, U.T("MessageErrorCopying"), MessageBoxButton.OK, MessageBoxImage.Error);
							else
								MessageBox.Show(exc.Message, U.T("MessageErrorMoving"), MessageBoxButton.OK, MessageBoxImage.Error);
							
						}
					}
				}
			}
		}

		/// <summary>
		/// Invoked when the user clicks to watch a track on YouTube
		/// in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuWatchOnYouTube_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (GetCurrentTrackList().SelectedItems.Count > 0)
				{
					List<TrackData> tracks = new List<TrackData>();
					foreach (TrackData t in GetCurrentTrackList().SelectedItems)
						if (YouTubeManager.IsYouTube(t))
							tracks.Add(t);

					foreach (TrackData track in tracks)
					{
						string vid = YouTubeManager.GetYouTubeID(track.Path);
						MediaManager.Pause();
						int autoplay = tracks.Count > 0 && tracks.IndexOf(track) > 0 ? 0 : 1;
						System.Diagnostics.Process.Start(String.Format("http://www.youtube.com/watch?v={0}&autoplay={1}&feature=stoffi", vid, autoplay));
					}
				}
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not play track on youtube.com: " + exc.Message);
			}
		}

		/// <summary>
		/// Invoked when the user clicks to listen to a track on
		/// SoundCloud in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuListenOnSoundCloud_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (GetCurrentTrackList().SelectedItems.Count > 0)
				{
					List<TrackData> tracks = new List<TrackData>();
					foreach (TrackData t in GetCurrentTrackList().SelectedItems)
						if (MediaManager.GetType(t) == TrackType.SoundCloud && t.URL != null)
							tracks.Add(t);

					foreach (TrackData track in tracks)
					{
						string vid = track.Path.Substring(10);
						MediaManager.Pause();
						System.Diagnostics.Process.Start(track.URL);
					}
				}
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not play track on soundcloud.com: " + exc.Message);
			}
		}

		/// <summary>
		/// Invoked when the user clicks to listen to a track on
		/// Jamendo in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuListenOnJamendo_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (GetCurrentTrackList().SelectedItems.Count > 0)
				{
					List<TrackData> tracks = new List<TrackData>();
					foreach (TrackData t in GetCurrentTrackList().SelectedItems)
						if (MediaManager.GetType(t) == TrackType.Jamendo && t.URL != null)
							tracks.Add(t);

					foreach (TrackData track in tracks)
					{
						string vid = track.Path.Substring(10);
						MediaManager.Pause();
						System.Diagnostics.Process.Start(track.URL);
					}
				}
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not play track on jamendo.com: " + exc.Message);
			}
		}

		/// <summary>
		/// Invoked when the user clicks to open the containing
		/// folder of a track.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuOpenFolder_Click(object sender, RoutedEventArgs e)
		{
			if (GetCurrentTrackList().SelectedItems.Count > 0)
			{
				StringCollection paths = new StringCollection();
				foreach (TrackData t in GetCurrentTrackList().SelectedItems)
					if (MediaManager.GetType(t) == TrackType.File)
					{
						string path = Path.GetDirectoryName(t.Path);
						if (Directory.Exists(path) && !paths.Contains(path))
							paths.Add(path);
					}

				foreach (String path in paths)
					System.Diagnostics.Process.Start(path);
			}
		}

		/// <summary>
		/// Invoked when the user clicks to share a YouTube track
		/// in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuShareSong_Click(object sender, RoutedEventArgs e)
		{
			if (GetCurrentTrackList().SelectedItems.Count > 0)
			{
				List<TrackData> tracks = new List<TrackData>();
				foreach (TrackData track in GetCurrentTrackList().SelectedItems)
				{
					TrackType t = MediaManager.GetType(track);
					if (t == TrackType.YouTube || t == TrackType.SoundCloud || t == TrackType.WebRadio)
						tracks.Add(track);
				}

				foreach (TrackData track in tracks)
					ServiceManager.ShareSong(track);
			}
		}

		/// <summary>
		/// Invoked when the user clicks "Visit website" on a
		/// radio tracks in the track list's context menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void listMenuVisitWebsite_Click(object sender, RoutedEventArgs e)
		{
			if (GetCurrentTrackList().SelectedItems.Count > 0)
			{
				List<TrackData> tracks = new List<TrackData>();
				foreach (TrackData t in GetCurrentTrackList().SelectedItems)
					if (MediaManager.GetType(t) == TrackType.WebRadio)
						tracks.Add(t);

				foreach (TrackData track in tracks)
				{
					if (track.URL != null && track.URL.StartsWith("http"))
						System.Diagnostics.Process.Start(track.URL);
				}
			}
		}

		/// <summary>
		/// Invoked when there's a problem with the Internet connection.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Video_ConnectionProblem(object sender, EventArgs e)
		{
			if (showMediaError)
			{
				showMediaError = false;
				MessageBox.Show(U.T("MessageConnectionProblem", "Message"),
					U.T("MessageConnectionProblem", "Title"), MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Invoked when a key is pressed no matter if the app has focus or not.
		/// (catched by the KListener)
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void KListener_KeyDown(object sender, RawKeyEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate() { KeyPressed(e.key, e.key, true); }));
		}

		/// <summary>
		/// Invoked when a key is released no matter if the app has focus or not.
		/// (catched by the KListener)
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void KListener_KeyUp(object sender, RawKeyEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate() { KeyReleased(e.key, e.key, true); }));
		}

		/// <summary>
		/// Invoked when a key is pressed while a playlist in the navigation pane has focus.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void NavigationPlaylist_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F2)
				NavigationPane.RenamePlaylist_Clicked(sender, null);
		}

		/// <summary>
		/// Invoked when the navigation pane gets focus.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void NavigationPane_GotFocus(object sender, EventArgs e)
		{
			RefreshDetails();
		}

		/// <summary>
		/// Invoked when the user clicks "Open playlist" and chooses "From file"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void AddPlaylistFile_Clicked(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
			dialog.Title = "Open Playlist";
			dialog.DefaultExt = ".pls";
			dialog.Filter = "Playlists (.pls)|*.pls|Playlists (.m3u)|*.m3u";
			bool result = (bool)dialog.ShowDialog();
			if (result == true)
				PlaylistManager.LoadPlaylist(dialog.FileName);
		}

		/// <summary>
		/// Invoked when the user clicks "Open playlist" and chooses "From YouTube"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void OpenPlaylistYouTube_Clicked(object sender, RoutedEventArgs e)
		{
			OpenURL d = new OpenURL(OpenPlaylistYouTube_Finished);
			d.Owner = this;
			d.Title = U.T("AddYouTubePlaylistTitle", "Title");
			d.WindowStartupLocation = WindowStartupLocation.CenterOwner;

			if ((bool)d.ShowDialog())
			{
				if (d.IsParsing)
					this.Cursor = Cursors.Wait;
				else
				{
					NavigationPane.AddToPlaylistQueue.Clear();
					foreach (TrackData track in d.Tracks)
						NavigationPane.AddToPlaylistQueue.Add(track);

					NavigationPane.CreateNewPlaylistETB.IsInEditMode = true;
					if (!String.IsNullOrWhiteSpace(d.MetaTitle.Text))
						NavigationPane.CreateNewPlaylistETB.Text = d.MetaTitle.Text;
				}
			}
		}

		/// <summary>
		/// Invoked when the OpenURL dialog finished parsing a YouTube
		/// playlist URL and the dialog was closed before the parsing
		/// could finish.
		/// </summary>
		/// <param name="tracks">The tracks representing the playlist at the URL</param>
		public void OpenPlaylistYouTube_Finished(List<TrackData> tracks)
		{
			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				this.Cursor = Cursors.Arrow;
				NavigationPane.AddToPlaylistQueue.Clear();
				foreach (TrackData track in tracks)
					NavigationPane.AddToPlaylistQueue.Add(track);

				NavigationPane.CreateNewPlaylistETB.IsInEditMode = true;
			}));
		}

		/// <summary>
		/// Invoked when the user clicks to add a radio station.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void AddURL_Clicked(object sender, RoutedEventArgs e)
		{
			OpenURL d = new OpenURL(OpenURL_Finished);
			d.Owner = this;
			d.Title = U.T("AddURLTitle", "Title");
			d.WindowStartupLocation = WindowStartupLocation.CenterOwner;

			if ((bool)d.ShowDialog())
			{
				if (d.IsParsing)
					this.Cursor = Cursors.Wait;
				else
					foreach (TrackData track in d.Tracks)
						SettingsManager.RadioTracks.Add(track);
			}
		}

		/// <summary>
		/// Invoked when the OpenURL dialog finished parsing a URL
		/// and the dialog was closed before the parsing could finish.
		/// </summary>
		/// <param name="tracks">The tracks representing the audio at the URL</param>
		public void OpenURL_Finished(List<TrackData> tracks)
		{
			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				this.Cursor = Cursors.Arrow;
				foreach (TrackData track in tracks)
					SettingsManager.RadioTracks.Add(track);
			}));
		}

		/// <summary>
		/// Invoked when the user clicks to add a plugin.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void AddPlugin_Clicked(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
			dialog.Title = U.T("DialogInstallPluginTitle");
			dialog.Filter = "Stoffi Plugin Package|*.spp";
			dialog.DefaultExt = ".spp";
			bool result = (bool)dialog.ShowDialog();
			if (result == true)
				PluginManager.Install(dialog.FileName, true);
		}

		/// <summary>
		/// Invoked when a key is released.
		/// </summary>
		/// <param name="key">The key that was released</param>
		/// <param name="sysKey">The system key if available</param>
		/// <param name="global">Whether or not the key was pressed from outside the application</param>
		private bool KeyReleased(Key key, Key sysKey, bool global = false)
		{
			if (!U.ListenForShortcut) return false;

			if (key == Key.System && (sysKey == Key.LeftAlt || sysKey == Key.RightAlt || sysKey == Key.LeftCtrl))
			{
				if (currentPressedKeys.Contains(sysKey)) currentPressedKeys.Remove(sysKey);
			}
			else if (key == Key.RightShift)
			{
				if (currentPressedKeys.Contains(Key.LeftShift)) currentPressedKeys.Remove(Key.LeftShift);
			}
			else
			{
				if (currentPressedKeys.Contains(key)) currentPressedKeys.Remove(key);
			}

			// toggle menu bar?
			if (sysKey == Key.LeftAlt && !global && !SettingsManager.MenuBarVisible && temporarilyShowMenuBar &&
				(currentPressedKeys.Count == 0 || (currentPressedKeys.Count == 1 && currentPressedKeys[0] == Key.LeftAlt)))
			{
				if (MenuBar.Visibility == Visibility.Visible)
					MenuBar.Visibility = Visibility.Collapsed;
				else
				{
					MenuBar.Visibility = Visibility.Visible;
				}
			}

			if (key == Key.Escape && !global && !SettingsManager.MenuBarVisible && MenuBar.Visibility == Visibility.Visible)
				MenuBar.Visibility = Visibility.Collapsed;

			return true;
		}

		/// <summary>
		/// Invoked when a key is pressed down.
		/// </summary>
		/// <param name="key">The key that was pressed</param>
		/// <param name="sysKey">The system key if available</param>
		/// <param name="global">Whether or not the key was pressed from outside the application</param>
		/// <returns>True if the key triggered a shortcut</returns>
		private bool KeyPressed(Key key, Key sysKey, bool global = false)
		{
			// catch media keys
			switch (key)
			{
				case Key.MediaPlayPause:
					if (!global && kListener.CheckHandlers()) return false;
					PlayPause();
					return true;
				case Key.MediaStop:
					MediaManager.Pause();
					return true;
				case Key.MediaNextTrack:
					if (!global && kListener.CheckHandlers()) return false;
					MediaManager.Next(true);
					return true;
				case Key.MediaPreviousTrack:
					if (!global && kListener.CheckHandlers()) return false;
					MediaManager.Previous();
					return true;
				default:
					break;
			}

			// are we listening?
			if (!U.ListenForShortcut) return false;

			// get ready to temporarily show menu bar?
			if (key == Key.LeftAlt)
				temporarilyShowMenuBar = true;
			else if (sysKey != Key.LeftAlt)
				temporarilyShowMenuBar = false;

			switch (key)
			{
				// convert right shift to left shift
				case Key.RightShift:
					if (!currentPressedKeys.Contains(Key.LeftShift)) currentPressedKeys.Add(Key.LeftShift);
					return false;
				// catch modifier keys
				case Key.LeftShift:
				case Key.LeftCtrl:
				case Key.LeftAlt:
				case Key.LWin:
				case Key.RightCtrl:
				case Key.RightAlt:
				case Key.RWin:
					if (!currentPressedKeys.Contains(key)) currentPressedKeys.Add(key);
					return false;

				// catch alt/left ctrl key when disguised as system key
				case Key.System:
					if (sysKey == Key.LeftAlt || sysKey == Key.RightAlt || sysKey == Key.LeftCtrl)
					{
						if (!currentPressedKeys.Contains(sysKey)) currentPressedKeys.Add(sysKey);
						return false;
					}
					break;

				// ignore these keys
				case Key.None:
				case Key.DeadCharProcessed:
					return false;

				default:
					break;
			}

			// turn key combination into string
			// TODO: convert Oem keys to nice strings
			String currentKey = key == Key.System ? KeyToString(sysKey) : KeyToString(key);
			String txt = GetModifiersAsText(currentPressedKeys);
			if (txt.Length > 0) txt += "+" + currentKey;
			else txt = currentKey;

			// find matching shortcut
			KeyboardShortcut sc = SettingsManager.GetKeyboardShortcut(SettingsManager.GetCurrentShortcutProfile(), txt);

			// hardcoded shortcuts
			if (!global)
			{
				if (txt == "Ctrl+A")
				{
					ViewDetails tl = GetCurrentTrackList();
					if (tl != null && tl.IsVisible)
					{
						tl.SelectAll();
						tl.Focus();
					}
				}
			}

			if (sc == null) return false;

			// skip if we received a global event but shortcut is not set to global
			// and prevent the shortcut from being executed twice if set to global
			if (sc.IsGlobal != global && kListener.CheckHandlers()) return false;

			// execute command
			if (sc.Category == "Application")
			{
				if (sc.Name == "Add track")
				{
					currentPressedKeys.Clear();
					AddFile_Click(null, null);
				}
				else if (sc.Name == "Add folder")
				{
					currentPressedKeys.Clear();
					AddFolder_Click(null, null);
				}
				else if (sc.Name == "Add playlist")
				{
					currentPressedKeys.Clear();
					AddPlaylistFile_Clicked(null, null);
				}
				else if (sc.Name == "Add radio station")
				{
					currentPressedKeys.Clear();
					AddURL_Clicked(null, null);
				}
				else if (sc.Name == "Add plugin")
				{
					currentPressedKeys.Clear();
					AddPlugin_Clicked(null, null);
				}
				else if (sc.Name == "Generate playlist")
				{
					currentPressedKeys.Clear();
					Generator_Clicked(null, null);
				}
				else if (sc.Name == "Help")
				{
					currentPressedKeys.Clear();
					Help_Clicked(null, null);
				}
				else if (sc.Name == "Minimize")
				{
					currentPressedKeys.Clear();
					Hide_Clicked(null, null);
				}
				else if (sc.Name == "Restore")
				{
					currentPressedKeys.Clear();
					TaskbarClicked(null, null);
				}
				else if (sc.Name == "Close")
					Close_Clicked(null, null);
			}
			else if (sc.Category == "MainWindow")
			{
				if (sc.Name == "Files" || sc.Name == "Library")
					SettingsManager.CurrentSelectedNavigation = "Files";
				if (sc.Name == "Video")
					SettingsManager.CurrentSelectedNavigation = "Video";
				if (sc.Name == "Visualizer")
					SettingsManager.CurrentSelectedNavigation = "Visualizer";
				else if (sc.Name == "SoundCloud")
					SettingsManager.CurrentSelectedNavigation = "SoundCloud";
				else if (sc.Name == "YouTube")
					SettingsManager.CurrentSelectedNavigation = "YouTube";
				else if (sc.Name == "Radio")
					SettingsManager.CurrentSelectedNavigation = "Radio";
				else if (sc.Name == "Jamendo")
					SettingsManager.CurrentSelectedNavigation = "Jamendo";
				else if (sc.Name == "Queue")
					SettingsManager.CurrentSelectedNavigation = "Queue";
				else if (sc.Name == "History")
					SettingsManager.CurrentSelectedNavigation = "History";
				else if (sc.Name == "Playlists")
				{
					MainContainer.Children.Remove(ControlPanel);
					MusicPanel.Visibility = System.Windows.Visibility.Visible;
					PlaybackControls.Search.Box.IsEnabled = true;
					NavigationPane.Playlists.Focus();
				}
				else if (sc.Name == "Create playlist")
				{
					MainContainer.Children.Remove(ControlPanel);
					MusicPanel.Visibility = System.Windows.Visibility.Visible;
					PlaybackControls.Search.Box.IsEnabled = true;
					NavigationPane.CreateNewPlaylist.Focus();
				}
				else if (sc.Name == "Tracklist")
				{
					MainContainer.Children.Remove(ControlPanel);
					MusicPanel.Visibility = System.Windows.Visibility.Visible;
					PlaybackControls.Search.Box.IsEnabled = true;
					ViewDetails vd = GetCurrentTrackList();
					if (vd != null)
						vd.Focus();
				}
				else if (sc.Name == "Search")
				{
					currentPressedKeys.Clear();
					PlaybackControls.Search.Box.Focus();
				}
				else if (sc.Name == "General preferences")
					OpenControlPanel(ControlPanel.Tab.General);
				else if (sc.Name == "Library sources" || sc.Name == "Music sources")
					OpenControlPanel(ControlPanel.Tab.Sources);
				else if (sc.Name == "Services")
					OpenControlPanel(ControlPanel.Tab.Services);
				else if (sc.Name == "Plugins")
					OpenControlPanel(ControlPanel.Tab.Plugins);
				else if (sc.Name == "Keyboard shortcuts")
					OpenControlPanel(ControlPanel.Tab.Shortcuts);
				else if (sc.Name == "About")
					OpenControlPanel(ControlPanel.Tab.About);
				else if (sc.Name == "Toggle details pane")
					ToggleDetailsPane(null, null);

				else if (sc.Name == "Toggle menu bar")
					ToggleMenuBar(null, null);
			}
			else if (sc.Category == "MediaCommands")
			{
				if (sc.Name == "Play or pause")
					PlayPause();

				else if (sc.Name == "Next")
					MediaManager.Next(true);

				else if (sc.Name == "Previous")
					MediaManager.Previous();

				else if (sc.Name == "Increase volume")
					PlaybackControls.VolumeSlide.Value += 1;

				else if (sc.Name == "Decrease volume")
					PlaybackControls.VolumeSlide.Value -= 1;

				else if (sc.Name == "Seek forward")
					PlaybackControls.SongProgress.Value += PlaybackControls.SongProgress.Maximum / 50;

				else if (sc.Name == "Seek backward")
					PlaybackControls.SongProgress.Value -= PlaybackControls.SongProgress.Maximum / 50;

				else if (sc.Name == "Toggle shuffle")
					PlaybackControls.Shuffle_Click(null, null);

				else if (sc.Name == "Toggle repeat")
					PlaybackControls.Repeat_Click(null, null);

				else if (sc.Name == "Add bookmark")
				{
					double pos = MediaManager.CreateBookmark();
					if (pos >= 0)
						PlaybackControls.AddBookmark(pos);
				}

				else if (sc.Name == "Jump to first bookmark")
					MediaManager.JumpToFirstBookmark();

				else if (sc.Name == "Jump to last bookmark")
					MediaManager.JumpToLastBookmark();

				else if (sc.Name == "Jump to previous bookmark")
					MediaManager.JumpToPreviousBookmark();

				else if (sc.Name == "Jump to next bookmark")
					MediaManager.JumpToNextBookmark();

				else if (sc.Name.StartsWith("Jump to bookmark "))
					MediaManager.JumpToBookmark(Convert.ToInt32(sc.Name.Substring(17)));

				else if (sc.Name == "Jump to current track")
				{
					SettingsManager.CurrentSelectedNavigation = SettingsManager.CurrentActiveNavigation;
					GetCurrentTrackList().SelectItem(SettingsManager.CurrentTrack);
				}
				else if (sc.Name == "Jump to selected track")
				{
					ViewDetails vd = GetCurrentTrackList();
					if (vd != null)
						vd.FocusItem();
				}
			}
			else if (sc.Category == "Track")
			{
				ViewDetails tl = GetCurrentTrackList();
				if (tl == null) return false;

				ListViewItem lvi = (ListViewItem)tl.ItemContainerGenerator.ContainerFromItem(tl.SelectedItem);
				if (lvi == null) return false;

				bool focused = tl.IsFocused;
				foreach (object i in tl.SelectedItems)
				{
					lvi = (ListViewItem)tl.ItemContainerGenerator.ContainerFromItem(i);
					if (lvi != null && lvi.IsFocused)
						focused = true;
				}

				if (!focused) return false;

				if (sc.Name == "Queue and dequeue")
				{
					currentPressedKeys.Clear();
					ViewDetails vd = GetCurrentTrackList();
					if (vd != null)
					{
						List<TrackData> tracks = new List<TrackData>();
						foreach (int i in vd.Config.SelectedIndices)
							tracks.Add(vd.GetItemAt(i) as TrackData);

						MediaManager.ToggleQueue(tracks);
					}
				}
				else if (sc.Name == "Play track")
				{
					currentPressedKeys.Clear();
					listMenuPlay_Click(null, null);
				}
				else if (sc.Name == "Open folder")
				{
					currentPressedKeys.Clear();
					listMenuOpenFolder_Click(null, null);
				}
				else if (sc.Name == "Remove")
				{
					currentPressedKeys.Clear();
					listMenuRemove_Click(null, null);
				}
				else if (sc.Name == "Remove from harddrive")
				{
					currentPressedKeys.Clear();
					listMenuDelete_Click(null, null);
				}
				else if (sc.Name == "Copy")
				{
					currentPressedKeys.Clear();
					listMenuCopyMove_Click(listMenuCopy, null);
				}
				else if (sc.Name == "Move")
				{
					currentPressedKeys.Clear();
					listMenuCopyMove_Click(listMenuMove, null);
				}
				else if (sc.Name == "View information")
				{
					currentPressedKeys.Clear();
					listMenuInfo_Click(null, null);
				}
				else if (sc.Name == "Share")
				{
					currentPressedKeys.Clear();
					listMenuShareSong_Click(null, null);
				}
			}
			return true;
		}

		/// <summary>
		/// Invoked when the user clicks on "Play" or "Pause" in the taskbar menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TaskbarPlayPause_Clicked(object sender, Microsoft.WindowsAPICodePack.Taskbar.ThumbnailButtonClickedEventArgs e)
		{
			PlayPause();
		}

		/// <summary>
		/// Invoked when the user clicks on "Next" in the taskbar menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TaskbarNext_Clicked(object sender, Microsoft.WindowsAPICodePack.Taskbar.ThumbnailButtonClickedEventArgs e)
		{
			MediaManager.Next(true);
		}

		/// <summary>
		/// Invoked when the user clicks on "Previous" in the taskbar menu
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TaskbarPrevious_Clicked(object sender, Microsoft.WindowsAPICodePack.Taskbar.ThumbnailButtonClickedEventArgs e)
		{
			MediaManager.Previous();
		}

		/// <summary>
		/// Invoked when the user clicks on "Equalizer"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Equalizer_Clicked(object sender, RoutedEventArgs e)
		{
			if (equalizer == null || !equalizer.Activate())
			{
				equalizer = new Equalizer();
				double l = SettingsManager.EqualizerLeft;
				double t = SettingsManager.EqualizerTop;

				if (l < 0)
					l = (System.Windows.SystemParameters.PrimaryScreenWidth / 2) - (equalizer.Width / 2);
				if (t < 0)
					t = (System.Windows.SystemParameters.PrimaryScreenHeight / 2) - (equalizer.Height / 2);

				equalizer.Owner = App.Current.MainWindow;
				equalizer.Topmost = false;
				equalizer.Show();
				equalizer.Left = l;
				equalizer.Top = t;
			}
			else
				equalizer.Activate();
		}

		/// <summary>
		/// Invoked when the user clicks on "Preferences"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Preferences_Clicked(object sender, RoutedEventArgs e)
		{
			OpenControlPanel();
		}

		/// <summary>
		/// Invoked when the user clicks "Import"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Importer_Clicked(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
			dialog.Title = "Import Configuration";
			dialog.DefaultExt = ".scx";
			dialog.Filter = "Stoffi Configuration File|*.scx;*.scf";
			bool result = (bool)dialog.ShowDialog();
			if (result == true)
				SettingsManager.Import(dialog.FileName);
		}

		/// <summary>
		/// Invoked when the user clicks "Export"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Exporter_Clicked(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
			dialog.Title = "Export Configuration";
			dialog.DefaultExt = ".scx";
			dialog.Filter = "Stoffi Configuration File|*.scx";
			bool result = (bool)dialog.ShowDialog();
			if (result == true)
			{
				SettingsManager.Export(dialog.FileName);
			}
		}

		/// <summary>
		/// Invoked when the user clicks "Generate playlist"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Generator_Clicked(object sender, RoutedEventArgs e)
		{
			GeneratePlaylist d = new GeneratePlaylist(TrackList_SearchMatch);
			d.Owner = App.Current.MainWindow;
			d.Topmost = false;
			d.Show();
		}

		/// <summary>
		/// Invoked when the user clicks on "About"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void About_Clicked(object sender, RoutedEventArgs e)
		{
			MusicPanel.Visibility = System.Windows.Visibility.Collapsed;
			if (!MainContainer.Children.Contains(ControlPanel)) MainContainer.Children.Add(ControlPanel);
			PlaybackControls.Search.Box.IsEnabled = false;
			ControlPanel.SwitchTab(Stoffi.ControlPanel.Tab.About);
		}

		/// <summary>
		/// Invoked when the user clicks on "Help"
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Help_Clicked(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.stoffiplayer.com/help?ref=stoffi");
		}

		/// <summary>
		/// Invoked when the user clicks on the view button in the toolbar.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewButton_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd == null) return;

			switch (vd.Mode)
			{
				case ViewDetails.ViewMode.Content:
					vd.Mode = ViewDetails.ViewMode.Icons;
					vd.IconSize = 96;
					break;

				case ViewDetails.ViewMode.Icons:
					vd.Mode = ViewDetails.ViewMode.List;
					break;

				case ViewDetails.ViewMode.List:
					vd.Mode = ViewDetails.ViewMode.Details;
					break;

				case ViewDetails.ViewMode.Details:
					vd.Mode = ViewDetails.ViewMode.Tiles;
					break;

				case ViewDetails.ViewMode.Tiles:
					vd.Mode = ViewDetails.ViewMode.Content;
					break;
			}

			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view extra large icons.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewExtraLargeIcons_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
			{
				vd.Mode = ViewDetails.ViewMode.Icons;
				vd.IconSize = 256;
			}
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view large icons.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewLargeIcons_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
			{
				vd.Mode = ViewDetails.ViewMode.Icons;
				vd.IconSize = 96;
			}
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view medium icons.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewMediumIcons_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
			{
				vd.Mode = ViewDetails.ViewMode.Icons;
				vd.IconSize = 48;
			}
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view small icons.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewSmallIcons_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
			{
				vd.Mode = ViewDetails.ViewMode.Icons;
				vd.IconSize = 16;
			}
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view list.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewList_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
				vd.Mode = ViewDetails.ViewMode.List;
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view details.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewDetails_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
				vd.Mode = ViewDetails.ViewMode.Details;
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view tiles.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewTiles_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
				vd.Mode = ViewDetails.ViewMode.Tiles;
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to view content.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ViewContent_Click(object sender, RoutedEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
				vd.Mode = ViewDetails.ViewMode.Content;
			RefreshViewButton();
		}

		/// <summary>
		/// Invoked when the user clicks to add a track.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void AddFile_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
			Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult result = dialog.ShowDialog();
			if (result == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.OK)
			{
				if (FilesystemManager.PathIsAdded(dialog.FileName))
					MessageBox.Show(
						U.T("MessageAlreadyAddedSource", "Message"),
						U.T("MessageAlreadyAddedSource", "Title"),
						MessageBoxButton.OK, MessageBoxImage.Information);
				else
				{
					SourceData s = new SourceData();
					s.Data = dialog.FileName;
					s.Type = SourceType.File;
					s.Include = true;
					s.Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/FileAudio.ico";
					FilesystemManager.AddSource(s);
				}
			}
		}

		/// <summary>
		/// Invoked when the user clicks to add a folder.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void AddFolder_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.SelectedPath = dialogPath;
			System.Windows.Forms.DialogResult result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				if (FilesystemManager.PathIsAdded(dialog.SelectedPath))
					MessageBox.Show(
						U.T("MessageAlreadyAddedSource", "Message"),
						U.T("MessageAlreadyAddedSource", "Title"),
						MessageBoxButton.OK, MessageBoxImage.Information);
				else
				{
					SourceData s = new SourceData();
					s.Data = dialog.SelectedPath;
					s.Type = SourceType.Folder;
					s.Include = true;
					s.Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/Folder.ico";
					FilesystemManager.AddSource(s);
				}
			}
			dialogPath = dialog.SelectedPath;
		}

		/// <summary>
		/// Invoked when the user clicks to ignore a track.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void IgnoreFile_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
			Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult result = dialog.ShowDialog();
			if (result == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.OK)
			{
				if (FilesystemManager.PathIsIgnored(dialog.FileName))
					MessageBox.Show(
						U.T("MessageAlreadyAddedSource", "Message"),
						U.T("MessageAlreadyAddedSource", "Title"),
						MessageBoxButton.OK, MessageBoxImage.Information);
				else
				{
					SourceData s = new SourceData();
					s.Data = dialog.FileName;
					s.Type = SourceType.File;
					s.Ignore = true;
					s.Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/FileAudio.ico";
					FilesystemManager.AddSource(s);
				}
			}
		}

		/// <summary>
		/// Invoked when the user clicks to ignore a folder.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		public void IgnoreFolder_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.SelectedPath = dialogPath;
			System.Windows.Forms.DialogResult result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				if (FilesystemManager.PathIsIgnored(dialog.SelectedPath))
					MessageBox.Show(
						U.T("MessageAlreadyAddedSource", "Message"),
						U.T("MessageAlreadyAddedSource", "Title"),
						MessageBoxButton.OK, MessageBoxImage.Information);
				else
				{
					SourceData s = new SourceData();
					s.Data = dialog.SelectedPath;
					s.Type = SourceType.Folder;
					s.Ignore = true;
					s.Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/Folder.ico";
					FilesystemManager.AddSource(s);
				}
			}
			dialogPath = dialog.SelectedPath;
		}

		/// <summary>
		/// Invoked when the user clicks to minimize the application to the tray.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Hide_Clicked(object sender, RoutedEventArgs e)
		{
			WindowState = System.Windows.WindowState.Minimized;
		}

		/// <summary>
		/// Invoked when the user clicks to close the application.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Close_Clicked(object sender, RoutedEventArgs e)
		{
			Close();
		}

		/// <summary>
		/// Invoked when the user removes a bookmark indicator.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Playback_RemoveBookmarkClick(object sender, EventArgs e)
		{
			Bookmark bm = sender as Bookmark;
			if (bm != null)
			{
				MediaManager.RemoveBookmark(bm.Position);
				PlaybackControls.RemoveBookmark(bm.Position);
			}
		}

		/// <summary>
		/// Invoked when the text in the search box is changed.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Playback_SearchTextChanged(object sender, EventArgs e)
		{
			string txt = PlaybackControls.Search.Text;
			string csn = SettingsManager.CurrentSelectedNavigation;
			if (csn == null) return;

			// switch to currently active navigation
			if (csn == "Video")
			{
				SettingsManager.CurrentSelectedNavigation = SettingsManager.CurrentActiveNavigation;
				PlaybackControls.Search.IsActive = true;
				PlaybackControls.Search.Text = txt;
				PlaybackControls.Search.Position = txt.Length;
				return;
			}

			ViewDetails vd = GetCurrentTrackList();
			if (vd != null)
				vd.Filter = txt;

			if (csn == "YouTube" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.YouTubeListConfig.Filter = txt;

			if (csn == "SoundCloud" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.SoundCloudListConfig.Filter = txt;

			if (csn == "Radio" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.RadioListConfig.Filter = txt;

			if (csn == "Jamendo" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.JamendoListConfig.Filter = txt;

			if (csn == "History" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.HistoryListConfig.Filter = txt;

			if (csn == "Queue" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.QueueListConfig.Filter = txt;

			if (csn == "Files" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.FileListConfig.Filter = txt;

			if (SettingsManager.SearchPolicy == SearchPolicy.Global)
			{
				foreach (PlaylistData pl in SettingsManager.Playlists)
					pl.ListConfig.Filter = txt;
			}
			else
			{
				if (csn.StartsWith("Playlist:") && SettingsManager.SearchPolicy == SearchPolicy.Partial)
					foreach (PlaylistData pl in SettingsManager.Playlists)
						pl.ListConfig.Filter = txt;
				else if (csn.StartsWith("Playlist:"))
				{
					PlaylistData p = PlaylistManager.FindPlaylist(csn.Split(new[] { ':' }, 2)[1]);
					if (p != null) p.ListConfig.Filter = txt;
				}
			}
		}

		/// <summary>
		/// Invoked when the search box is cleared.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Playback_SearchCleared(object sender, EventArgs e)
		{
			//ParentWindow.GetCurrentTrackList().Items.Filter = null;

			string csn = SettingsManager.CurrentSelectedNavigation;

			if (csn == "History" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.HistoryListConfig.Filter = "";

			if (csn == "Queue" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.QueueListConfig.Filter = "";

			if (csn == "Library" || SettingsManager.SearchPolicy == SearchPolicy.Global)
				SettingsManager.FileListConfig.Filter = "";

			if (SettingsManager.SearchPolicy == SearchPolicy.Global)
				foreach (PlaylistData pl in SettingsManager.Playlists)
					pl.ListConfig.Filter = "";
			else
			{
				if (csn.StartsWith("Playlist:") && SettingsManager.SearchPolicy == SearchPolicy.Partial)
					foreach (PlaylistData pl in SettingsManager.Playlists)
						pl.ListConfig.Filter = "";
				else if (csn.StartsWith("Playlist:"))
					PlaylistManager.FindPlaylist(csn.Split(new[] { ':' }, 2)[1]).ListConfig.Filter = "";
			}
		}

		/// <summary>
		/// Invoked when the user adds a search to a new playlist.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Playback_AddSearchToNew(object sender, EventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd == null) return;
			try
			{
				NavigationPane.AddToPlaylistQueue.Clear();
				foreach (TrackData track in vd.Items)
					NavigationPane.AddToPlaylistQueue.Add(track);
				NavigationPane.CreateNewPlaylistETB.IsInEditMode = true;
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not add search to new playlist: " + exc.Message);
				MessageBox.Show(exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Invoked when the user adds a search to an existing playlist.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Playback_AddSearch(object sender, Stoffi.Core.GenericEventArgs<string> e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd == null) return;
			try
			{
				List<object> tracks = new List<object>();
				foreach (TrackData track in vd.Items)
					tracks.Add(track);
				PlaylistManager.AddToPlaylist(tracks, e.Value);
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not add search to playlist: " + exc.Message);
				MessageBox.Show(exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Invoked when the user removes a search from a playlist.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Playback_RemoveSearch(object sender, Stoffi.Core.GenericEventArgs<string> e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd == null) return;
			try
			{
				List<TrackData> tracks = new List<TrackData>();
				foreach (TrackData track in vd.Items)
					tracks.Add(track);
				PlaylistManager.RemoveFromPlaylist(tracks, e.Value);
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not remove search from playlist: " + exc.Message);
				MessageBox.Show(exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Invoked when the user clicks on Pause/Play in the playback pane.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Playback_PausePlayClick(object sender, EventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd == null) return;

			try
			{
				if (SettingsManager.MediaState == Stoffi.Core.MediaState.Playing)
					MediaManager.Pause();
				else if (SettingsManager.CurrentTrack != null)
					MediaManager.Play();
				else
				{
					if (vd.SelectedItems.Count > 0)
					{
						TrackData track = (TrackData)vd.SelectedItem;
						MediaManager.Load(track);
						MediaManager.Play();
					}
					else if (vd.Items.Count > 0)
					{
						TrackData track = (TrackData)vd.Items[0];
						MediaManager.Load(track);
						MediaManager.Play();
					}
				}
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not handle pause/play click: " + exc.Message);
				MessageBox.Show(exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ToggleDetailsPane(object sender, RoutedEventArgs e)
		{
			SettingsManager.DetailsPaneVisible = !SettingsManager.DetailsPaneVisible;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ToggleMenuBar(object sender, RoutedEventArgs e)
		{
			SettingsManager.MenuBarVisible = !SettingsManager.MenuBarVisible;
		}

		/// <summary>
		/// Invoked when the window is resized.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ChangeSize(object sender, SizeChangedEventArgs e)
		{
			SettingsManager.WinWidth = ActualWidth;
			SettingsManager.WinHeight = ActualHeight;
			PlaybackControls.Compress(ActualWidth, MinWidth);
		}

		/// <summary>
		/// Invoked when the window is moved.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void ChangePos(object sender, EventArgs e)
		{
			SettingsManager.WinTop = Top;
			SettingsManager.WinLeft = Left;
		}

		/// <summary>
		/// Invoked when the user clicks to on the application in the taskbar.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TaskbarClicked(object sender, RoutedEventArgs e)
		{
			if (SettingsManager.FastStart && !IsVisible)
			{
				ShowInTaskbar = true;
				Show();
				trayIcon.Visibility = Visibility.Visible;
			}
			else if (WindowState != System.Windows.WindowState.Minimized)
			{
				oldWindowState = WindowState;
				WindowState = System.Windows.WindowState.Minimized;
			}
			else
			{
				WindowState = oldWindowState;
				Activate();
			}
		}

		/// <summary>
		/// Invoked when the user clicks "Show" in the tray.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrayShow_Clicked(object sender, RoutedEventArgs e)
		{
			if (WindowState == System.Windows.WindowState.Minimized)
				WindowState = oldWindowState;
			Activate();
			if (SettingsManager.FastStart)
			{
				ShowInTaskbar = true;
				Show();
				trayIcon.Visibility = Visibility.Visible;
			}
		}

		/// <summary>
		/// Invoked when the user clicks "Play" or "Pause" in the tray.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrayPlayPause_Clicked(object sender, RoutedEventArgs e)
		{
			PlayPause();
		}

		/// <summary>
		/// Invoked when the user clicks "Next" in the tray.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrayNext_Clicked(object sender, RoutedEventArgs e)
		{
			MediaManager.Next(true);
		}

		/// <summary>
		/// Invoked when the user clicks "Previous" in the tray.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrayPrevious_Clicked(object sender, RoutedEventArgs e)
		{
			MediaManager.Previous();
		}

		/// <summary>
		/// Invoked when the user clicks "Exit" in the tray.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrayExit_Clicked(object sender, RoutedEventArgs e)
		{
			//trayMenu.Visibility = System.Windows.Visibility.Hidden;
			if (SettingsManager.FastStart)
			{
				if (MessageBox.Show(U.T("MessageCloseFastStart", "Message"), U.T("MessageCloseFastStart", "Title"), MessageBoxButton.YesNo, MessageBoxImage.Warning)
					== MessageBoxResult.Yes)
				{
					forceShutdown = true;
					Close_Clicked(sender, e);
				}
			}
			else
				Close_Clicked(sender, e);
		}

		/// <summary>
		/// Updates the scan indicator
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void FilesystemManager_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			//Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			//{
			//    switch ((string)e.UserState)
			//    {
			//        case "start":
			//            ScanProgressBar.Value = 0;
			//            ScanProgressBar.IsIndeterminate = true;
			//            ScanProgress.Visibility = System.Windows.Visibility.Visible;
			//            break;

			//        case "progress":
			//            ScanProgressBar.IsIndeterminate = false;
			//            ScanProgressBar.Value = e.ProgressPercentage;
			//            break;

			//        case "done":
			//            ScanProgress.Visibility = System.Windows.Visibility.Collapsed;
			//            break;
			//    }
			//}));
		}

		/// <summary>
		/// Invoked when the window is about to be closed.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_Closing(object sender, CancelEventArgs e)
		{
			if (UpgradeManager.InProgress)
			{
				if (MessageBox.Show(U.T("MessageUpgradeInProgress", "Message"),
									U.T("MessageUpgradeInProgress", "Title"),
									MessageBoxButton.YesNo,
									MessageBoxImage.Warning,
									MessageBoxResult.No) == MessageBoxResult.No)
				{
					e.Cancel = true;
					return;
				}
			}

			if (SettingsManager.FastStart && !forceShutdown)
			{
				if (UpgradeManager.Pending)
				{
					CloseProgress cp = new CloseProgress(true);
					try
					{
						cp.Owner = this;
					}
					catch { }
					cp.ShowDialog();
				}

				ShowInTaskbar = false;
				Hide();
				e.Cancel = true;
				MediaManager.Pause();
				trayIcon.Visibility = Visibility.Collapsed;
				SettingsManager.Save();
			}
			else
			{
				forceShutdown = false;
				kListener.Dispose();
				CloseProgress cp = new CloseProgress();
				try
				{
					cp.Owner = this;
				}
				catch { }
				cp.ShowDialog();
				if (doRestart)
					Process.Start(Path.GetFullPath((new System.Uri(Assembly.GetExecutingAssembly().CodeBase)).AbsolutePath));

				U.L(LogLevel.Debug, "MAIN", "Forcing shutdown");
				Application.Current.Shutdown();
			}
		}

		/// <summary>
		/// Invoked when the window become active.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_Activated(object sender, EventArgs e)
		{
			RefreshGlassEffect();
		}

		/// <summary>
		/// Invoked when the window becomes inactive
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_Deactivated(object sender, EventArgs e)
		{
			if (!glassEffect && System.Windows.Forms.VisualStyles.VisualStyleInformation.DisplayName != "")
				Background = SystemColors.GradientInactiveCaptionBrush;

			if (etbInEdit != null)
			{
				etbInEdit.IsInEditMode = false;
				etbInEdit = null;
			}

			if (!SettingsManager.MenuBarVisible && MenuBar.Visibility == Visibility.Visible)
				MenuBar.Visibility = Visibility.Collapsed;
		}

		/// <summary>
		/// Invoked when the user moves the mouse over the window
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_MouseMove(object sender, MouseEventArgs e)
		{
			//if (GetCurrentTrackList() != null)
			//	GetCurrentTrackList().ClearDropTarget();
		}

		/// <summary>
		/// Invoked when the window changes state
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_StateChanged(object sender, EventArgs e)
		{
			oldWindowState = currentWindowState;
			currentWindowState = WindowState;
			SettingsManager.WinState = WindowState.ToString();
		}
		
		/// <summary>
		/// Invoked when a key is pressed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (KeyPressed(e.Key, e.SystemKey))
				e.Handled = true;
		}

		/// <summary>
		/// Invoked when a key is released
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_KeyUp(object sender, KeyEventArgs e)
		{
			KeyReleased(e.Key, e.SystemKey);
		}

		/// <summary>
		/// Invoked when the left mouse button is pressed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (etbInEdit != null && etbInEdit.IsInEditMode)
			{
				EditableTextBlock etb = Utilities.TryFindParent<EditableTextBlock>((DependencyObject)e.OriginalSource);
				if (etb == null || etb != etbInEdit)
				{
					etbInEdit.Done();
					etbInEdit = null;
				}
			}
			Menu mb = Utilities.TryFindParent<Menu>((DependencyObject)e.OriginalSource);
			if (mb == null && !SettingsManager.MenuBarVisible && MenuBar.Visibility == Visibility.Visible)
				MenuBar.Visibility = Visibility.Collapsed;
		}

		/// <summary>
		/// Invoked when the right mouse button is pressed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			Menu mb = Utilities.TryFindParent<Menu>((DependencyObject)e.OriginalSource);
			if (mb == null && !SettingsManager.MenuBarVisible && MenuBar.Visibility == Visibility.Visible)
				MenuBar.Visibility = Visibility.Collapsed;
		}

		/// <summary>
		/// Event handler that saves the changed width of the navigation pane
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void VerticalSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			SettingsManager.NavigationPaneWidth = NavigationColumn.Width.Value;
		}

		/// <summary>
		/// Event handler that saves the changed height of the details pane
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void HorizontalSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			SettingsManager.DetailsPaneHeight = DetailsRow.Height.Value;
		}

		/// <summary>
		/// Prevents the size of DetailsPane from become too large
		/// because of the behaviour of the GridSplitter.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void DetailsPane_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			double a = MainFrame.ActualHeight;
			double b = RootPanel.ActualHeight;
			double total = MainFrame.ActualHeight;
			double occupied = TopToolbar.ActualHeight + MusicPane.ActualHeight;
			double free = total - occupied;
			if (free < 0) free = 0;

			if (DetailsRow.Height.Value > free)
				DetailsRow.Height = new GridLength(free);
		}

		/// <summary>
		/// Invoked when a field is edited inside the details pane.
		/// Will update the file tags.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void DetailsPane_FieldEdited(object sender, FieldEditedEventArgs e)
		{
			switch (e.Field)
			{
				case "Artist":
					currentlySelectedTrack.Artist = e.Value;
					break;

				case "Album":
					currentlySelectedTrack.Album = e.Value;
					break;

				case "Title":
					currentlySelectedTrack.Title = e.Value;
					break;

				case "Genre":
					currentlySelectedTrack.Genre = e.Value;
					break;

				case "Year":
					currentlySelectedTrack.Year = Convert.ToUInt32(e.Value);
					break;
			}

			try
			{
				FilesystemManager.SaveTrack(currentlySelectedTrack);
				DetailsPane.SetField(e.Field, e.Value);
			}
			catch (Exception exc)
			{
				MessageBox.Show("Error saving: " + currentlySelectedTrack.Path + "\n\n" + exc.Message, "Could Not Write Tag", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Invoked when a source is modified by the Filesystem manager.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void FilesystemManager_SourceModified(object sender, SourceModifiedEventArgs e)
		{
			sourceModifiedDelay.Stop();

			sourceModifiedCallbacks = e.Callbacks;

			if (sourceModifiedTracks.ContainsKey(e.Track))
				sourceModifiedTracks[e.Track] = e.ModificationType;
			else
				sourceModifiedTracks.Add(e.Track, e.ModificationType);

			sourceModifiedDelay.Start();
		}

		/// <summary>
		/// Invoked when a source has been added.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void FilesystemManager_SourceAdded(object sender, SourcesModifiedEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				SettingsManager.Sources.Add(e.Source);
			}));
		}

		/// <summary>
		/// Invoked when a source has been removed.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void FilesystemManager_SourceRemoved(object sender, SourcesModifiedEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				SettingsManager.Sources.Remove(e.Source);
			}));
		}
		
		/// <summary>
		/// Invoked when something happens to a track.
		/// Updates the library/queue/history time and the TrackList(s) of the parent.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Event arguments</param>
		private void FilesystemManager_TrackModified(object sender, PropertyChangedEventArgs e)
		{
			TrackData track = sender as TrackData;
			if (!pathsThatWasChanged.Contains(track.Path))
			{
				pathsThatWasChanged.Add(track.Path);
				resortDelay.Stop();
				resortDelay.Start();
			}

			if (e.PropertyName != "RawLength") return;

			// update time trackers
			LibraryTime += track.diff;

			if (SettingsManager.QueueTracks.Contains(track))
				QueueTime += track.diff;

			if (SettingsManager.HistoryTracks.Contains(track))
				HistoryTime += track.diff;

			foreach (PlaylistData p in SettingsManager.Playlists)
				if (p.Tracks.Contains(track))
					p.Time += track.diff;

			if (GetCurrentTrackList() != null && GetCurrentTrackList().Items.Contains(track))
			{
				Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
				{
					if (SettingsManager.CurrentSelectedNavigation == "Files")
						InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)LibraryTime));
					else if (SettingsManager.CurrentSelectedNavigation == "History")
						InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)HistoryTime));
					else if (SettingsManager.CurrentSelectedNavigation == "Queue")
						InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)QueueTime));
					else if (SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
					{
						String playlistName = SettingsManager.CurrentSelectedNavigation.Split(new[]{':'},2)[1];
						PlaylistData pl = PlaylistManager.FindPlaylist(playlistName);
						InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)pl.Time));
					}
				}));
			}
		}

		/// <summary>
		/// Invoked when a file or folder has been renamed.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void FilesystemManager_PathRenamed(object sender, RenamedEventArgs e)
		{
			foreach (TrackData track in SettingsManager.FileTracks)
				if (track.Path.StartsWith(e.OldName))
					track.Path = track.Path.Replace(e.OldName, e.Name);

			foreach (TrackData track in SettingsManager.HistoryTracks)
				if (track.Path.StartsWith(e.OldName))
					track.Path = track.Path.Replace(e.OldName, e.Name);

			foreach (TrackData track in SettingsManager.QueueTracks)
				if (track.Path.StartsWith(e.OldName))
					track.Path = track.Path.Replace(e.OldName, e.Name);

			foreach (PlaylistData playlist in SettingsManager.Playlists)
				foreach (TrackData track in playlist.Tracks)
					if (track.Path.StartsWith(e.OldName))
						track.Path = track.Path.Replace(e.OldName, e.Name);

			foreach (SourceData src in SettingsManager.Sources)
				if (src.Data.StartsWith(e.OldName))
					src.Data.Replace(e.OldName, e.Name);
		}

		/// <summary>
		/// Invoked when a change is detected in a file.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void FilesystemManager_PathModified(object sender, PathModifiedEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				foreach (TrackData track in SettingsManager.FileTracks)
					if (track.Path == e.Path)
						FilesystemManager.UpdateTrack(track);

				foreach (TrackData track in SettingsManager.QueueTracks)
					if (track.Path == e.Path)
						FilesystemManager.UpdateTrack(track);

				foreach (TrackData track in SettingsManager.HistoryTracks)
					if (track.Path == e.Path)
						FilesystemManager.UpdateTrack(track);

				foreach (PlaylistData playlist in SettingsManager.Playlists)
					foreach (TrackData track in playlist.Tracks)
						if (track.Path == e.Path)
							FilesystemManager.UpdateTrack(track);
			}));
		}

		/// <summary>
		/// Invoked when a property of the settings manager changes.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void SettingsManager_PropertyChanged(object sender, PropertyChangedWithValuesEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				switch (e.PropertyName)
				{
					case "DetailsPaneHeight":
						DetailsRow.Height = new GridLength(SettingsManager.DetailsPaneHeight);
						break;

					case "DetailsPaneVisible":
						UpdateVisibility("details");
						break;

					case "MenuBarVisible":
						UpdateVisibility("menubar");
						break;

					case "NavigationPaneWidth":
						NavigationColumn.Width = new GridLength(SettingsManager.NavigationPaneWidth);
						break;

					case "Language":
						CultureInfo ci = CultureInfo.GetCultureInfo(SettingsManager.Language);
						LanguageContext.Instance.Culture = ci;
						Thread.CurrentThread.CurrentUICulture = ci;
						RefreshStrings();
						break;

					case "CurrentSelectedNavigation":
						SwitchNavigation();
						break;

					case "CurrentTrack":
						if (SettingsManager.CurrentSelectedNavigation == "Video")
							if (SettingsManager.CurrentTrack == null)
							{
								InfoPaneTitle.Text = U.T("PlaybackEmpty");
								InfoPaneSubtitle.Text = "";
							}
							else
							{
								InfoPaneTitle.Text = SettingsManager.CurrentTrack.Title;
								InfoPaneSubtitle.Text = SettingsManager.CurrentTrack.Artist;
							}
						break;

					case "CurrentVisualizer":
						if (SettingsManager.CurrentSelectedNavigation == "Visualizer")
						{
							InfoPaneTitle.Text = VisualizerContainer.Title;
							InfoPaneSubtitle.Text = VisualizerContainer.Description;
						}
						UpdateSelectedVisualizer();
						break;

					case "MediaState":
						switch (SettingsManager.MediaState)
						{
							case Core.MediaState.Playing:
								if (taskbarPlay != null) taskbarPlay.Icon = Properties.Resources.Pause;
								if (trayMenuPlay != null) trayMenuPlay.Header = "Pause";
								if (jumpTaskPlay != null) jumpTaskPlay.Title = "Pause";
								if (YouTubeManager.IsYouTube(SettingsManager.CurrentTrack) && VideoContainer != null)
								{
									if (VideoContainer.BrowserVisibility != Visibility.Visible)
										VideoContainer.BrowserVisibility = Visibility.Visible;
								}
								else if (VideoContainer != null)
								{
									if (VideoContainer.BrowserVisibility != Visibility.Collapsed)
										VideoContainer.BrowserVisibility = Visibility.Collapsed;
								}
								break;

							case Core.MediaState.Paused:
							case Core.MediaState.Stopped:
								if (taskbarPlay != null) taskbarPlay.Icon = Properties.Resources.Play;
								if (trayMenuPlay != null) trayMenuPlay.Header = "Play";
								if (jumpTaskPlay != null) jumpTaskPlay.Title = "Play";
								break;
						}
						break;

					case "QueueListConfig":
						QueueTracks.Config = SettingsManager.QueueListConfig;
						SettingsManager.QueueListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;

					case "YouTubeListConfig":
						SettingsManager.YouTubeListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;

					case "SoundCloudListConfig":
						SettingsManager.SoundCloudListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;

					case "RadioListConfig":
						RadioTracks.Config = SettingsManager.RadioListConfig;
						SettingsManager.RadioListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;

					case "JamendoListConfig":
						SettingsManager.JamendoListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;

					case "DiscListConfig":
						DiscTracks.Config = SettingsManager.DiscListConfig;
						SettingsManager.DiscListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;

					case "HistoryListConfig":
						HistoryTracks.Config = SettingsManager.HistoryListConfig;
						SettingsManager.HistoryListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;

					case "FileListConfig":
						FileTracks.Config = SettingsManager.FileListConfig;
						SettingsManager.FileListConfig.PropertyChanged += new PropertyChangedEventHandler(ViewDetailsConfig_PropertyChanged);
						break;
				}
			}));
		}

		/// <summary>
		/// Invoked when the UI thread needs to perform a modification on a track collection.
		/// </summary>
		/// <param name="sender">The track collection</param>
		/// <param name="e">The event data</param>
		private void ServiceManager_ModifyTracks(object sender, ModifiedEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				try
				{
					ObservableCollection<TrackData> collection = sender as ObservableCollection<TrackData>;
					List<TrackData> tracks = e.Data as List<TrackData>;
					if (collection != null && tracks != null)
					{
						switch (e.Type)
						{
							case ModifyType.Added:
								foreach (TrackData track in tracks)
									if (!U.ContainsPath(collection, track.Path))
										collection.Add(track);
								break;

							case ModifyType.Removed:
								List<TrackData> tracksToRemove = new List<TrackData>();
								foreach (TrackData track in tracks)
								{
									foreach (TrackData t in collection)
										if (t.Path == track.Path)
										{
											tracksToRemove.Add(t);
											break;
										}
								}
								foreach (TrackData track in tracksToRemove)
									collection.Remove(track);
								break;
						}
					}
				}
				catch (Exception exc)
				{
					U.L(LogLevel.Warning, "MAIN", "Could not modify track collection: " + exc.Message);
				}
			}));
		}

		/// <summary>
		/// Invoked when the PluginManager indicates that the VisualizerSelector need to be updated.
		/// </summary>
		/// <remarks>
		/// Ensures that the VisualizerSelector contains all installed
		/// plugins of type Visualizer and only them.
		/// </remarks>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void PluginManager_RefreshVisualizerSelector(object sender, EventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				for (int i = 0; i < PluginManager.VisualizerSelector.Count; i++)
				{
					PluginItem v = PluginManager.VisualizerSelector[i];
					if (v.ID != null && PluginManager.GetListItem(v.ID) == null)
					{
						PluginManager.VisualizerSelector.RemoveAt(i--);
					}
				}

				foreach (PluginItem p in SettingsManager.Plugins)
				{
					PluginItem item = null;
					if (p.Type == PluginType.Visualizer)
					{
						foreach (PluginItem v in PluginManager.VisualizerSelector)
							if (v.ID != null && v.ID == p.ID)
							{
								item = v;
								break;
							}
						if (item == null)
						{
							PluginManager.VisualizerSelector.Add(new PluginItem()
							{
								ID = p.ID,
								Name = p.Name,
								Type = p.Type,
								Description = p.Description,
								URL = p.URL,
								Author = p.Author,
								Disabled = true
							});
						}
					}
				}

				if (VisualizerList.SelectedItem == null && VisualizerList.Items.Count > 0)
					VisualizerList.SelectedIndex = 0;
			}));
		}

		/// <summary>
		/// Invoked when a plugin has been installed.
		/// </summary>
		/// <remarks>
		/// Will add the plugin to the list of installed plugins for management.
		/// </remarks>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void PluginManager_Installed(object sender, PluginEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				string icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/";
				switch (e.Plugin.Type)
				{
					case PluginType.Visualizer:
						icon += "Visualizer.ico";
						break;

					case PluginType.Filter:
						icon += "MidiSynth.ico";
						break;

					default:
						icon += "Package.ico";
						break;
				}
				if (PluginManager.GetListItem(e.Plugin.ID) == null)
				{
					DateTime installed = DateTime.Now;
					foreach (PluginSettings s in SettingsManager.PluginSettings)
					{
						if (s.PluginID == e.Plugin.ID && s.Installed != null)
						{
							installed = s.Installed;
							break;
						}
					}

                    SettingsManager.Plugins.Add(new PluginItem()
                    {
                        ID = e.Plugin.ID,
                        Name = e.Plugin.T("Name"),
                        Description = e.Plugin.T("Description"),
                        Author = e.Plugin.Author,
                        URL = e.Plugin.Website,
                        Type = e.Plugin.Type,
                        Version = e.Plugin.Version,
                        Installed = installed,
						Image = icon,
                        Icon = icon,
                        Disabled = true
                    });
                }
            }));
        }

		/// <summary>
		/// Invoked when a plugin has been uninstalled.
		/// </summary>
		/// <remarks>
		/// Will remove the plugin from the list of installed plugins for management.
		/// </remarks>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void PluginManager_Uninstalled(object sender, PluginEventArgs e)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				PluginItem p;
				if ((p = PluginManager.GetListItem(e.Plugin.ID)) != null)
					SettingsManager.Plugins.Remove(p);
			}));
		}

		/// <summary>
		/// Invoked when an error occured in the YouTube player
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="message">The error message</param>
		private void YouTube_ErrorOccured(object sender, string message)
		{
			int errorCode = Convert.ToInt32(message);
			if (showMediaError)
			{
				showMediaError = false;
				switch (errorCode)
				{
					case 2:
						MessageBox.Show(
							U.T("MessageYouTubeBadParams", "Message"),
							U.T("MessageYouTubeBadParams", "Title"),
							MessageBoxButton.OK,
							MessageBoxImage.Error
						);
						break;

					case 100:
						MessageBox.Show(
							U.T("MessageYouTubeNotFound", "Message"),
							U.T("MessageYouTubeNotFound", "Title"),
							MessageBoxButton.OK,
							MessageBoxImage.Error
						);
						break;

					case 101:
					case 150: // restricted
						MediaManager.Next(true);
						break;

					default:
						MessageBox.Show(
							String.Format(U.T("MessageYouTubeUnknown", "Message"), message),
							U.T("MessageYouTubeUnknown", "Title"),
							MessageBoxButton.OK,
							MessageBoxImage.Error
						);
						break;
				}
				MediaManager.Stop();
			}
		}

		/// <summary>
		/// Invoked when the YouTube player complains that there is no flash installed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void YouTube_NoFlashDetected(object sender, EventArgs e)
		{
			if (showMediaError)
			{
				showMediaError = false;
				YouTubeManager.HasFlash = false;
				MessageBoxResult r = MessageBox.Show(
							U.T("MessageNoFlash", "Message"),
							U.T("MessageNoFlash", "Title"),
							MessageBoxButton.YesNo,
							MessageBoxImage.Error
						);
				if (r == MessageBoxResult.Yes)
				{
					System.Diagnostics.Process.Start("IEXPLORE.EXE", "http://get.adobe.com/flashplayer");
				}
				MediaManager.Stop();
			}

		}

		/// <summary>
		/// Invoked when the YouTube player is ready
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void YouTube_PlayerReady(object sender, EventArgs e)
		{
			YouTubeManager.HasFlash = true;
			VideoContainer.LoadYouTube();
		}

		/// <summary>
		/// Adds or removes a track from the library collection
		/// </summary>
		/// <param name="sender">The sender of the event (the timer)</param>
		/// <param name="e">The event data</param>
		private void SourceModifiedDelay_Tick(object sender, EventArgs e)
		{
			sourceModifiedDelay.Stop();

			Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
			{
				//ScanProgressBar.IsIndeterminate = true;
				//ScanProgress.Visibility = System.Windows.Visibility.Visible;
				Cursor = Cursors.AppStarting;
			}));

			ThreadStart GUIScanThread = delegate()
			{
				try
				{
					// copy the tracks into two lists
					SortedSet<string> trackPaths = new SortedSet<string>();
					List<TrackData> tracksToAdd = new List<TrackData>();
					SortedList<string, TrackData> tracksToRemove = new SortedList<string, TrackData>();
					List<TrackData> tracksToUpdate = new List<TrackData>();

					foreach (DictionaryEntry de in sourceModifiedTracks)
					{
						SourceModificationType modType = (SourceModificationType)de.Value;
						TrackData track = (TrackData)de.Key;
						if (modType == SourceModificationType.Added)
							tracksToAdd.Add(track);
						else if (modType == SourceModificationType.Removed)
						{
							if (!tracksToRemove.ContainsKey(track.Path))
								tracksToRemove.Add(track.Path, track);
						}
						else
							tracksToUpdate.Add(de.Key as TrackData);
					}
					sourceModifiedTracks.Clear();

					// copy the observable collections so we can work on them
					// outside the gui thread
					ObservableCollection<TrackData> files = new ObservableCollection<TrackData>();
					foreach (TrackData t in SettingsManager.FileTracks)
					{
						files.Add(t);
						trackPaths.Add(t.Path);
					}

					// add tracks
					for (int j = 0; j < tracksToAdd.Count; j++)
					{
						TrackData track = tracksToAdd[j];
						if (trackPaths.Contains(track.Path))
							tracksToAdd.RemoveAt(j--);
						else
							files.Add(track);
					}

					// update source for file list
					U.L(LogLevel.Debug, "MAIN", "Adding tracks to GUI list");
					//DateTime start = DateTime.Now;
					Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
					{
						SettingsManager.FileTracks = files;
						SettingsManager.FileTracks.CollectionChanged += new NotifyCollectionChangedEventHandler(LibraryTracks_CollectionChanged);
						if (FileTracks != null)
							FileTracks.ItemsSource = files;
						if (SettingsManager.CurrentSelectedNavigation == "Files")
							InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), SettingsManager.FileTracks.Count);
						//ScanProgressBar.IsIndeterminate = false;
						//ScanProgressBar.Value = 0;
					}));

					// remove tracks
					//int numTracks = tracksToRemove.Count + tracksToAdd.Count + tracksToUpdate.Count;
					//double progressDelta = 100.0 / numTracks;
					//if (Double.IsInfinity(progressDelta)) progressDelta = 0;
					//double progress = 0;
					//double removeDelta = progressDelta * tracksToRemove.Count;
					if (tracksToRemove.Count > 0)
					{
						// remove if current track
						for (int i = 0; i < tracksToRemove.Count; i++)
						{
							TrackData track = tracksToRemove.Values[i];
							if (SettingsManager.CurrentTrack.Path == track.Path)
								SettingsManager.CurrentTrack = null;
						}

						//double lists = SettingsManager.Playlists.Count + 3;
						//double trackDelta = progressDelta / lists;
						//double listDelta = removeDelta / lists;
						double listDelta = 1;
						foreach (PlaylistData p in SettingsManager.Playlists)
							RemoveTracks(tracksToRemove, p.Tracks, listDelta);
						RemoveTracks(tracksToRemove, SettingsManager.QueueTracks, listDelta);
						RemoveTracks(tracksToRemove, SettingsManager.HistoryTracks, listDelta);
						RemoveTracks(tracksToRemove, SettingsManager.FileTracks, listDelta);
					}
					//progress = removeDelta;
					//if (showBar)
					//    Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
					//    {
					//        ScanProgressBar.Value = progress;
					//    }));

					// update tracks
					//U.L(LogLevel.Debug, "MAIN", "Updating tracks");
					for (int j = 0; j < tracksToAdd.Count; j++)
					{
						TrackData track = tracksToAdd[j];
						if (U.IsClosing) return;
						FilesystemManager.UpdateTrack(track, false);
						//if (showBar && j % 100 == 0)
						//{
						//    Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
						//    {
						//        ScanProgressBar.Value = progress;
						//    }));
						//}
						//progress += progressDelta;
					}
					for (int j = 0; j < tracksToUpdate.Count; j++)
					{
						TrackData track = tracksToUpdate[j];
						if (U.IsClosing) return;
						FilesystemManager.UpdateTrack(track, false);
						//if (j % 100 == 0)
						//{
						//    Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
						//    {
						//        ScanProgressBar.Value = progress;
						//    }));
						//}
						//progress += progressDelta;
					}
					//TimeSpan ts = (DateTime.Now - start);
					//double time = Math.Round(ts.TotalMilliseconds / numTracks, 2);
					//U.L(LogLevel.Debug, "FILESYSTEM", String.Format("Scanning took {0} seconds, an average of {1} ms/track", Math.Round(ts.TotalSeconds, 2), time));

					Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
					{
						Cursor = Cursors.Arrow;
						//ScanProgress.Visibility = System.Windows.Visibility.Collapsed;
					}));

					// call callbacks
					Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate()
					{
						foreach (KeyValuePair<ScannerCallback, object> pair in sourceModifiedCallbacks)
						{
							ScannerCallback callback = pair.Key;
							object callbackParams = pair.Value;
							if (callback != null)
								callback(callbackParams);
						}
						sourceModifiedCallbacks.Clear();
					}));
				}
				catch (Exception exc)
				{
					U.L(LogLevel.Warning, "MAIN", "Error occured in meta scanner: " + exc.Message);
					U.L(LogLevel.Warning, "MAIN", "Restarting meta scanner.");
					SourceModifiedDelay_Tick(sender, e);
				}
			};
			Thread gs_thread = new Thread(GUIScanThread);
			gs_thread.Name = "GUI scan updater";
			gs_thread.IsBackground = true;
			gs_thread.Priority = ThreadPriority.Lowest;
			gs_thread.Start();
		}

		/// <summary>
		/// Reapplies the sorting on a TrackList when a track was changed.
		/// 
		/// This method is called from a Timer in order to prevent many calls from being made in a short period of time.
		/// </summary>
		/// <param name="sender">The sender of the event (the timer)</param>
		/// <param name="e">The event data</param>
		private void ResortDelay_Tick(object sender, EventArgs e)
		{
			resortDelay.IsEnabled = false;
			ResortTracklist(FileTracks, pathsThatWasChanged);
			ResortTracklist(HistoryTracks, pathsThatWasChanged);
			ResortTracklist(QueueTracks, pathsThatWasChanged);
			foreach (DictionaryEntry o in PlaylistTrackLists)
				ResortTracklist((ViewDetails)o.Value, pathsThatWasChanged);
			pathsThatWasChanged.Clear();
		}
		
		/// <summary>
		/// Invoked when something happens to the library collection.
		/// Updates the library time and the library TrackList.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Event arguments</param>
		private void LibraryTracks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			LibraryTime = 0;
			foreach (TrackData t in SettingsManager.FileTracks)
				LibraryTime += t.Length;

			if (SettingsManager.CurrentSelectedNavigation == "Files")
			{
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
				{
					InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), SettingsManager.FileTracks.Count);
					InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)LibraryTime));
				}));
			}
		}

		/// <summary>
		/// Invoked when something happens to the radio collection.
		/// Updates the radio time and the radio TrackList.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Event data</param>
		private void RadioTracks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (SettingsManager.CurrentSelectedNavigation == "Radio")
			{
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
				{
					InfoPaneTracks.Text = String.Format(U.T("HeaderStations"), SettingsManager.RadioTracks.Count);
					InfoPaneDuration.Text = "";
				}));
			}
		}

		/// <summary>
		/// Invoked when something happens to the queue collection.
		/// Updates the queue time and the queue TrackList.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Event arguments</param>
		private void QueueTracks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			QueueTime = 0;
			foreach (TrackData t in SettingsManager.QueueTracks)
				QueueTime += t.Length;

			if (SettingsManager.CurrentSelectedNavigation == "Queue")
			{
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
				{
					// are all selected items removed?
					bool allRemoved = true;
					foreach (TrackData selectedTrack in QueueTracks.SelectedItems)
					{
						if (!e.OldItems.Contains(selectedTrack))
						{
							allRemoved = false;
							break;
						}
					}

					InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), SettingsManager.QueueTracks.Count);
					InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)QueueTime));

					// select the last item that is not being removed
					if (allRemoved && QueueTracks.SelectedItems.Count > 0)
					{
						if (QueueTracks.Items.Count > 0)
						{
							int indexToSelect = e.OldStartingIndex >= QueueTracks.Items.Count ? QueueTracks.Items.Count - 1 : e.OldStartingIndex;
							QueueTracks.FocusAndSelectItem(indexToSelect);
						}
					}
				}));
			}
		}

		/// <summary>
		/// Invoked when something happens to the history collection.
		/// Updates the history time and the history TrackList.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Event arguments</param>
		private void HistoryTracks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			HistoryTime = 0;
			foreach (TrackData t in SettingsManager.HistoryTracks)
				HistoryTime += t.Length;

			if (SettingsManager.CurrentSelectedNavigation == "History")
			{
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
				{
					InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), SettingsManager.HistoryTracks.Count);
					InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)HistoryTime));
				}));
			}
		}

		/// <summary>
		/// Invoked when something happens to a playlist collection.
		/// Updates the playlist time and track count.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">The event data</param>
		private void PlaylistTracks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			ObservableCollection<TrackData> tracks = sender as ObservableCollection<TrackData>;

			if (tracks != null)
			{
				foreach (PlaylistData p in SettingsManager.Playlists)
				{
					if (p.Tracks == tracks)
					{
						double time = 0;
						foreach (TrackData t in tracks)
							time += t.Length;

						if (SettingsManager.CurrentSelectedNavigation == "Playlist:"+p.Name)
						{
							Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
							{
								InfoPaneTracks.Text = String.Format(U.T("HeaderTracks"), tracks.Count);
								InfoPaneDuration.Text = U.TimeSpanToLongString(new TimeSpan(0, 0, (int)time));
							}));
						}

						break;
					}
				}
			}
		}

		/// <summary>
		/// Invoked when the selection of a track list changes
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshDetails();
		}

		/// <summary>
		/// Invoked when a tracklist gets focus.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrackList_GotFocus(object sender, EventArgs e)
		{
			RefreshDetails();
		}

		/// <summary>
		/// Evaluates if a given track matches a specific string
		/// </summary>
		/// <param name="item">The track to match</param>
		/// <param name="needles">The string to match the track against</param>
		/// <returns>true if the track matches the string, otherwise false</returns>
		private bool TrackList_SearchMatch(object item, string needles)
		{
			if (needles == null || needles == "") return true;

			TrackData track = (TrackData)item;

			String artist = track.Artist == null ? "" : track.Artist.ToLower();
			String album = track.Album == null ? "" : track.Album.ToLower();
			String title = track.Title == null ? "" : track.Title.ToLower();
			String genre = track.Genre == null ? "" : track.Genre.ToLower();
			String year = track.Year.ToString().ToLower();
			String path = track.Path.ToLower();

			foreach (String needle in needles.ToLower().Split(' '))
			{
				if (!artist.Contains(needle) &&
					!album.Contains(needle) &&
					!title.Contains(needle) &&
					!genre.Contains(needle) &&
					!year.Contains(needle) &&
					!path.Contains(needle))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Invoked when files are dropped on a track list
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrackList_FilesDropped(object sender, FileDropEventArgs e)
		{
			ViewDetails vd = GetCurrentTrackList();
			if (vd == null || vd == HistoryTracks) return;

			foreach (string path in e.Paths)
			{
				if (Directory.Exists(path) || MediaManager.IsSupported(path))
				{
					// create callback params
					List<object> l = new List<object>();
					l.Add(vd as object);
					l.Add(e as object);
					l.Add(path as object);

					if (FilesystemManager.PathIsAdded(path))
						TrackList_MoveNewlyAdded(l as object);
					else
						FilesystemManager.AddSource(path, TrackList_MoveNewlyAdded, l as object);
				}
			}
		}

		/// <summary>
		/// Invoked when a new source has been added after a file drop
		/// after the scan has finished.
		/// </summary>
		/// <param name="param">
		/// A list of three objects:
		///  * The ViewDetails that was dropped upon
		///  * The FileDropEventArgs containing the event data for the drop
		///  * The string of the path that was scanned
		///  </param>
		private void TrackList_MoveNewlyAdded(object param)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
			{
				// extract parameters
				List<object> l = param as List<object>;
				ViewDetails vd = l[0] as ViewDetails;
				FileDropEventArgs e = l[1] as FileDropEventArgs;
				string path = l[2] as string;

				// remove sorting but keep positions
				vd.ClearSort(true);

				int p = e.Position;

				if (vd == FileTracks)
				{
					for (int i = 0; i < SettingsManager.FileTracks.Count; i++)
					{
						TrackData t = SettingsManager.FileTracks[i];
						if (t.Path.StartsWith(path) && i != (i >= p ? p : p - 1))
						{
							SettingsManager.FileTracks.Remove(t);
							if (i >= p) // move back
							{
								SettingsManager.FileTracks.Insert(p++, t);
							}
							else // move forward
							{
								SettingsManager.FileTracks.Insert(p - 1, t);
								i--;
							}
						}
					}
				}
				else
				{
					ObservableCollection<TrackData> source = GetCurrentTrackCollection();
					if (source != null)
					{
						List<TrackData> libTracks = U.GetTracks(SettingsManager.FileTracks, path);

						if (vd == QueueTracks)
						{
							MediaManager.Queue(libTracks, e.Position);
						}
						else if (SettingsManager.CurrentSelectedNavigation.StartsWith("Playlist:"))
						{
							String playlistName = SettingsManager.CurrentSelectedNavigation.Split(new[] { ':' }, 2)[1];
							PlaylistManager.AddToPlaylist(libTracks, playlistName, e.Position);
						}
					}
					// find tracks in library
					// each track:
					//   if addSource:
					//	 check for tracks in track list
					//	 if found: move and break
					//   insert
					Console.WriteLine("check each track, move or insert to queue/playlist at " + e.Position);
				}
			}));
		}

		/// <summary>
		/// Invoked when an item needs to be moved in a track list
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void TrackList_MoveItem(object sender, MoveItemEventArgs e)
		{
			try
			{
				TrackData track = e.Item as TrackData;
				ViewDetails vd = sender as ViewDetails;
				ObservableCollection<TrackData> tracks = GetCurrentTrackCollection();
				int i = tracks.IndexOf(track);
				if (i == e.Position) return; // already at position
				int j = i < e.Position ? e.Position - 1 : e.Position;
				tracks.Remove(track);
				tracks.Insert(j, track);
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Warning, "MAIN", "Could not move track list item: " + exc.Message);
			}
		}

		/// <summary>
		/// Invoked when the upgrade button is clicked.
		/// Will restart the application.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void UpgradeButton_Click(object sender, RoutedEventArgs e)
		{
			Restart();
		}

		/// <summary>
		/// Invoked when something is dropped on the window.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Window_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] paths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
				foreach (string path in paths)
					if (Path.GetExtension(path) == ".spp")
						PluginManager.Install(path, true);
			}
		}

		/// <summary>
		/// Invoked when the user changes which visualizer should currently be active.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void VisualizerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			PluginItem v = VisualizerList.SelectedItem as PluginItem;
			if (v != null)
				SettingsManager.CurrentVisualizer = v.ID;
		}

		/// <summary>
		/// Invoked when the number of hidden toolbar buttons changes.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void LeftToolbarButtons_HidingChanged(object sender, EventArgs e)
		{
			ContextMenu menu = ToolbarMoreButton.ArrowMenu;

			menu.Items.Clear();

			// move content from buttons into the menu
			foreach (UIElement child in LeftToolbarButtons.HiddenChildren)
			{
				ToolbarButton button = child as ToolbarButton;
				if (button != null)
				{
					MenuItem mi = new MenuItem();

					// copy content
					object o = button.Content;
					StackPanel sp = o as StackPanel;
					TextBlock tb = o as TextBlock;
					if (sp != null && sp.Children.Count > 0)
					{
						Image img = sp.Children[0] as Image;
						tb = sp.Children[1] as TextBlock;
						if (tb != null)
							mi.Header = tb.Text;
						else
							mi.Header = "---";

						if (img != null)
							mi.Icon = new Image() { Source = img.Source, Width = 16, Height = 16 };
					}
					else if (tb != null)
						mi.Header = tb.Text;
					else if (o is string)
						mi.Header = (string)o;
					else
						mi.Header = "---";

					if (button.ArrowMenu != null)
						Utilities.CopyMenu(button.ArrowMenu.Items, mi.Items);
					else
						mi.Click += (object s, RoutedEventArgs rea) => { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };

					menu.Items.Add(mi);
				}
			}
		}

		/// <summary>
		/// Invoked when the user changes system preferences.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			if (e.Category == UserPreferenceCategory.Locale)
			{
				CultureInfo.CurrentCulture.ClearCachedData();
				RefreshStrings();
			}
		}

		#endregion

		#endregion

		#region Events

		/// <summary>
		/// Occurs when a property has been changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		#endregion
	}
}