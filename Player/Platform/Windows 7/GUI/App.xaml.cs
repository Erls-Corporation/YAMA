/***
 * App.xaml.cs
 * 
 * The first code that runs during startup.
 * Checks for other running instances and takes
 * care of communication with other instances.
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

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

using Un4seen.Bass;
using BASSSync = Un4seen.Bass.BASSSync;
using BASSInput = Un4seen.Bass.BASSInput;
using BassActive = Un4seen.Bass.BASSActive;

using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.Win32;
using GlassLib;
using Tomers.WPF.Localization;

using Stoffi.Core;

namespace Stoffi
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		#region Members

		private KeyboardListener kListener = new KeyboardListener();
		private string identifier = "165de6c3da87d45d5f5a3c2d75alpha";
		private string languageFolder = @"Languages\";

		#endregion

		#region Constructor

		/// <summary>
		/// Creates an instance of the App.
		/// </summary>
		App()
		{
			U.Check();
			U.Level = LogLevel.Debug;
			U.L(LogLevel.Information, "APP", "Starting up");

			string a = Assembly.GetExecutingAssembly().CodeBase;
			Uri b = new Uri(a);
			string c = b.AbsolutePath;
			string d = Uri.EscapeDataString(c);

			string baseFolder = U.BasePath;
			languageFolder = Path.Combine(baseFolder, languageFolder);

			U.L(LogLevel.Debug, "APP", "Loading languages from " + languageFolder);

			if (!Directory.Exists(languageFolder))
				languageFolder = Path.Combine(baseFolder, @"..\..\Platform\Windows 7\GUI\Languages");

			LanguageDictionary.RegisterDictionary(
				CultureInfo.GetCultureInfo("en-US"),
				new XmlLanguageDictionary(Path.Combine(languageFolder, "en-US.xml")));

			LanguageDictionary.RegisterDictionary(
				CultureInfo.GetCultureInfo("sv-SE"),
				new XmlLanguageDictionary(Path.Combine(languageFolder, "sv-SE.xml")));

			LanguageDictionary.RegisterDictionary(
				CultureInfo.GetCultureInfo("zh-CN"),
				new XmlLanguageDictionary(Path.Combine(languageFolder, "zh-CN.xml")));

			LanguageDictionary.RegisterDictionary(
				CultureInfo.GetCultureInfo("pt-BR"),
				new XmlLanguageDictionary(Path.Combine(languageFolder, "pt-BR.xml")));

			LanguageDictionary.RegisterDictionary(
				CultureInfo.GetCultureInfo("de-DE"),
				new XmlLanguageDictionary(Path.Combine(languageFolder, "de-DE.xml")));

			LanguageDictionary.RegisterDictionary(
				CultureInfo.GetCultureInfo("it-IT"),
				new XmlLanguageDictionary(Path.Combine(languageFolder, "it-IT.xml")));

			string lang = Stoffi.Properties.Settings.Default.Language;
			if (lang == null)
				lang = Thread.CurrentThread.CurrentUICulture.IetfLanguageTag;
			CultureInfo ci = CultureInfo.GetCultureInfo(lang);
			U.L(LogLevel.Debug, "APP", String.Format("Setting culture: {0} ({1})", ci.TwoLetterISOLanguageName, ci.IetfLanguageTag));
			LanguageContext.Instance.Culture = ci;
			Thread.CurrentThread.CurrentUICulture = ci;

			// check arguments
			string[] arguments = Environment.GetCommandLineArgs();
			if (arguments.Length == 3 && arguments[1] == "--associate")
			{
				U.L(LogLevel.Information, "APP", "Associating file types and URI handles");
				SetAssociations(arguments[2].Split(',').ToList<string>());

				//U.L(LogLevel.Information, "APP", "Dying gracefully after file association");
				Application.Current.Shutdown();
				return;
			}

			// find out if Stoffi is already running
			Process ThisProcess = Process.GetCurrentProcess();
			Process[] SameProcesses = Process.GetProcessesByName(ThisProcess.ProcessName);
			U.L(LogLevel.Debug, "APP", "Checking for processes named: " + ThisProcess.ProcessName);
			if (SameProcesses.Length > 1) // Stoffi is already running!
			{
				U.L(LogLevel.Information, "APP", "Another instance is already running");

				// pass arguments to first instance
				try
				{
					using (NamedPipeClientStream client = new NamedPipeClientStream(identifier.ToString()))
					{
						using (StreamWriter writer = new StreamWriter(client))
						{
							U.L(LogLevel.Debug, "APP", "Sending arguments");
							client.Connect(200);

							foreach (String argument in arguments)
								writer.WriteLine(argument);

							if (arguments.Count() == 1)
							{
								U.L(LogLevel.Debug, "APP", "Trying to raise window");
								SetForegroundWindow(SameProcesses[0].MainWindowHandle);
								ShowWindow(SameProcesses[0].MainWindowHandle, 9);
							}
						}
					}

					// shut down
					U.L(LogLevel.Information, "APP", "Dying gracefully after sending data to running instance");
					Application.Current.Shutdown();
					return;
				}
				catch (TimeoutException exc)
				{
					U.L(LogLevel.Error, "APP", "Couldn't connect to server: " + exc.Message);
				}
				catch (IOException exc)
				{
					U.L(LogLevel.Error, "APP", "Pipe was broken: " + exc.Message);
				}
				catch (Exception exc)
				{
					U.L(LogLevel.Error, "APP", "Couldn't send arguments: " + exc.Message);
				}
				U.L(LogLevel.Warning, "APP", "Couldn't contact the other instance; I declare it dead and take its place");
			}
		}

		#endregion

		#region Methods

		#region Private

		/// <summary>
		/// Method which listen for arguments from other instances
		/// </summary>
		/// <param name="state">TODO</param>
		private void ListenForArguments(Object state)
		{
			U.L(LogLevel.Debug, "APP", "Listening for arguments");
			try
			{
				using (NamedPipeServerStream server = new NamedPipeServerStream(identifier))
				using (StreamReader reader = new StreamReader(server))
				{
					server.WaitForConnection();

					List<String> arguments = new List<String>();
					while (server.IsConnected)
						arguments.Add(reader.ReadLine());

					U.L(LogLevel.Debug, "APP", "Doing argument receivement");
					ArgumentsReceived((object)arguments.ToArray());
					//ThreadPool.QueueUserWorkItem(new WaitCallback(ArgumentsReceived), arguments.ToArray());
					U.L(LogLevel.Debug, "APP", "Continuing with listening");
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Error, "APP", "Couldn't listen for arguments: " + e.Message);
			}

			finally
			{
				ListenForArguments(null);
			}
		}

		/// <summary>
		/// Receives the arguments from ListenForArgument() and sends them to the MainWindow.
		/// </summary>
		/// <param name="state">The arguments converted from a String[] type</param>
		private void ArgumentsReceived(Object state)
		{
			U.L(LogLevel.Information, "APP", "Received arguments");
			String argument = "";
			try
			{
				String[] args = state as String[];
				for (int i = 1; i < args.Length; i++)
					argument += args[i].Trim() + " ";
				argument = argument.Trim();
			}
			catch (Exception e)
			{
				U.L(LogLevel.Debug, "APP", "Couldn't parse arguments: " + e.Message);
			}
			try
			{
				U.L(LogLevel.Debug, "APP", "Invoking CallFromSecondInstance with argument: " + argument);
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate() { ((StoffiWindow)MainWindow).CallFromSecondInstance(argument); }));
			}
			catch { }
		}

		/// <summary>
		/// Sets file associations in the registry.
		/// </summary>
		/// <param name="files">A list of all file types that should be associated with Stoffi.</param>
		private void SetAssociations(List<string> files)
		{
			string[] playlists = new string[] { "pls", "m3u", "m3u8" };
			string[] other = new string[] { "spp", "scx" };

			foreach (string item in files)
			{
				bool isPlaylist = playlists.Contains<string>(item);
				bool isOther = other.Contains<string>(item);

				string path = U.FullPath;
				string icon = Path.Combine(Path.GetDirectoryName(path), @"Icons\Win7\FileAudio.ico");
				string verb = U.T("FileAssociationOpen");

				string name;
				if (isPlaylist)
					name = String.Format(U.T("FileAssociationPlaylist"), item);

				else if (isOther)
				{
					name = U.T(String.Format("FileAssociation{0}", item.ToUpper()));
					icon = Path.Combine(Path.GetDirectoryName(path), String.Format(@"Icons\Win7\{0}.ico", item));
					if (item == "spp")
						verb = U.T("FileAssociationInstall");
					else if (item == "scx")
						verb = U.T("FileAssociationLoad");
				}
				else
					name = String.Format(U.T("FileAssociationSong"), item);


				// create pointer
				RegistryKey key = Registry.ClassesRoot.CreateSubKey("." + item);
				key.SetValue("", String.Format("Stoffi.{0}", item));
				key.Close();

				key = Registry.ClassesRoot.CreateSubKey(String.Format("Stoffi.{0}", item));

				// set icon
				RegistryKey iconKey = key.CreateSubKey("DefaultIcon");
				iconKey.SetValue("", icon);
				iconKey.Close();

				// create shell commands
				RegistryKey shellKey = key.CreateSubKey("shell");

				// open 
				RegistryKey openKey = shellKey.CreateSubKey("Open");
				RegistryKey cmdKey = openKey.CreateSubKey("command");
				cmdKey.SetValue("", path);
				cmdKey.Close();
				openKey.SetValue("", String.Format("&{0}", verb));
				openKey.Close();

				// play
				if (!isOther)
				{
					RegistryKey playKey = shellKey.CreateSubKey("PlayWithStoffi");
					cmdKey = playKey.CreateSubKey("command");
					cmdKey.SetValue("", String.Format("\"{0}\" \"%1\"", path));
					cmdKey.Close();
					playKey.SetValue("", U.T("FileAssociationPlay"));
					playKey.Close();
				}

				shellKey.SetValue("", "Open");
				shellKey.Close();

				key.SetValue("", name);
				key.Close();
			}

			// associate URL protocols
			foreach (string name in new string[] { "stoffi" })
			{
				string path = U.FullPath;
				string icon = Path.Combine(Path.GetDirectoryName(path), @"Icons\Win7\Stoffi.ico");

				RegistryKey key = Registry.ClassesRoot.CreateSubKey(name);
				key.SetValue("", String.Format("URL:{0} Protocol", U.Capitalize(name)));
				key.SetValue("URL Protocol", "");

				RegistryKey iconKey = key.CreateSubKey("DefaultIcon");
				iconKey.SetValue("", icon);
				iconKey.Close();

				// create shell commands
				RegistryKey shellKey = key.CreateSubKey("shell");
				RegistryKey openKey = shellKey.CreateSubKey("Open");
				RegistryKey cmdKey = openKey.CreateSubKey("command");
				cmdKey.SetValue("", String.Format("\"{0}\" \"%1\"", path));
				cmdKey.Close();
				openKey.Close();
				shellKey.Close();

				key.Close();
			}
		}

		#endregion

		#region Event handlers

		/// <summary>
		/// Called when the application is started
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Arguments of the event</param>
		private void App_Startup(object sender, StartupEventArgs e)
		{
			U.L(LogLevel.Information, "APP", "Starting Stoffi Music Player");
		}

		/// <summary>
		/// Called when the application is closed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void App_Exit(object sender, ExitEventArgs e)
		{
			U.L(LogLevel.Information, "APP", "Shutting down");
		}

		/// <summary>
		/// Event handler for when the application crashes due to an unhandled exception
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			U.L(LogLevel.Error, "APP", "Crashing due to unforseen problems: " + e.Exception.Message);
			U.L(LogLevel.Error, "APP", e.Exception.StackTrace);
			U.L(LogLevel.Error, "APP", e.Exception.Source);
			Stoffi.Core.SettingsManager.Save();
			if (Application.Current != null)
			{
				StoffiWindow stoffi = Application.Current.MainWindow as StoffiWindow;
				if (stoffi != null && stoffi.trayIcon != null)
					stoffi.trayIcon.Visibility = Visibility.Collapsed;
			}
		}

		#endregion

		#endregion

		#region Overrides

		/// <summary>
		/// Looks for another instance of Stoffi already running and sends arguments if such can
		/// be found.
		/// </summary>
		/// <param name="e">The startup arguments</param>
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			U.L(LogLevel.Information, "APP", "Started up");

			// create pipe server to listen for command line arguments
			U.L(LogLevel.Debug, "APP", "Starting to listen for arguments from second instances");
			ThreadPool.QueueUserWorkItem(new WaitCallback(ListenForArguments));
		}

		#endregion

		#region Imported

		[DllImport("user32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		#endregion
	}
}