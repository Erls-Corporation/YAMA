/**
 * ServiceManager.cs
 * 
 * Communicates with the Stoffi Services.
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Xml;
using System.Web;
using Newtonsoft.Json.Linq;

using Stoffi.Core;

namespace Stoffi
{
	/// <summary>
	/// Represents a manager that handles communication with
	/// the Stoffi services.
	/// </summary>
	public static class ServiceManager
	{
		#region Members

		private static OAuth.Manager oauth = new OAuth.Manager();
		private static string oauth_request_token = "";
		private static string domain = "http://beta.stoffiplayer.com";
		private static bool connected = false;
		private static int failedPings = 0;

		/// <summary>
		/// The name of the currently linked user.
		/// </summary>
		private static string userName = "";

		/// <summary>
		/// The name of this device.
		/// </summary>
		private static string deviceName = "";

		/// <summary>
		/// The timer which sends all sync operations contained in syncOutBuffer.
		/// </summary>
		private static Timer syncOutDelay = null;

		/// <summary>
		/// The ID of the currently linked user. -1 indicates no link active.
		/// </summary>
		private static uint userID = 0;

		/// <summary>
		/// The language tag for use with Stoffi website.
		/// </summary>
		private static string lang = CultureInfo.CurrentUICulture.IetfLanguageTag.Split('-').Last<string>().ToLower();

		/// <summary>
		/// A list of all buffered sync operations scheduled to be sent to the server.
		/// </summary>
		private static List<SyncOperation> syncOutBuffer = new List<SyncOperation>();
		//private static Dictionary<KeyValuePair<string, string>, string> albumArtCache = new Dictionary<KeyValuePair<string, string>, string>();
		private static string artFolder = "Art";
		private static string defaultArt = Path.Combine(artFolder, "Default.png");

		/// <summary>
		/// A timer used for delaying listen requests so that if the user changes tracks
		/// rapidely, only the last track will be submitted.
		/// </summary>
		private static Timer listenDelay = null;

		/// <summary>
		/// The track that is the current object being listened to.
		/// </summary>
		private static TrackData currentListen = null;

		/// <summary>
		/// A cached ID of the listen object for the current track at the server.
		/// </summary>
		private static uint idOfCurrentListen = 0;

		/// <summary>
		/// The time the current listen was started.
		/// </summary>
		private static DateTime? stampOfCurrentListen = null;

		/// <summary>
		/// A list of all listens that has been sent to server but now yet received reply.
		/// Key is the path of the track, value is timestamp of send and whether to issue delete upon reply.
		/// </summary>
		private static Dictionary<string, Tuple<bool, DateTime>> listenQueue = new Dictionary<string, Tuple<bool, DateTime>>();

		/// <summary>
		/// The minimum amount of seconds a song most play. If the playtime is below this
		/// then the listen will be deleted from the server.
		/// </summary>
		private static uint minimumListenTime = 15;

		/// <summary>
		/// A timer used for retransmission of listen submissions which have failed.
		/// </summary>
		private static Timer listenRetry = null;

		/// <summary>
		/// The seconds to wait until next retry of transmitting failed listen submissions.
		/// </summary>
		private static int[] listenRetrySteps = new int[] { 10, 30, 60, 180, 300 };

		/// <summary>
		/// The index of listenRetrySteps, indicating how many seconds to wait for next retransmission of failed listen submissions.
		/// </summary>
		/// <remarks>
		/// -1 indicates no retransmissions scheduled. If index is above upper bound then the last value in listenRetrySteps will be used.
		/// </remarks>
		private static int listenRetryStep = -1;

		#endregion

		#region Properties

		/// <summary>
		/// Gets whether or not the 
		/// </summary>
		public static bool Connected
		{
			get { return connected; }
			set
			{
				bool old = connected;
				connected = value;
				DispatchPropertyChanged("Connected", old, value);
			}
		}

		/// <summary>
		/// Gets whether the client is linked to a service account or not
		/// </summary>
		public static bool Linked
		{
			get
			{
				return SettingsManager.OAuthToken != null &&
					   SettingsManager.OAuthToken != "" &&
					   SettingsManager.OAuthSecret != null &&
					   SettingsManager.OAuthSecret != "";
			}
		}

		/// <summary>
		/// Gets the URL for authorizing the application with OAuth
		/// </summary>
		public static string RequestURL
		{
			get
			{
				return String.Format("{0}/{1}/oauth/authorize?oauth_token={2}",
					domain, 
					lang, 
					oauth_request_token);
			}
		}

		/// <summary>
		/// Gets the URL for logging out
		/// </summary>
		public static string LogoutURL { get { return String.Format("{0}/{1}/logout", domain, lang); } }

		/// <summary>
		/// Gets the Identity representing the currently linked user.
		/// </summary>
		public static CloudIdentity Identity
		{
			get
			{
				return SettingsManager.GetCloudIdentity(userID);
			}
		}

		/// <summary>
		/// Gets the callback URL for OAuth
		/// </summary>
		public static string CallbackURL
		{
			get { return String.Format("{0}/dashboard", domain); }
		}

		/// <summary>
		/// Gets the callback URL with parameters to authenticate 
		/// </summary>
		public static string CallbackURLWithAuthParams
		{
			get
			{
				return String.Format("{0}/{1}/dashboard?callback=stoffi&oauth_token={2}&oauth_secret_token={3}",
					domain,
					lang,
					SettingsManager.OAuthToken,
					SettingsManager.OAuthSecret);
			}
		}

		/// <summary>
		/// Gets the name of the currently linked user
		/// </summary>
		public static string UserName
		{
			get { return userName; }
			set
			{
				object old = userName;
				userName = value;
				DispatchPropertyChanged("UserName", old, value, true);
			}
		}

		/// <summary>
		/// Gets or sets the name of the device
		/// </summary>
		public static string DeviceName
		{
			get { return deviceName; }
			set
			{
				object old = deviceName;
				deviceName = value;
				DispatchPropertyChanged("DeviceName", old, value, true);

				if (old != value as object)
				{
					U.L(LogLevel.Debug, "SERVICE", "Changing device name");
					string url = String.Format("/devices/{0}.json", Identity.DeviceID);
					string query = String.Format("?device[name]={0}&device[version]={1}",
						OAuth.Manager.UrlEncode(value),
						OAuth.Manager.UrlEncode(SettingsManager.Release));
					var response = SendRequest(url, "PUT", query);
					if (response == null || response.StatusCode != HttpStatusCode.NoContent)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem changing device name");
						U.L(LogLevel.Error, "SERVICE", response);
					}
					else
					{
						U.L(LogLevel.Debug, "SERVICE", "Device name changed successfully");
					}
					if (response != null) response.Close();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether or not to synchronize with the cloud.
		/// </summary>
		public static bool Synchronize
		{
			get { return Identity != null && Identity.Synchronize; }
			set
			{
				bool old = Synchronize;
				if (Identity != null)
				{
					Identity.Synchronize = value;
					if (value && !old)
					{
						SyncConfig();
						SyncPlaylists();
					}
				}
				DispatchPropertyChanged("Synchronize", old, value);
			}
		}

		/// <summary>
		/// Gets or sets whether or not to synchronize configuration.
		/// </summary>
		public static bool SynchronizeConfiguration
		{
			get
			{
				return Identity != null && Identity.Synchronize && Identity.SynchronizeConfig;
			}
			set
			{
				if (value)
					Synchronize = true;

				if (Identity != null)
					Identity.SynchronizeConfig = value;
				SyncConfig();
			}
		}

		/// <summary>
		/// Gets or sets whether or not to synchronize playlists.
		/// </summary>
		public static bool SynchronizePlaylists
		{
			get
			{
				return Identity != null && Identity.Synchronize && Identity.SynchronizePlaylists;
			}
			set
			{
				if (value)
					Synchronize = true;

				if (Identity != null)
					Identity.SynchronizePlaylists = value;
				SyncPlaylists();
			}
		}

		/// <summary>
		/// Gets the domain name of the service.
		/// </summary>
		public static string Domain { get { return domain; } }

		#endregion

		#region Constructor

		/// <summary>
		/// Creates an instance of the ServiceManager class.
		/// </summary>
		static ServiceManager()
		{

		}

		#endregion

		#region Methods

		#region Public

		/// <summary>
		/// Initializes the manager.
		/// </summary>
		public static void Initialize()
		{
			ServicePointManager.DefaultConnectionLimit = 1000;

			ThreadStart initThread = delegate()
			{
				InitOAuth();
				SettingsManager.PropertyChanged += SettingsManager_PropertyChanged;
				MediaManager.Started += MediaManager_Started;
				PlaylistManager.PlaylistModified += PlaylistManager_PlaylistModified;
				PlaylistManager.PlaylistRenamed += PlaylistManager_PlaylistRenamed;
				DispatchInitialized();
			};
			Thread init_thread = new Thread(initThread);
			init_thread.Name = "Service Initializer thread";
			init_thread.Priority = ThreadPriority.Lowest;
			init_thread.Start();
		}

		/// <summary>
		/// Fetches the album art of a given track and applies to the track.
		/// </summary>
		/// <param name="track">The track to fetch art for</param>
		/// <param name="size">The prefered size to fetch</param>
		public static void SetArt(TrackData track, string size = "large")
		{
			switch (MediaManager.GetType(track))
			{
				case TrackType.YouTube:
					track.ArtURL = YouTubeManager.GetThumbnail(track);
					break;

				case TrackType.SoundCloud:
					break;

				default:
					track.ArtURL = Path.GetFullPath(GetArt(track, size));
					break;
			}
		}

		/// <summary>
		/// Fetches the album art of a given track.
		/// </summary>
		/// <param name="track">The track to fetch art for</param>
		/// <param name="size">The prefered size to fetch</param>
		/// <returns>The path to the best matching album art or the default album art</returns>
		public static string GetArt(TrackData track, string size = "large")
		{
			string path, ret = defaultArt;

			// check for album file
			if (!String.IsNullOrWhiteSpace(track.Album))
			{
				if (!String.IsNullOrWhiteSpace(track.Artist))
				{
					// check for artist's album
					path = Path.Combine(artFolder, U.C(track.Artist), U.C(track.Album));
					ret = FetchCachedImages(path, size);
					if (ret != defaultArt)
						return ret;

					// check for mix album
					path = Path.Combine(artFolder, "Mix", U.C(track.Album));
					ret = FetchCachedImages(path, size);
					if (ret != defaultArt)
						return ret;

				}
				else
				{
					// check all albums
					foreach (string folder in Directory.GetDirectories(artFolder))
					{
						path = Path.Combine(artFolder, folder, U.C(track.Album));
						ret = FetchCachedImages(path, size);
						if (ret != defaultArt)
							return ret;
					}
				}
			}

			// check for single file
			if (!String.IsNullOrWhiteSpace(track.Album) && !String.IsNullOrWhiteSpace(track.Artist))
			{
				path = Path.Combine(artFolder, U.C(track.Artist), "Singles", U.C(track.Title));
				ret = FetchCachedImages(path, size);
				if (ret != defaultArt)
					return ret;
			}

			// check for album URL on Last.fm
			if (!String.IsNullOrWhiteSpace(track.Album) && !String.IsNullOrWhiteSpace(track.Artist))
			{
				ret = FetchLastFMAlbumArt(track.Album, track.Artist, size);
				if (ret != defaultArt)
					return ret;
			}

			// check for single URL on Last.fm
			if (!String.IsNullOrWhiteSpace(track.Title) && !String.IsNullOrWhiteSpace(track.Artist))
			{
				ret = FetchLastFMTrackArt(track.Title, track.Artist, size);
				if (ret != defaultArt)
					return ret;
			}

			// check for artist URL on Last.fm
			if (!String.IsNullOrWhiteSpace(track.Artist))
			{
				ret = FetchLastFMArtistArt(track.Artist, size);
				if (ret != defaultArt)
					return ret;
			}

			return defaultArt;
		}

		/// <summary>
		/// Shares a track.
		/// </summary>
		/// <param name="track">The track to share</param>
		public static void ShareSong(TrackData track)
		{
			ThreadStart shareThread = delegate()
			{
				string path = track.Path;
				if (MediaManager.GetType(track) == TrackType.File)
					path = Path.GetFileName(track.Path);

				string query = String.Format("?{0}&object=song",
						String.Join("&", EncodeTrack(track, "track")));

				var response = SendRequest("/shares.json", "POST", query);

				if (response == null || response.StatusCode != HttpStatusCode.Created)
				{
					U.L(LogLevel.Error, "SERVICE" ,"There was a problem sharing song");
					U.L(LogLevel.Error, "SERVICE", response);
				}
				else
				{
					U.L(LogLevel.Information, "SERVICE", "Shared song successfully");
				}
				if (response != null) response.Close();
			};
			Thread sharethread = new Thread(shareThread);
			sharethread.Name = "Share thread";
			sharethread.Priority = ThreadPriority.Lowest;
			sharethread.Start();
		}

		/// <summary>
		/// Shares a playlist.
		/// </summary>
		/// <param name="playlist">The playlist to share</param>
		public static void SharePlaylist(PlaylistData playlist)
		{
			ThreadStart shareThread = delegate()
			{
				string query = String.Format("?playlist={0}&object=playlist",
						OAuth.Manager.UrlEncode(playlist.Name));
				var response = SendRequest("/shares.json", "POST", query);

				if (response == null || response.StatusCode != HttpStatusCode.Created)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem sharing playlist");
					U.L(LogLevel.Error, "SERVICE", response);
				}
				else
				{
					U.L(LogLevel.Information, "SERVICE", "Shared playlist successfully");
				}
				if (response != null) response.Close();
			};
			Thread sharethread = new Thread(shareThread);
			sharethread.Name = "Share thread";
			sharethread.Priority = ThreadPriority.Lowest;
			sharethread.Start();
		}

		/// <summary>
		/// Link Stoffi to an account
		/// </summary>
		/// <param name="url">The full callback URL (including parameters) where OAuth sent us after authorization</param>
		public static void Link(string url)
		{
			U.L(LogLevel.Information, "SERVICE", "Linking to service account");
			Dictionary<string,string> p = U.GetParams(U.GetQuery(url));
			string token = p["oauth_token"];
			string verifier = p["oauth_verifier"];
			var access_url = domain + "/oauth/access_token";
			try
			{
				OAuth.OAuthResponse accessToken = oauth.AcquireAccessToken(access_url, "POST", verifier);
				string[] tokens = accessToken.AllText.Split('&');
				SettingsManager.OAuthToken = tokens[0].Split('=')[1];
				SettingsManager.OAuthSecret = tokens[1].Split('=')[1];
				U.L(LogLevel.Information, "SERVICE", "Service account has been linked");
				RetrieveUserData();
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SERVICE", "Problem linking with account: " + e.Message);
				Connected = false;
			}
		}

		/// <summary>
		/// Remove the current link if there is one
		/// </summary>
		public static void Delink()
		{
			if (!Linked) return;
			SettingsManager.OAuthToken = null;
			SettingsManager.OAuthSecret = null;
			try
			{
				InitOAuth();
				DispatchPropertyChanged("Linked", true, false);
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SERVICE", "Problem delinking with account: " + e.Message);
				Connected = false;
			}
		}

		/// <summary>
		/// Update the values of an object.
		/// </summary>
		/// <remarks>
		/// Use this when an object is changed from outside the application (ie async call from server)
		/// in order to prevent the manager to send the update back to the server.
		/// </remarks>
		/// <param name="objectType">The name of the type of object</param>
		/// <param name="objectID">The object ID</param>
		/// <param name="updatedProperties">The updated properties of the object encoded in JSON</param>
		public static void UpdateObject(string objectType, uint objectID, string updatedProperties)
		{
			try
			{
				JObject o = JObject.Parse(updatedProperties);
				switch (objectType)
				{
					case "device":
						if (Identity == null) return;
						if (objectID == Identity.ConfigurationID)
						{
							if (o["name"] != null)
							{
								string name = U.UnescapeHTML((string)o["name"]);
								deviceName = name;
								DeviceName = name;
							}
						}
						break;

					case "configuration":
						if (Identity == null) return;
						if (objectID == Identity.ConfigurationID)
							SyncProfileUpdated(o);
						break;

					case "list_config":
						break;

					case "link":
						if (Identity == null) return;
						bool found = false;

						for (int i = 0; i < Identity.Links.Count; i++)
							if (Identity.Links[i].Connected && Identity.Links[i].ID == objectID)
							{
								UpdateLink(Identity.Links[i], true, o, false);
								found = true;
								break;
							}

						if (!found)
						{
							Link l = CreateLink(true, o);
							if (l != null)
								Identity.Links.Add(l);
						}

						DispatchPropertyChanged("Links", Identity.Links, Identity.Links, true);
						break;

					case "column":
						break;

					case "column_sort":
						break;

					case "equalizer_profile":
						break;

					case "keyboard_shortcut_profile":
						break;

					case "keyboard_shortcut":
						break;

					case "song":
						break;

					case "album":
						break;

					case "artist":
						break;

					case "user":
						break;

					case "playlist":
						if (SynchronizePlaylists)
						{
							string name = (string)o["name"];

							if (name != null)
							{
								PlaylistManager.PlaylistRenamed -= PlaylistManager_PlaylistRenamed;
								PlaylistManager.RenamePlaylist(objectID, (string)o["name"]);
								PlaylistManager.PlaylistRenamed += PlaylistManager_PlaylistRenamed;
							}

							JObject tracks = (JObject)o["songs"];
							PlaylistData playlist = PlaylistManager.FindPlaylist(objectID);

							if (tracks != null && playlist != null)
							{
								JArray added = (JArray)tracks["added"];
								JArray removed = (JArray)tracks["removed"];

								playlist.Tracks.CollectionChanged -= Playlist_CollectionChanged;

								List<TrackData> tracksToAdd = new List<TrackData>();
								List<TrackData> tracksToRemove = new List<TrackData>();
								foreach (JObject track in added)
								{
									TrackData t = JSONToTrack(track);
									if (t != null)
									{
										bool exists = false;
										foreach (TrackData tt in playlist.Tracks)
											if (tt.Path == t.Path)
											{
												exists = true;
												break;
											}
										if (!exists)
											tracksToAdd.Add(t);
									}
								}
								foreach (JObject track in removed)
								{
									TrackData t = JSONToTrack(track);
									if (t != null)
									{
										bool exists = false;
										foreach (TrackData tt in playlist.Tracks)
											if (tt.Path == t.Path)
											{
												exists = true;
												break;
											}
										if (exists)
											tracksToRemove.Add(t);
									}
								}
								DispatchModifyTracks(playlist.Tracks, new ModifiedEventArgs(ModifyType.Added, tracksToAdd));
								DispatchModifyTracks(playlist.Tracks, new ModifiedEventArgs(ModifyType.Removed, tracksToRemove));

								playlist.Tracks.CollectionChanged += Playlist_CollectionChanged;
							}
						}
						break;
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Error, "SERVICE", "Could not update object " + objectType + " with ID " + objectID.ToString() + ": " + e.Message);
			}
		}

		/// <summary>
		/// Creates a new object.
		/// </summary>
		/// <remarks>
		/// Use this when an object is changed from outside the application (ie async call from server)
		/// in order to prevent the manager to send the update back to the server.
		/// </remarks>
		/// <param name="objectType">The type of the object</param>
		/// <param name="objectJSON">The object encoded in JSON</param>
		public static void CreateObject(string objectType, string objectJSON)
		{
			try
			{
				JObject o = JObject.Parse(objectJSON);
				switch (objectType)
				{
					case "device":
						break;

					case "configuration":
						break;

					case "link":
						if (Identity == null) return;
						bool found = false;

						for (int i=0; i < Identity.Links.Count; i++)
							if (Identity.Links[i].Provider == (string)o["display"])
							{
								if (Identity.Links[i].Connected)
								{
									UpdateLink(Identity.Links[i], true, o);
									found = true;
									break;
								}
								else
								{
									Identity.Links.RemoveAt(i);
									break;
								}
							}

						if (!found)
						{
							Link l = CreateLink(true, o);
							if (l != null)
								Identity.Links.Add(l);
						}

						DispatchPropertyChanged("Links", Identity.Links, Identity.Links, true);
						break;

					case "list_config":
						break;

					case "column":
						break;

					case "column_sort":
						break;

					case "equalizer_profile":
						break;

					case "keyboard_shortcut_profile":
						break;

					case "keyboard_shortcut":
						break;

					case "song":
						break;

					case "album":
						break;

					case "artist":
						break;

					case "user":
						break;

					case "playlist":
						if (SynchronizePlaylists)
							MergePlaylistObject(o);
						break;
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Error, "SERVICE", "Could not create object " + objectType + ": " + e.Message);
			}
		}

		/// <summary>
		/// Deletes an object.
		/// </summary>
		/// <remarks>
		/// Use this when an object is changed from outside the application (ie async call from server)
		/// in order to prevent the manager to send the update back to the server.
		/// </remarks>
		/// <param name="objectType">The name of the type of object</param>
		/// <param name="objectID">The object ID</param>
		public static void DeleteObject(string objectType, uint objectID)
		{
			try
			{
				switch (objectType)
				{
					case "device":
						if (objectID == Identity.DeviceID)
						{
							Identity.DeviceID = 0;
							RegisterDevice();
						}
						break;

					case "configuration":
						if (objectID == Identity.ConfigurationID && Identity != null)
						{
							Identity.ConfigurationID = 0;
							Identity.SynchronizeConfig = false;
						}
						break;

					case "link":
						foreach (Link link in Identity.Links)
						{
							if (link.ID == objectID)
							{
								UpdateLink(link, false);
								break;
							}
						}
						DispatchPropertyChanged("Links", Identity.Links, Identity.Links, true);
						break;

					case "list_config":
						break;

					case "column":
						break;

					case "column_sort":
						break;

					case "equalizer_profile":
						break;

					case "keyboard_shortcut_profile":
						break;

					case "keyboard_shortcut":
						break;

					case "song":
						break;

					case "album":
						break;

					case "artist":
						break;

					case "user":
						break;

					case "playlist":
						if (SynchronizePlaylists)
						{
							PlaylistManager.PlaylistModified -= PlaylistManager_PlaylistModified;
							PlaylistManager.RemovePlaylist(objectID);
							PlaylistManager.PlaylistModified += PlaylistManager_PlaylistModified;
						}
						break;
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Error, "SERVICE", "Could not delete object " + objectType + " with ID " + objectID.ToString() + ": " + e.Message);
			}
		}

		/// <summary>
		/// Executes a command.
		/// </summary>
		/// <param name="command">The name of the command</param>
		/// <param name="configID">The ID of the config</param>
		public static void ExecuteCommand(string command, uint configID)
		{
			try
			{
				if (Identity.ConfigurationID != configID)
					return;

				switch (command.ToLower())
				{
					case "next":
						MediaManager.Next(true, true);
						break;

					case "prev":
					case "previous":
						MediaManager.Previous();
						break;

					case "play":
						MediaManager.Play();
						break;

					case "pause":
						MediaManager.Pause();
						break;

					case "play-pause":
						if (SettingsManager.MediaState == MediaState.Playing)
							MediaManager.Pause();
						else
							MediaManager.Play();
						break;
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Error, "SERVICE", "Could not execute command " + command + ": " + e.Message);
			}
		}

		/// <summary>
		/// Synchronize the application with a given cloud configuration.
		/// </summary>
		public static void SyncConfig()
		{
			if (!SynchronizeConfiguration)
				return;

			ThreadStart thread = delegate()
			{
				try
				{
					U.L(LogLevel.Debug, "SERVICE", "Retrieving cloud sync profile");
					string url = String.Format("/configurations.json");
					var response = SendRequest(url, "GET");
					if (response == null || response.StatusCode != HttpStatusCode.OK)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem retrieving sync profiles");
						U.L(LogLevel.Error, "SERVICE", response);
						SynchronizeConfiguration = false;
					}
					else
					{
						string query;
						JArray configs = ParseResponse(response) as JArray;
						if (configs.Count == 0)
						{
							U.L(LogLevel.Debug, "SERVICE", "Need to create configuration");
							url = String.Format("/configurations.json");
							query = String.Format("?configuration[name]=Default&device[version]={0}",
								OAuth.Manager.UrlEncode(SettingsManager.Release));
							response = SendRequest(url, "POST", query);
							if (response == null || response.StatusCode != HttpStatusCode.Created)
							{
								U.L(LogLevel.Error, "SERVICE", "There was a problem creating configuration");
								U.L(LogLevel.Error, "SERVICE", response);
								SynchronizeConfiguration = false;
							}
							else
							{
								Stream s = response.GetResponseStream();
								StreamReader sr = new StreamReader(s);
								string str = sr.ReadToEnd();
								JObject o = JObject.Parse(str);
								Identity.ConfigurationID = (uint)o["id"];
								U.L(LogLevel.Debug, "SERVICE", "Configuration was created successfully");
							}
							if (response != null) response.Close();
						}
						else
						{
							Identity.ConfigurationID = (uint)configs[0]["id"];
							SyncProfileUpdated(configs[0] as JObject);
						}

						U.L(LogLevel.Debug, "SERVICE", String.Format("Binding device to sync profile {0} in cloud", Identity.ConfigurationID));
						url = String.Format("/devices/{0}.json", Identity.DeviceID);
						query = String.Format("?device[configuration_id]={0}",
							OAuth.Manager.UrlEncode(Identity.ConfigurationID.ToString()));
						if (response != null) response.Close();

						response = SendRequest(url, "PUT", query);
						if (response == null || response.StatusCode != HttpStatusCode.NoContent)
						{
							U.L(LogLevel.Error, "SERVICE", "There was a problem updating device data");
							U.L(LogLevel.Error, "SERVICE", response);
						}
						else
							U.L(LogLevel.Debug, "SERVICE", "Successfully told server of our current sync profile.");

						if (response != null) response.Close();
					}
					if (response != null) response.Close();
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem setting up synchronization");
					U.L(LogLevel.Error, "SERVICE", e.Message);
					SynchronizeConfiguration = false;
				}
			};
			Thread th = new Thread(thread);
			th.Name = "Service thread";
			th.Priority = ThreadPriority.Lowest;
			th.Start();
		}

		/// <summary>
		/// Synchronize the playlists with the cloud.
		/// </summary>
		public static void SyncPlaylists()
		{
			if (!SynchronizePlaylists)
				return;

			ThreadStart thread = delegate()
			{
				try
				{
					U.L(LogLevel.Debug, "SERVICE", "Retrieving playlists");
					string url = String.Format("/me/playlists.json");
					var response = SendRequest(url, "GET");
					if (response == null || response.StatusCode != HttpStatusCode.OK)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem retrieving playlists");
						U.L(LogLevel.Error, "SERVICE", response);
					}
					else
					{
						JArray playlists = ParseResponse(response) as JArray;

						Dictionary<string, uint> cloudPlaylists = new Dictionary<string, uint>();

						foreach (JObject playlist in playlists)
						{
							MergePlaylistObject(playlist);
							cloudPlaylists.Add(playlist["name"].ToString(), Convert.ToUInt32(playlist["id"].ToString()));
						}

						foreach (PlaylistData playlist in SettingsManager.Playlists)
							if (playlist.ID <= 0 || (
									!cloudPlaylists.Values.Contains<uint>(playlist.ID) &&
									Identity != null &&
									playlist.Owner != Identity.UserID))
								UploadPlaylist(playlist);
					}
					if (response != null) response.Close();
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem synchronizing playlists");
					U.L(LogLevel.Error, "SERVICE", e.Message);
				}
			};
			Thread th = new Thread(thread);
			th.Name = "Playlist sync thread";
			th.Priority = ThreadPriority.Lowest;
			th.Start();
		}

		/// <summary>
		/// Initializes the oauth manager and acquires a request token if needed
		/// </summary>
		public static void InitOAuth()
		{
			oauth = new OAuth.Manager();
			oauth["consumer_key"] = U.StoffiKey;
			oauth["consumer_secret"] = U.StoffiSecret;
			if (Linked)
			{
				oauth["token"] = SettingsManager.OAuthToken;
				oauth["token_secret"] = SettingsManager.OAuthSecret;
				RetrieveUserData();
			}
			else
			{
				try
				{
					//oauth.AcquireRequestToken(domain + "/oauth/request_token", "POST");
					//oauth_request_token = oauth["token"];
				}
				catch (Exception e)
				{
					U.L(LogLevel.Warning, "SERVICE", "Problem linking with account: " + e.Message);
					Connected = false;
				}
			}
		}

		/// <summary>
		/// Checks if a given URL works.
		/// If not it will go to disconnected mode.
		/// </summary>
		/// <param name="url">The URL to check</param>
		public static void Ping(Uri url)
		{
			ThreadStart thread = delegate()
			{
				bool failed = false;
				try
				{
					U.L(LogLevel.Debug, "SERVICE", "Trying to ping: " + url.Host);
					Ping p = new Ping();
					PingReply r = p.Send(url.Host, 5000);
					if (r.Status != IPStatus.Success)
					{
						U.L(LogLevel.Error, "SERVICE", "Problem pinging " + url.Host + ": " + r.Status);
						failed = true;
					}
					else
					{
						U.L(LogLevel.Debug, "SERVICE", "Got ping reply from: " + url.Host);
						Connected = true;
					}
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "Error while pinging URL: " + e.Message);
					failed = true;
				}

				if (failed && failedPings < 3)
				{
					failedPings++;
					Ping(url);
				}
				else if (failed)
					Connected = false;
			};
			Thread th = new Thread(thread);
			th.Name = "Ping thread";
			th.Priority = ThreadPriority.Lowest;
			th.Start();
		}

		/// <summary>
		/// Retrieves a playlist object and either creates it or
		/// update an already existing playlist.
		/// </summary>
		/// <remarks>
		/// Called when a playlist is created in the cloud and need to be
		/// pushed to the local set of playlists.
		/// </remarks>
		/// <param name="playlist">The playlist object</param>
		public static PlaylistData MergePlaylistObject(JObject playlist)
		{
			string name = (string)playlist["name"];
			uint id = Convert.ToUInt32(playlist["id"].ToString());
			uint owner = Convert.ToUInt32(playlist["user_id"].ToString());

			PlaylistData p = PlaylistManager.FindPlaylist(id);

			if (p != null)
			{
				// found playlist with same ID
				p.Owner = owner;
				PlaylistManager.PlaylistRenamed -= PlaylistManager_PlaylistRenamed;
				PlaylistManager.RenamePlaylist(id, name);
				PlaylistManager.PlaylistRenamed += PlaylistManager_PlaylistRenamed;
			}
			else
			{
				p = PlaylistManager.FindPlaylist(name);
				if (p != null)
				{
					// found playlist with same name
					p.ID = id;
					p.Owner = owner;
				}
				else
				{
					// no playlist found
					PlaylistManager.PlaylistModified -= PlaylistManager_PlaylistModified;
					p = PlaylistManager.CreatePlaylist(name, id, owner);
					PlaylistManager.PlaylistModified += PlaylistManager_PlaylistModified;
				}
			}

			if (p != null)
			{
				// playlist created, merge tracks
				JArray tracks = (JArray)playlist["songs"];

				List<TrackData> tracksToAdd = new List<TrackData>();

				if (tracks != null)
				{
					// import tracks from cloud to local
					List<string> pathsInCloud = new List<string>();
					foreach (JObject track in tracks)
					{
						TrackData t = JSONToTrack(track);
						if (t != null)
						{
							pathsInCloud.Add(t.Path);
							bool exists = false;
							foreach (TrackData tt in p.Tracks)
								if (tt.Path == t.Path)
								{
									exists = true;
									break;
								}
							if (!exists)
								tracksToAdd.Add(t);
						}
					}

					// upload tracks from local to cloud
					if (Identity != null && p.Owner == Identity.UserID)
					{
						JArray tracksToCloud = new JArray();
						foreach (TrackData track in p.Tracks)
						{
							if (!pathsInCloud.Contains<string>(track.Path))
							{
								JObject json = TrackToJSON(track);
								if (json != null)
									tracksToCloud.Add(json);
							}
						}
						if (tracksToCloud.Count > 0)
						{
							JObject json = new JObject();
							json["songs"] = new JObject();
							json["songs"]["added"] = tracksToCloud;
							syncOutBuffer.Add(new SyncOperation("update", "playlists", p.ID, json));
							InitiateSyncTimer();
						}
					}
				}

				if (tracksToAdd.Count > 0)
				{
					DispatchModifyTracks(p.Tracks, new ModifiedEventArgs(ModifyType.Added, tracksToAdd));
				}

				p.Tracks.CollectionChanged += Playlist_CollectionChanged;
			}

			return p;
		}

		/// <summary>
		/// Deletes a given link to a third party service provider.
		/// </summary>
		/// <param name="provider">The name of the provider</param>
		public static void DeleteLink(string provider)
		{
			try
			{
				foreach (Link l in Identity.Links)
				{
					if (l.Connected && l.Provider == provider)
					{
						DeleteLink(l);
						return;
					}
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SERVICE", "Could not delete link: " + e.Message);
			}
		}

		/// <summary>
		/// Deletes a given link to a third party service provider.
		/// </summary>
		/// <param name="url">The URL to the link object</param>
		public static void DeleteLink(Uri url)
		{
			try
			{
				foreach (Link l in Identity.Links)
				{
					if (l.Connected && l.URL == url.AbsoluteUri)
					{
						DeleteLink(l);
						return;
					}
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SERVICE", "Could not delete link: " + e.Message);
			}
		}

		/// <summary>
		/// Deletes a given link to a third party service provider.
		/// </summary>
		/// <param name="link">The link to be deleted</param>
		public static void DeleteLink(Link link)
		{
			try
			{
				var response = SendRequest(link.URL + ".json", "DELETE");
				if (response == null || response.StatusCode != HttpStatusCode.NoContent)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem deleting link");
					U.L(LogLevel.Error, "SERVICE", response);
				}
				else
				{
					U.L(LogLevel.Information, "SERVICE", "Link deleted successfully");
				}
				if (response != null) response.Close();
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SERVICE", "Could not delete link: " + e.Message);
			}
		}

		/// <summary>
		/// Updates the properties of a link object at the server.
		/// </summary>
		/// <param name="link">The link to update</param>
		public static void UpdateLink(Link link)
		{
			try
			{
				string url = String.Format("{0}.json", link.URL);
				string query = "?";
				query += U.CreateParam("do_share", link.DoShare, "link") + "&";
				query += U.CreateParam("do_listen", link.DoListen, "link") + "&";
				query += U.CreateParam("do_donate", link.DoDonate, "link") + "&";
				query += U.CreateParam("do_create_playlist", link.DoCreatePlaylist, "link");
				var response = SendRequest(url, "PUT", query);

				if (response == null || response.StatusCode != HttpStatusCode.OK)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem updating link");
					U.L(LogLevel.Error, "SERVICE", response);
				}
				else
				{
					U.L(LogLevel.Debug, "SERVICE", "Updated link successfully");
				}
				if (response != null) response.Close();
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SERVICE", "Could not update link: " + e.Message);
			}
		}

		/// <summary>
		/// Retrieves a given link object.
		/// </summary>
		/// <param name="provider">The name of the provider of the link</param>
		/// <returns>The link object if it could be found, otherwise null</returns>
		public static Link GetLink(string provider)
		{
			try
			{
				foreach (Link l in Identity.Links)
					if (l.Provider == provider)
						return l;
			}
			catch { }
			return null;
		}

		/// <summary>
		/// Retrieves a given link object.
		/// </summary>
		/// <param name="id">The ID of the link</param>
		/// <returns>The link object if it could be found, otherwise null</returns>
		public static Link GetLink(int id)
		{
			try
			{
				foreach (Link l in Identity.Links)
					if (l.ID == id)
						return l;
			}
			catch { }
			return null;
		}
		
		#endregion

		#region Private

		/// <summary>
		/// Retrieves the data of the currently linked user.
		/// </summary>
		public static void RetrieveUserData()
		{
			if (!Linked) return;

			ThreadStart cloudThread = delegate()
			{
				U.L(LogLevel.Debug, "SERVICE", "Fetching user data");
				string url = String.Format("/me.json");
				var response = SendRequest(url, "GET", "");
				if (response == null || response.StatusCode != HttpStatusCode.OK)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem retrieving user data");
					U.L(LogLevel.Error, "SERVICE", response);
					Delink();
				}
				else
				{
					try
					{
						JObject o = ParseResponse(response) as JObject;
						userID = (uint)o["id"];
						UserName = (string)o["display"];
						if (!SettingsManager.HasCloudIdentity(userID))
							SettingsManager.CloudIdentities.Add(new CloudIdentity
							{
								UserID = userID,
								Links = new List<Link>(),
								SynchronizeConfig = true,
								SynchronizePlaylists = true,
								Synchronize = true
							});
						RegisterDevice();
					}
					catch (Exception e)
					{
						U.L(LogLevel.Error, "SERVICE", "Could not retrieve user data: " + e.Message);
						Delink();
					}
				}
				if (response != null) response.Close();
			};
			Thread cl_thread = new Thread(cloudThread);
			cl_thread.Name = "Cloud user thread";
			cl_thread.Priority = ThreadPriority.Lowest;
			cl_thread.Start();
		}

		/// <summary>
		/// Retrieves the links to third parties for the currently linked user.
		/// </summary>
		public static void RetrieveLinkData()
		{
			if (!Linked) return;

			ThreadStart cloudThread = delegate()
			{
				U.L(LogLevel.Debug, "SERVICE", "Fetching link data");
				string url = String.Format("/links.json");
				var response = SendRequest(url, "GET", "");
				if (response == null || response.StatusCode != HttpStatusCode.OK)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem retrieving link data");
					U.L(LogLevel.Error, "SERVICE", response);
					Delink();
				}
				else
				{
					try
					{
						JObject o = ParseResponse(response) as JObject;
						Identity.Links.Clear();

						JArray connected = (JArray)o["connected"];
						foreach (JObject link in connected)
							Identity.Links.Add(CreateLink(true, link));

						JArray notConnected = (JArray)o["not_connected"];
						foreach (JObject link in notConnected)
							Identity.Links.Add(CreateLink(false, link));
						DispatchPropertyChanged("Linked", false, true);
						SyncConfig();
						SyncPlaylists();
					}
					catch (Exception e)
					{
						U.L(LogLevel.Error, "SERVICE", "Could not retrieve link data for user: " + e.Message);
						Delink();
					}
				}
				if (response != null) response.Close();
			};
			Thread cl_thread = new Thread(cloudThread);
			cl_thread.Name = "Cloud link thread";
			cl_thread.Priority = ThreadPriority.Lowest;
			cl_thread.Start();
		}

		/// <summary>
		/// Registers the device with the server
		/// </summary>
		private static void RegisterDevice()
		{
			ThreadStart cloudThread = delegate()
			{
				try
				{
					if (Identity != null && Identity.DeviceID > 0)
					{
						U.L(LogLevel.Debug, "SERVICE", "Device already registered, verifying ID");
						string url = String.Format("/devices/{0}.json", Identity.DeviceID);
						var response = SendRequest(url, "GET");
						if (response == null || response.StatusCode != HttpStatusCode.OK)
						{
							Identity.DeviceID = 0;
							U.L(LogLevel.Debug, "SERVICE", "Previous registered ID was invalid");
							RegisterDevice();
						}
						else
						{
							Stream s = response.GetResponseStream();
							StreamReader sr = new StreamReader(s);
							string str = sr.ReadToEnd();
							JObject o = JObject.Parse(str);
							string name = (string)o["name"];
							U.L(LogLevel.Debug, "SERVICE", "ID verified, device name is " + name);
							deviceName = name;
							DeviceName = name;
							RetrieveLinkData();
						}
						if (response != null) response.Close();
					}
					else
					{
						string name = U.Capitalize(Environment.MachineName);
						if (SettingsManager.Channel != "Stable")
							name += " " + U.Capitalize(SettingsManager.Channel);
#if (DEBUG)
						name += " Dev";
#endif

						U.L(LogLevel.Debug, "SERVICE", "Need to register the device");
						string url = String.Format("/devices.json");
						string query = String.Format("?device[name]={0}&device[version]={1}",
							OAuth.Manager.UrlEncode(name),
							OAuth.Manager.UrlEncode(SettingsManager.Release));
						var response = SendRequest(url, "POST", query);
						if (response == null || response.StatusCode != HttpStatusCode.Created)
						{
							U.L(LogLevel.Error, "SERVICE", "There was a problem registering the device");
							U.L(LogLevel.Error, "SERVICE", response);
							Delink();
						}
						else
						{
							Stream s = response.GetResponseStream();
							StreamReader sr = new StreamReader(s);
							string str = sr.ReadToEnd();
							JObject o = JObject.Parse(str);
							Identity.DeviceID = (uint)o["id"];
							U.L(LogLevel.Debug, "SERVICE", "Device has been registered with ID " + Identity.DeviceID.ToString());
							deviceName = name;
							DeviceName = name;
							RetrieveLinkData();
						}
						if (response != null) response.Close();
					}
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "Could not register device: " + e.Message);
					Delink();
				}
			};
			Thread cl_thread = new Thread(cloudThread);
			cl_thread.Name = "Cloud register thread";
			cl_thread.Priority = ThreadPriority.Lowest;
			cl_thread.Start();
		}

		/// <summary>
		/// Uploads a playlist to the cloud.
		/// </summary>
		/// <param name="playlist">The playlist to upload</param>
		private static void UploadPlaylist(PlaylistData playlist)
		{
			if (playlist == null) return;

			JArray tracks = new JArray();
			foreach (TrackData track in playlist.Tracks)
			{
				JObject json = TrackToJSON(track);
				if (json != null)
					tracks.Add(json);
			}

			JObject para = new JObject();
			para["name"] = OAuth.Manager.UrlEncode(playlist.Name);
			para["is_public"] = "0";
			para["songs"] = tracks;

			syncOutBuffer.Add(new SyncOperation("create", "playlists", para));

			InitiateSyncTimer();
		}

		/// <summary>
		/// Will update Stoffi's current configuration according to the
		/// state of the synchronization profile.
		/// </summary>
		/// <param name="updatedParams">The updated parameters of the configuration.</param>
		private static void SyncProfileUpdated(JObject updatedParams)
		{
			SettingsManager.PropertyChanged -= SettingsManager_PropertyChanged;

			#region Media state
			//if (updatedParams["media_state"] != null)
			//{
			//    try
			//    {
			//        switch ((string)updatedParams["media_state"])
			//        {
			//            case "Playing":
			//                MediaManager.Play();
			//                break;

			//            case "Paused":
			//                MediaManager.Pause();
			//                break;

			//            case "Stopped":
			//                MediaManager.Stop();
			//                break;
			//        }
			//    }
			//    catch (Exception e)
			//    {
			//        U.L(LogLevel.Error, "SERVICE", "Problem synchronizing media state: " + e.Message);
			//    }
			//}
			#endregion

			#region Shuffle
			if (updatedParams["shuffle"] != null)
			{
				try
				{
					switch (((string)updatedParams["shuffle"]).ToLower())
					{
						case "noshuffle":
						case "off":
							SettingsManager.Shuffle = false;
							break;

						case "random":
						case "on":
							SettingsManager.Shuffle = true;
							break;
					}
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "Problem synchronizing shuffle mode: " + e.Message);
				}
			}
			#endregion

			#region Repeat
			if (updatedParams["repeat"] != null)
			{
				try
				{
					switch (((string)updatedParams["repeat"]).ToLower())
					{
						case "norepeat":
							SettingsManager.Repeat = RepeatState.NoRepeat;
							break;

						case "repeatall":
							SettingsManager.Repeat = RepeatState.RepeatAll;
							break;

						case "repeatone":
							SettingsManager.Repeat = RepeatState.RepeatOne;
							break;
					}
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "Problem synchronizing repeat mode: " + e.Message);
				}
			}
			#endregion

			#region Volume
			if (updatedParams["volume"] != null)
			{
				try
				{
					float vol = (float)updatedParams["volume"];
					SettingsManager.Volume = Convert.ToDouble(vol);
				}
				catch (Exception e)
				{
					try
					{
						string vol = (string)updatedParams["volume"];
						SettingsManager.Volume = Convert.ToDouble(vol);
					}
					catch (Exception ee)
					{
						U.L(LogLevel.Error, "SERVICE", "Problem synchronizing volume");
						U.L(LogLevel.Error, "SERVICE", e.Message);
						U.L(LogLevel.Error, "SERVICE", ee.Message);
					}
				}
			}
			#endregion

			#region Current track
			if (updatedParams["current_track"] != null)
			{
				try
				{
					string path = U.UnescapeHTML((string)updatedParams["current_track"]["path"]);
					if (path != null)
					{
						// TODO:
						// Create function SettingsManager.FindTrack()
						// It should then either find a local track fast
						// or convert the path into a youtube track.
						//if (YouTubeManager.IsYouTube(path))
						//{
						//    TrackData track = YouTubeManager.CreateTrack(path);
						//    if (track != null)
						//        SettingsManager.CurrentTrack = track;
						//}
						//else
						//{
						//    foreach (TrackData track in SettingsManager.FileTracks)
						//    {
						//        if (track.Path == path)
						//        {
						//            SettingsManager.CurrentTrack = track;
						//            break;
						//        }
						//    }
						//}
					}
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "Could not read current track from server");
					U.L(LogLevel.Error, "SERVICE", e.Message);
				}
			}
			#endregion

			SettingsManager.PropertyChanged += SettingsManager_PropertyChanged;
		}

		/// <summary>
		/// Sends a request to the server
		/// </summary>
		/// <param name="url">The base url</param>
		/// <param name="method">The HTTP method to use</param>
		/// <param name="query">The query string, encoded properly</param>
		/// <param name="body">The body of the request, encoded as JSON</param>
		/// <returns>The response from the server</returns>
		private static HttpWebResponse SendRequest(string url, string method, string query = "", string body = "")
		{
			if (SettingsManager.HasCloudIdentity(userID))
			{
				if (query == null || query == "") query = "?";
				else query += "&";
				query += "device_id=" + Identity.DeviceID.ToString();
			}

			string fullUrl = url + query;
			if (!fullUrl.StartsWith("http") && !fullUrl.StartsWith("/"))
				fullUrl = domain + "/" + fullUrl;
			else if (!fullUrl.StartsWith("http"))
				fullUrl = domain + fullUrl;
			//U.L(LogLevel.Debug, "SERVICE", "Sending " + method + " request to " + fullUrl);
			var authzHeader = oauth.GenerateAuthzHeader(fullUrl, method);
			var request = (HttpWebRequest)WebRequest.Create(fullUrl);
			request.Method = method;
			request.PreAuthenticate = true;
			request.AllowWriteStreamBuffering = true;
			request.Headers.Add("Authorization", authzHeader);
			request.Timeout = 30000;
			request.KeepAlive = false;

			if (body != null && body != "" && body != "[]" && body != "{}")
			{
				ASCIIEncoding encoding = new ASCIIEncoding();
				byte[] data = encoding.GetBytes(body);

				//request.ContentType = "application/json";
				request.ContentLength = data.Length;

				Stream bodyStream = request.GetRequestStream();
				bodyStream.Write(data, 0, data.Length);
				bodyStream.Close();
			}

			try
			{
				HttpWebResponse resp = (HttpWebResponse)request.GetResponse();
				return resp;
			}
			catch (WebException exc)
			{
				U.L(LogLevel.Error, "SERVICE", "Error while contacting cloud server: " + exc.Message);
				return null;
			}
		}

		/// <summary>
		/// Parses a web response encoded in JSON into an object.
		/// </summary>
		/// <param name="response">The JSON web response.</param>
		/// <returns>A JSON object.</returns>
		private static JToken ParseResponse(HttpWebResponse response)
		{
			try
			{
				Stream s = response.GetResponseStream();
				StreamReader sr = new StreamReader(s);
				string str = sr.ReadToEnd();
				sr.Close();
				return JToken.Parse(str);
			}
			catch (Exception e)
			{
				U.L(LogLevel.Error, "SERVICE", "Could not parse response: " + e.Message);
				return null;
			}
		}

		/// <summary>
		/// Encodes a track object into HTTP parameters.
		/// </summary>
		/// <param name="track">The track to encode</param>
		/// <param name="prefix">An optional prefix to put on each property</param>
		/// <returns>The track encoded as title=X, artist=Y, genre=Z...</returns>
		private static List<string> EncodeTrack(TrackData track, string prefix = "")
		{
			List<string> paras = new List<string>();
			if (track != null)
			{
				paras.Add(U.CreateParam("title", track.Title, prefix));
				paras.Add(U.CreateParam("artist", track.Artist, prefix));
				paras.Add(U.CreateParam("art_url", track.ArtURL, prefix));
				paras.Add(U.CreateParam("foreign_url", track.URL, prefix));
				paras.Add(U.CreateParam("genre", track.Genre, prefix));
				paras.Add(U.CreateParam("length", track.Length, prefix));

				switch (MediaManager.GetType(track))
				{
					case TrackType.File:
						paras.Add(U.CreateParam("path", Path.GetFileName(track.Path), prefix));
						break;

					default:
						paras.Add(U.CreateParam("path", track.Path, prefix));
						break;
				}
			}
			return paras;
		}

		/// <summary>
		/// Encodes a track to a JSON object.
		/// </summary>
		/// <param name="track">The track to encode</param>
		/// <returns>A JSON object</returns>
		private static JObject TrackToJSON(TrackData track)
		{
			JObject json = new JObject();
			if (track != null)
			{
				json.Add("title", U.EscapeJSON(track.Title));
				json.Add("artist", U.EscapeJSON(track.Artist));
				json.Add("art_url", U.EscapeJSON(track.ArtURL));
				json.Add("foreign_url", U.EscapeJSON(track.URL));
				json.Add("genre", U.EscapeJSON(track.Genre));
				json.Add("length", U.EscapeJSON(track.Length));

				switch (MediaManager.GetType(track))
				{
					case TrackType.File:
						json.Add("path", U.EscapeJSON(Path.GetFileName(track.Path)));
						break;

					default:
						json.Add("path", U.EscapeJSON(track.Path));
						break;
				}
			}
			return json;
		}

		/// <summary>
		/// Converts a JSON object to a track.
		/// </summary>
		/// <param name="track">The JSON object describing the track</param>
		/// <returns>A track if such can be created or found, otherwise null</returns>
		private static TrackData JSONToTrack(JObject track)
		{
			string path = (string)track["path"];
			double length = -1;
			try
			{
				length = Convert.ToDouble(track["length"].ToString());
			}
			catch { }
			
			TrackData t = null;
			if (path != null)
			{
				switch (MediaManager.GetType(path))
				{
					case TrackType.File:
						t = FilesystemManager.GetTrack(path, length);
						break;

					case TrackType.SoundCloud:
						t = SoundCloudManager.CreateTrack(path);
						break;

					case TrackType.YouTube:
						t = YouTubeManager.CreateTrack(path);
						break;

					case TrackType.WebRadio:
						t = MediaManager.ParseURL(path);
						break;
				}
			}
			return t;
		}

		/// <summary>
		/// Pushes a configuration change to the synchronization buffer.
		/// </summary>
		/// <param name="key">The key that was changed.</param>
		/// <param name="value">The new value of the key.</param>
		private static void PushConfigUpdate(string key, string value)
		{
			JToken token = (JToken)OAuth.Manager.UrlEncode(value);
			PushConfigUpdate(key, token);
		}

		/// <summary>
		/// Pushes a configuration change to the synchronization buffer.
		/// </summary>
		/// <param name="key">The key that was changed.</param>
		/// <param name="value">The new value of the key.</param>
		private static void PushConfigUpdate(string key, JToken value)
		{
			bool found = false;
			foreach (SyncOperation op in syncOutBuffer)
			{
				if (op.Command == "update" && op.ObjectType == "configurations" && op.ObjectID == Identity.ConfigurationID)
				{
					found = true;
					op.Params[key] = value;
				}
			}
			if (!found)
			{
				JObject para = new JObject();
				para[key] = value;
				syncOutBuffer.Add(new SyncOperation("update", "configurations", Identity.ConfigurationID, para));
			}

			InitiateSyncTimer();
		}

		/// <summary>
		/// Starts or resets the synchronization timer.
		/// </summary>
		private static void InitiateSyncTimer()
		{
			if (syncOutDelay != null)
				syncOutDelay.Dispose();
			syncOutDelay = new Timer(PerformSyncOut, null, 500, 3600000);
		}

		/// <summary>
		/// Looks for a cached album art image inside the path.
		/// </summary>
		/// <param name="path">The path in which to look for images</param>
		/// <param name="size">The prefered size of the image</param>
		/// <returns>The best matching image or the default album art</returns>
		private static string FetchCachedImages(string path, string size = "large")
		{
			if (Directory.Exists(path))
			{
				string ret = "";
				foreach (string file in Directory.GetFiles(path))
				{
					if (Path.GetFileNameWithoutExtension(file) == size)
						return file;
					ret = file;
				}
				if (ret != "")
					return ret;
			}
			return defaultArt;
		}

		/// <summary>
		/// Fetch the image of an artist on Last.fm.
		/// </summary>
		/// <param name="artist">The name of the artist</param>
		/// <param name="size">The prefered size</param>
		/// <returns>The best matching art path or the default album art</returns>
		private static string FetchLastFMArtistArt(string artist, string size = "large")
		{
			return FetchLastFMObjectArt(String.Format("http://ws.audioscrobbler.com/2.0?method=artist.info&format=json&api_key={0}&artist={1}",
				U.LastFMKey, OAuth.Manager.UrlEncode(artist)), size);
		}

		/// <summary>
		/// Fetch the art for an album on Last.fm.
		/// </summary>
		/// <param name="album">The name of the album</param>
		/// <param name="artist">The name of the artist</param>
		/// <param name="size">The prefered size</param>
		/// <returns>The best matching art path or the default album art</returns>
		private static string FetchLastFMAlbumArt(string album, string artist, string size = "large")
		{
			return FetchLastFMObjectArt(String.Format("http://ws.audioscrobbler.com/2.0?method=album.info&format=json&api_key={0}&album={1}&artist={2}",
				U.LastFMKey, OAuth.Manager.UrlEncode(album), OAuth.Manager.UrlEncode(artist)), size);
		}

		/// <summary>
		/// Fetch the art for a track on Last.fm.
		/// </summary>
		/// <param name="title">The title of the track</param>
		/// <param name="artist">The name of the artist</param>
		/// <param name="size">The prefered size</param>
		/// <returns>The best matching art path or the default album art</returns>
		private static string FetchLastFMTrackArt(string title, string artist, string size = "large")
		{
			return FetchLastFMObjectArt(String.Format("http://ws.audioscrobbler.com/2.0?method=track.info&format=json&api_key={0}&track={1}&artist={2}",
				U.LastFMKey, OAuth.Manager.UrlEncode(title), OAuth.Manager.UrlEncode(artist)), size);
		}

		/// <summary>
		/// Fetch the album art for an artist, album or track on Last.fm.
		/// </summary>
		/// <param name="url">The full URL to the object</param>
		/// <param name="size">The prefered size</param>
		/// <returns>The best matching album art path or the default album art</returns>
		private static string FetchLastFMObjectArt(string url, string size = "large")
		{
			try
			{
				var request = (HttpWebRequest)WebRequest.Create(url);
				using (var response = (HttpWebResponse)request.GetResponse())
				using (var reader = new StreamReader(response.GetResponseStream()))
				{
					string ret = FetchLastFMImages(JObject.Parse(reader.ReadToEnd()), size);
					if (ret != defaultArt)
						return ret;
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Information, "SERVICE", "Could not fetch album art from Last.fm: " + e.Message);
			}
			return defaultArt;
		}

		/// <summary>
		/// Fetches the images from a JSON object representing an album, artist or track.
		/// The images will be downloaded into the cache and the path to the image best
		/// matching the prefered size will be returned.
		/// </summary>
		/// <param name="json">The artist, album or track</param>
		/// <param name="size">The prefered size to return</param>
		/// <returns>The path to the image best matching the size</returns>
		private static string FetchLastFMImages(JObject json, string size = "large")
		{
			JArray images = null;
			string path = "";
			if (json["artist"] != null && json["artist"].Type == JTokenType.Object)
			{
				images = (JArray)json["artist"]["image"];
				path = Path.Combine(artFolder, U.C(json["artist"]["name"]));
			}
			else if (json["album"] != null && json["album"].Type == JTokenType.Object)
			{
				images = (JArray)json["album"]["image"];
				path = Path.Combine(artFolder, U.C(json["album"]["artist"]), U.C(json["album"]["name"]));
			}
			else if (json["track"] != null && json["track"].Type == JTokenType.Object)
			{
				if (json["track"]["album"] != null && json["track"]["album"].Type == JTokenType.Object)
				{
					images = (JArray)json["track"]["album"]["image"];
					path = Path.Combine(artFolder, U.C(json["track"]["album"]["artist"]), U.C(json["track"]["album"]["title"]));
				}
				else if (json["track"]["artist"] != null && json["track"]["artist"].Type == JTokenType.Object)
				{
					images = (JArray)json["track"]["artist"]["image"];
					path = Path.Combine(artFolder, U.C(json["track"]["artist"]["name"]), "Singles", U.C(json["track"]["name"]));
				}
			}

			if (images != null)
			{
				bool foundExact = false;
				WebClient client = new WebClient();
				string ret = "";
				foreach (JObject image in images)
				{
					try
					{
						string url = (string)image["#text"];
						if (url != "")
						{
							string ext = url.Substring(url.LastIndexOf('.'));
							Directory.CreateDirectory(path);
							string p = Path.Combine(path, (string)image["size"] + ext);
							if (!File.Exists(p))
								client.DownloadFile(url, p);
							if (!foundExact)
								ret = p;
							if ((string)image["size"] == size)
								foundExact = true;
						}
					}
					catch (Exception e)
					{
						U.L(LogLevel.Information, "SERVICE", "Could not download album art from Last.fm: " + e.Message);
					}
				}
				if (ret != "")
					return ret;
			}

			return defaultArt;
		}

		/// <summary>
		/// Starts or resets the listen delay timer.
		/// </summary>
		private static void InitiateListenTimer()
		{
			if (listenDelay != null)
				listenDelay.Dispose();
			listenDelay = new Timer(PerformStartListen, null, 2000, 3600000);
		}

		/// <summary>
		/// Queue a submission of the current track as being listened to.
		/// </summary>
		private static void StartListen()
		{
			if (!SettingsManager.SubmitSongs) return;
			InitiateListenTimer();
		}

		/// <summary>
		/// Update the expected end time for the current listen.
		/// </summary>
		/// <param name="endTime">The time that the listen is expected to end</param>
		private static void UpdateListen(DateTime endTime)
		{
			if (!SettingsManager.SubmitSongs || idOfCurrentListen <= 0) return;

			ThreadStart listenThread = delegate()
			{
				try
				{
					string url = String.Format("/listens/{0}.json", idOfCurrentListen);
					string query = String.Format("?listen[ended_at]={0}", endTime.ToUniversalTime().ToString("yyyyMMddHHmmss"));
					var response = SendRequest(url, "PUT", query);

					if (response == null || response.StatusCode != HttpStatusCode.NoContent)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem updating listen");
						if (response == null)
						{
							// overwrite previous PUTs but preserve previous DELETEs
							if (!SettingsManager.ListenBuffer.ContainsKey(url) || SettingsManager.ListenBuffer[url].Item1 != "DELETE")
							{
								SettingsManager.ListenBuffer[url] = new Tuple<string, string>("PUT", query);
								CheckListenRetry();
							}
						}
						else
							U.L(LogLevel.Error, "SERVICE", response);
					}
					else
					{
						U.L(LogLevel.Debug, "SERVICE", "Updated listen successfully");
					}
					if (response != null) response.Close();
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem updating listen");
					U.L(LogLevel.Error, "SERVICE", e.Message);
				}
			};
			Thread l_thread = new Thread(listenThread);
			l_thread.Name = "Listen thread";
			l_thread.Priority = ThreadPriority.Lowest;
			l_thread.Start();
		}

		/// <summary>
		/// End the listen with a given ID.
		/// </summary>
		/// <param name="id">The ID of the listen to end.</param>
		private static void EndListen(uint id)
		{
			if (!SettingsManager.SubmitSongs || id <= 0) return;

			ThreadStart listenThread = delegate()
			{
				try
				{
					string url = String.Format("/listens/{0}/end.json", id);
					var response = SendRequest(url, "POST");

					if (response == null || response.StatusCode != HttpStatusCode.Created)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem ending listen");
						if (response == null)
						{
							SettingsManager.ListenBuffer[url] = new Tuple<string, string>("POST", "");
							CheckListenRetry();
						}
						else
							U.L(LogLevel.Error, "SERVICE", response);
					}
					else
					{
						U.L(LogLevel.Debug, "SERVICE", "Ended listen successfully");
					}
					if (response != null) response.Close();
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem ending listen");
					U.L(LogLevel.Error, "SERVICE", e.Message);
				}
			};
			Thread l_thread = new Thread(listenThread);
			l_thread.Name = "Listen thread";
			l_thread.Priority = ThreadPriority.Lowest;
			l_thread.Start();
		}

		/// <summary>
		/// Delete the listen with a given ID.
		/// </summary>
		/// <param name="id">The ID of the listen to delete</param>
		private static void DeleteListen(uint id)
		{
			if (!SettingsManager.SubmitSongs || id <= 0) return;

			ThreadStart listenThread = delegate()
			{
				try
				{
					string url = String.Format("/listens/{0}.json", id);
					var response = SendRequest(url, "DELETE");

					if (response == null || response.StatusCode != HttpStatusCode.NoContent)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem deleting listen");
						if (response == null)
						{
							SettingsManager.ListenBuffer[url] = new Tuple<string, string>("DELETE", "");
							CheckListenRetry();
						}
						else
							U.L(LogLevel.Error, "SERVICE", response);
					}
					else
					{
						U.L(LogLevel.Debug, "SERVICE", "Deleted listen successfully");
					}
					if (response != null) response.Close();
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem deleting listen");
					U.L(LogLevel.Error, "SERVICE", e.Message);
				}
			};
			Thread l_thread = new Thread(listenThread);
			l_thread.Name = "Listen thread";
			l_thread.Priority = ThreadPriority.Lowest;
			l_thread.Start();
		}

		/// <summary>
		/// Starts the retransmission of failed listen submission if needed.
		/// </summary>
		private static void CheckListenRetry()
		{
			if (SettingsManager.ListenBuffer.Count == 0)
				listenRetryStep = -1;
			else if (listenRetryStep < 0)
				listenRetryStep = 0;

			if (listenRetryStep < 0)
			{
				if (listenRetry != null)
					listenRetry.Dispose();
				listenRetry = null;
			}
			else if (listenRetry == null)
			{
				listenRetry = new Timer(PerformRetryListen, null, listenRetrySteps[listenRetryStep] * 1000, Int32.MaxValue);
			}
		}

		/// <summary>
		/// Creates a link according to a JSON object.
		/// </summary>
		/// <param name="connected">Whether or not the link is connected or not</param>
		/// <param name="link">The JSON object describing the link</param>
		/// <returns>The Link object corresponding to the JSON object</returns>
		private static Link CreateLink(bool connected, JObject link)
		{
			//try
			//{
			if (connected)
			{
				Link l = new Link();
				UpdateLink(l, true, link);
				l.ConnectURL = String.Format("{0}?origin=%2Fdashboard", link["connectURL"]);
				return l;
			}
			else
			{
				return new Link()
				{
					Provider = (string)link["display"],
					URL = (string)link["url"],
					ConnectURL = (string)link["url"],
					Connected = false
				};
			}
			//}
			//catch (Exception e)
			//{
			//    U.L(LogLevel.Warning, "SERVICE", "Could not create link object: " + e.Message);
			//    return null;
			//}
		}

		/// <summary>
		/// Updates a link object.
		/// </summary>
		/// <param name="link">The link object to be updated</param>
		/// <param name="connected">Whether the link is connected or not</param>
		/// <param name="json">A JSON object describing the link (cannot be null if connected = true)</param>
		/// <param name="forceDefaults">Whether or not the boolean properties should be set to default values if they do not appear in the JSON object</param>
		private static void UpdateLink(Link link, bool connected, JObject json = null, bool forceDefaults = true)
		{
			if (connected)
			{
				string[] props = new string[] { "share", "listen", "donate", "create_playlist" };
				string[] pref = new string[] { "do", "can" };
				Dictionary<string, bool> settings = new Dictionary<string, bool>();
				foreach (string prop in props)
				{
					foreach (string prefix in pref)
					{
						string key = prefix + "_" + prop;
						try
						{
							settings[key] = json[key] != null && json[key].ToString().ToLower() == "true";
						}
						catch
						{
							settings[key] = false;
						}
					}
				}
				if (json["display"] != null)
					link.Provider = (string)json["display"];
				if (json["url"] != null)
					link.URL = (string)json["url"];

				try
				{
					if (json["id"] != null)
						link.ID = Convert.ToUInt32(String.Format("{0}", json["id"]));
				}
				catch { }

				if (json["can_share"] != null || forceDefaults)
					link.CanShare = settings["can_share"];
				if (json["do_share"] != null || forceDefaults)
					link.DoShare = settings["do_share"];
				if (json["can_listen"] != null || forceDefaults)
					link.CanListen = settings["can_listen"];
				if (json["do_listen"] != null || forceDefaults)
					link.DoListen = settings["do_listen"];
				if (json["can_donate"] != null || forceDefaults)
					link.CanDonate = settings["can_donate"];
				if (json["do_donate"] != null || forceDefaults)
					link.DoDonate = settings["do_donate"];
				if (json["can_create_playlist"] != null || forceDefaults)
					link.CanCreatePlaylist = settings["can_create_playlist"];
				if (json["do_create_playlist"] != null || forceDefaults)
					link.DoCreatePlaylist = settings["do_create_playlist"];
				link.Connected = true;
			}
			else
			{
				link.Connected = false;
			}
		}

		#endregion

		#region Event handlers

		/// <summary>
		/// Invoked when a property of the settings manager is changed
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private static void SettingsManager_PropertyChanged(object sender, PropertyChangedWithValuesEventArgs e)
		{
			if (Identity == null) return;

			// create query string
			switch (e.PropertyName)
			{
				case "Volume":
					if (SynchronizeConfiguration)
						PushConfigUpdate("volume", Convert.ToString(SettingsManager.Volume));
					break;

				case "Repeat":
					if (SynchronizeConfiguration)
						PushConfigUpdate("repeat", SettingsManager.Repeat.ToString());
					break;

				case "Shuffle":
					if (SynchronizeConfiguration)
						PushConfigUpdate("shuffle", SettingsManager.Shuffle ? "Random" : "Off");
					break;

				case "MediaState":
					if (SynchronizeConfiguration)
						PushConfigUpdate("media_state", SettingsManager.MediaState == MediaState.Playing ? "Playing" : "Paused");

					if (SettingsManager.MediaState == MediaState.Stopped)
						currentListen = null;

					if (SettingsManager.MediaState == MediaState.Playing && SettingsManager.CurrentTrack != null)
					{
						string path = SettingsManager.CurrentTrack.Path;
						if (listenQueue.ContainsKey(path))
						{
							// listen has been submitted but no reply yet
							listenQueue[path] = new Tuple<bool, DateTime>(false, listenQueue[path].Item2);
						}
						else
						{
							if (idOfCurrentListen <= 0)
							{
								// no listen submitted yet
								stampOfCurrentListen = DateTime.Now;
								StartListen();
							}
							else
							{
								// update listen to reflect new exepcted end time
								double pos = MediaManager.Position;
								double len = MediaManager.Length;
								DateTime expectedEnd = DateTime.UtcNow.AddSeconds(len - pos);
								UpdateListen(expectedEnd);
							}
						}
					}
					else if (SettingsManager.CurrentTrack != null)
					{
						string path = SettingsManager.CurrentTrack.Path;
						if (listenQueue.ContainsKey(path))
						{
							// listen has been submitted but no reply yet
							DateTime stamp = listenQueue[path].Item2;
							TimeSpan diff = DateTime.Now - stamp;
							if (diff.TotalSeconds < minimumListenTime)
								listenQueue[path] = new Tuple<bool, DateTime>(true, stamp);
						}
						else
						{
							if (idOfCurrentListen <= 0) return;

							// update listen to reflect exepcted end time
							UpdateListen(DateTime.UtcNow);
						}
					}

					break;

				case "CurrentTrack":
					// TODO: we need to be able to properly serialize nested JSON stuff in PerformSyncOut()
					//if (SynchronizeConfiguration)
					//    PushConfigUpdate("current_track", TrackToJSON(SettingsManager.CurrentTrack));
					break;

				case "SyncPlaylists":
					SyncPlaylists();
					break;

				case "SyncConfig":
					SyncConfig();
					break;

				default:
					return;
			}
		}

		/// <summary>
		/// Invoked when the media manager starts playing a track.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private static void MediaManager_Started(object sender, EventArgs e)
		{
			if (SettingsManager.MediaState == MediaState.Playing &&
				SettingsManager.CurrentTrack != null &&
				(currentListen == null || SettingsManager.CurrentTrack.Path != currentListen.Path))
			{
				uint oldListen = idOfCurrentListen;
				idOfCurrentListen = 0;

				if (oldListen > 0 && stampOfCurrentListen != null)
				{
					DateTime stamp = stampOfCurrentListen.Value;
					TimeSpan diff = DateTime.Now - stamp;
					if (diff.TotalSeconds >= minimumListenTime)
						EndListen(oldListen);
					else
						DeleteListen(oldListen);
				}

				stampOfCurrentListen = DateTime.Now;
				currentListen = SettingsManager.CurrentTrack;
				StartListen();
			}
		}

		/// <summary>
		/// Invoked when a playlist has been modified.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private static void PlaylistManager_PlaylistModified(object sender, ModifiedEventArgs e)
		{
			if (!SynchronizePlaylists) return;

			PlaylistData playlist = sender as PlaylistData;
			if (Identity == null || playlist.Owner != Identity.UserID)
				return;

			Thread thread = new Thread(delegate()
			{
				switch (e.Type)
				{
					case ModifyType.Created:
						UploadPlaylist(playlist);
						break;

					case ModifyType.Removed:
						syncOutBuffer.Add(new SyncOperation("delete", "playlists", playlist.ID));
						InitiateSyncTimer();
						break;
				}
			});
			thread.Name = "Playlist sync thread";
			thread.Priority = ThreadPriority.Lowest;
			thread.Start();
		}

		/// <summary>
		/// Invoked when a playlist has been modified.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private static void PlaylistManager_PlaylistRenamed(object sender, RenamedEventArgs e)
		{
			if (!SynchronizePlaylists) return;

			PlaylistData playlist = sender as PlaylistData;

			JObject para = new JObject();
			para["name"] = OAuth.Manager.UrlEncode(e.Name);

			syncOutBuffer.Add(new SyncOperation("update", "playlists", playlist.ID, para));
			InitiateSyncTimer();
		}

		/// <summary>
		/// Invoked when songs are added or removed from a playlist.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private static void Playlist_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			// find the playlist whose collection was modified
			ObservableCollection<TrackData> t = sender as ObservableCollection<TrackData>;
			if (t == null) return;
			PlaylistData playlist = null;
			foreach (PlaylistData p in SettingsManager.Playlists)
				if (p.Tracks == t)
				{
					playlist = p;
					break;
				}
			if (playlist == null) return;

			// not synced or our playlist?
			if (Identity == null || playlist.ID <= 0 || playlist.Owner != Identity.UserID)
				return;

			SyncOperation op = null;
			foreach (SyncOperation o in syncOutBuffer)
			{
				if (o.Command == "update" && o.ObjectType == "playlists" && o.ObjectID == playlist.ID)
				{
					op = o;
					break;
				}
			}

			if (op == null)
			{
				JObject para = new JObject();
				para["songs"] = new JObject();
				para["songs"]["added"] = new JArray();
				para["songs"]["removed"] = new JArray();
				op = new SyncOperation("update", "playlists", playlist.ID, para);
				syncOutBuffer.Add(op);
			}

			JObject tracks = new JObject();
			if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems.Count > 0)
			{
				foreach (TrackData track in e.NewItems)
				{
					JObject json = TrackToJSON(track);
					if (json != null)
						((JArray)op.Params["songs"]["added"]).Add(json);
				}
			}
			if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems.Count > 0)
			{
				foreach (TrackData track in e.OldItems)
				{
					JObject json = TrackToJSON(track);
					if (json != null)
						((JArray)op.Params["songs"]["removed"]).Add(json);
				}
			}

			InitiateSyncTimer();
		}

		/// <summary>
		/// Sends requests to the server updating the properties stored in
		/// syncOutBuffer
		/// </summary>
		/// <param name="state">The state (not used)</param>
		private static void PerformSyncOut(object state)
		{
			if (syncOutDelay != null)
				syncOutDelay.Dispose();
			syncOutDelay = null;

			if (Identity == null || syncOutBuffer.Count == 0)
				return;

			ThreadStart thread = delegate()
			{
				U.L(LogLevel.Debug, "SERVICE", "Synchronizing with cloud");
				List<SyncOperation> tmpBuffer = new List<SyncOperation>();
				foreach (SyncOperation op in syncOutBuffer)
					tmpBuffer.Add(op);
				syncOutBuffer.Clear();
				foreach (SyncOperation op in tmpBuffer)
				{
					HttpStatusCode expectedCode = HttpStatusCode.NoContent;
					string method = null;
					string url = null;
					string query = null;
					string body = null;
					
					string o = op.ObjectType.Substring(0, op.ObjectType.Length-1);

					switch (op.Command)
					{
						case "create":
							method = "POST";
							url = String.Format("/{0}.json", op.ObjectType);

							if (op.ObjectType == "playlists")
							{
								// remove "songs" and put in body
								if (op.Params["songs"] != null)
								{
									body = op.Params["songs"].ToString();
									op.Params.Remove("songs");
								}
							}

							foreach (string key in op.Params.Properties().Select(p => p.Name).ToList())
								query += String.Format("{0}[{1}]={2}&", o, key, OAuth.Manager.UrlEncode(op.Params[key].ToString()));

							expectedCode = HttpStatusCode.Created;

							break;

						case "update":
							method = "PUT";
							url = String.Format("/{0}/{1}.json", op.ObjectType, op.ObjectID);

							if (op.ObjectType == "playlists")
							{
								// remove "songs" and put in body
								if (op.Params["songs"] != null)
								{
									body = op.Params["songs"].ToString();
									op.Params.Remove("songs");
								}
							}
							
							foreach (string key in op.Params.Properties().Select(p => p.Name).ToList())
								query += String.Format("{0}[{1}]={2}&", o, key, OAuth.Manager.UrlEncode(op.Params[key].ToString()));
							break;

						case "delete":
							method = "DELETE";
							url = String.Format("/{0}/{1}.json", op.ObjectType, op.ObjectID);
							break;
					}

					if (query != null)
						query = "?" + query.Substring(0, query.Length - 1);

					// send request to server
					var response = SendRequest(url, method, query, body);
					if (response == null || response.StatusCode != expectedCode)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem synchronizing with cloud");
						U.L(LogLevel.Error, "SERVICE", response);
					}
					else
					{
						if (op.ObjectType == "playlists" && op.Command == "create")
						{
							Stream s = response.GetResponseStream();
							StreamReader sr = new StreamReader(s);
							string str = sr.ReadToEnd();
							JObject jo = JObject.Parse(str);
							PlaylistData playlist = PlaylistManager.FindPlaylist((string)jo["name"]);
							if (playlist != null)
							{
								playlist.ID = (uint)jo["id"];
								playlist.Owner = (uint)jo["user_id"];
								playlist.Tracks.CollectionChanged += Playlist_CollectionChanged;
								U.L(LogLevel.Debug, "SERVICE", "Playlist was uploaded successfully with cloud ID " + playlist.ID);
							}
						}
					}
					if (response != null) response.Close();
				}
				syncOutBuffer.Clear();
			};
			Thread th = new Thread(thread);
			th.Name = "Sync thread";
			th.Priority = ThreadPriority.Lowest;
			th.Start();
		}

		/// <summary>
		/// Sends request to the server submitting the CurrentTrack as a new Listen object.
		/// </summary>
		/// <param name="state">The state (not used)</param>
		private static void PerformStartListen(object state)
		{
			if (listenDelay != null)
				listenDelay.Dispose();
			listenDelay = null;

			if (SettingsManager.CurrentTrack == null)
				return;

			ThreadStart thread = delegate()
			{
				try
				{
					TrackData track = SettingsManager.CurrentTrack;

					PlaylistData playlist = null;
					if (SettingsManager.CurrentActiveNavigation.StartsWith("playlist:"))
						playlist = PlaylistManager.FindPlaylist(SettingsManager.CurrentActiveNavigation.Split(new char[] { ':' }, 2)[1]);

					string path = track.Path;
					if (MediaManager.GetType(track) == TrackType.File)
						path = Path.GetFileName(track.Path);

					string query = String.Format("?{0}", String.Join("&", EncodeTrack(track, "track")));
					if (playlist != null)
						query = String.Format("{0}&playlist={1}", query, playlist.ID > 0 ? playlist.ID.ToString() : playlist.Name);

					listenQueue[path] = new Tuple<bool, DateTime>(false, DateTime.Now);

					var response = SendRequest("/listens.json", "POST", query);

					if (response == null || response.StatusCode != HttpStatusCode.Created)
					{
						U.L(LogLevel.Error, "SERVICE", "There was a problem submitting song");
						if (response == null)
						{
							SettingsManager.ListenBuffer["/listens.json" + query] = new Tuple<string, string>("POST", path);
							CheckListenRetry();
						}
						else
							U.L(LogLevel.Error, "SERVICE", response);
					}
					else
					{
						U.L(LogLevel.Debug, "SERVICE", "Submitted listen of song");
						JObject listen = ParseResponse(response) as JObject;
						uint id = (uint)listen["id"];
						bool b = listenQueue[path].Item1;
						listenQueue.Remove(path);
						if (b)
							DeleteListen(id);
						else
							idOfCurrentListen = id;
					}
					if (response != null) response.Close();
				}
				catch (Exception e)
				{
					U.L(LogLevel.Error, "SERVICE", "There was a problem starting listen");
					U.L(LogLevel.Error, "SERVICE", e.Message);
					//Delink();
				}
			};
			Thread th = new Thread(thread);
			th.Name = "Listen submission thread";
			th.Priority = ThreadPriority.Lowest;
			th.Start();
		}

		/// <summary>
		/// Sends request to the server submitting the listens that has failed.
		/// </summary>
		/// <param name="state">The state (not used)</param>
		private static void PerformRetryListen(object state)
		{
			if (listenRetry != null)
				listenRetry.Dispose();

			if (SettingsManager.ListenBuffer.Count == 0)
			{
				listenRetryStep = -1;
				return;
			}

			listenRetryStep++;

			try
			{
				List<Tuple<string,string,string>> buffer = new List<Tuple<string,string,string>>();
				for (int i = 0; i < SettingsManager.ListenBuffer.Count; i++)
				{
					KeyValuePair<string,Tuple<string,string>> kv = SettingsManager.ListenBuffer.ElementAt(i);
					buffer.Add(new Tuple<string,string,string>(kv.Key, kv.Value.Item1, kv.Value.Item2));
				}
				SettingsManager.ListenBuffer.Clear();

				foreach (Tuple<string,string,string> tup in buffer)
				{
					string path = tup.Item1;
					string method = tup.Item2;
					string query = tup.Item3;
					ThreadStart thread = delegate()
					{
						try
						{
							string trackPath = "";

							// if POST then URL will contain the query and the track's path will be
							// the second item in the tuple.
							if (method == "POST")
							{
								string[] k = path.Split(new char[] { '?' }, 2);
								path = k[0];
								query = "?" + k[1];
								trackPath = tup.Item3;
								// if the listen is scheduled to be removed after creation (due to not
								// being played long enough) then we just skip creating it.
								if (listenQueue.ContainsKey(trackPath) && listenQueue[trackPath].Item1)
								{
									listenQueue.Remove(trackPath);
									return;
								}
							}
							var response = SendRequest(path, method, query);

							if (response == null || (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.NoContent))
							{
								U.L(LogLevel.Error, "SERVICE", "There was a problem when retrying listen transmission");
								if (response == null)
									SettingsManager.ListenBuffer[tup.Item1] = new Tuple<string, string>(tup.Item2, tup.Item3);
								else
									U.L(LogLevel.Error, "SERVICE", response);
							}
							else
							{
								U.L(LogLevel.Debug, "SERVICE", "Successfully retransmitted listen submission");
								if (response.StatusCode == HttpStatusCode.Created)
								{
									JObject listen = ParseResponse(response) as JObject;
									uint id = (uint)listen["id"];
									if (listenQueue.ContainsKey(trackPath))
									{
										bool b = listenQueue[trackPath].Item1;
										listenQueue.Remove(trackPath);
										if (b)
											DeleteListen(id);
										else
											idOfCurrentListen = id;
									}
									else
										idOfCurrentListen = id;
								}
							}
							if (response != null) response.Close();
						}
						catch (Exception e)
						{
							U.L(LogLevel.Error, "SERVICE", "There was a problem starting listen");
							U.L(LogLevel.Error, "SERVICE", e.Message);
						}
					};
					Thread th = new Thread(thread);
					th.Name = "Listen submission retry thread";
					th.Priority = ThreadPriority.Lowest;
					th.Start();
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Error, "SERVICE", "There was a problem starting listen");
				U.L(LogLevel.Error, "SERVICE", e.Message);
			}

			int nextStep = Math.Min(listenRetryStep, listenRetrySteps.Count() - 1);
			listenRetry = new Timer(PerformRetryListen, null, listenRetrySteps[nextStep] * 1000, Int32.MaxValue);
		}

		#endregion

		#region Dispatchers

		/// <summary>
		/// Trigger the PropertyChanged event
		/// </summary>
		/// <param name="name">The name of the property that was changed</param>
		/// <param name="oldValue">The value of the property before the change</param>
		/// <param name="newValue">The value of the property after the change</param>
		/// <param name="force">Dispatch even when no change occured between values</param>
		public static void DispatchPropertyChanged(string name, object oldValue, object newValue, bool force = false)
		{
			if (PropertyChanged != null && (oldValue != newValue || force))
				PropertyChanged(null, new PropertyChangedWithValuesEventArgs(name, oldValue, newValue));
		}

		/// <summary>
		/// Trigger the Initialized event.
		/// </summary>
		public static void DispatchInitialized()
		{
			if (Initialized != null)
				Initialized(null, new EventArgs());
		}

		/// <summary>
		/// Trigger the ModifyTracks event.
		/// </summary>
		/// <param name="collection">The collection that need to be modified</param>
		/// <param name="arguments">The arguments describing the modifications</param>
		public static void DispatchModifyTracks(ObservableCollection<TrackData> collection, ModifiedEventArgs arguments)
		{
			if (ModifyTracks != null)
				ModifyTracks(collection, arguments);
		}

		#endregion

		#endregion
		
		#region Events

		/// <summary>
		/// Occurs when a property has been changed
		/// </summary>
		public static event PropertyChangedWithValuesEventHandler PropertyChanged;

		/// <summary>
		/// Occurs when the service manager has been fully initialized.
		/// </summary>
		public static event EventHandler Initialized;

		/// <summary>
		/// Occurs when the main window need to modify a collection in the UI thread.
		/// </summary>
		public static event EventHandler<ModifiedEventArgs> ModifyTracks;

		#endregion
	}

	/// <summary>
	/// Describes the interface that the async javascript will call to update attributes
	/// </summary>
	[ComVisibleAttribute(true)]
	public class CloudSyncInterface
	{
		#region Members

		/// <summary>
		/// Values correpsond to arguments to <seealso cref="Buffer"/>.
		/// Key is in format: command, objectID, objectType
		/// Value is data
		/// </summary>
		private static Dictionary<Tuple<SyncCommand,uint,string>, string> syncInBuffer =
			new Dictionary<Tuple<SyncCommand, uint, string>, string>();
		private static Timer syncInDelay = null;
		#endregion

		#region Methods

		#region Private

		/// <summary>
		/// Places a sync command onto the buffer
		/// and resets the buffer timer.
		/// </summary>
		/// <param name="syncCommand">The command to be pushed onto the buffer</param>
		/// <param name="objectID">The ID of the object which to manipulate (not used for Create command)</param>
		/// <param name="objectType">The type of the object to act on (not used for Execute command).</param>
		/// <param name="data">
		/// Object structure if Update or Create, command if Execute, otherwise empty.
		/// </param>
		private void Buffer(SyncCommand syncCommand, uint objectID, string objectType, string data)
		{
			// create query string
			switch (syncCommand)
			{
				case SyncCommand.Execute:
					syncInBuffer[Tuple.Create<SyncCommand, uint, string>(syncCommand, objectID, "")] = data;
					break;

				case SyncCommand.Create:
					syncInBuffer[Tuple.Create<SyncCommand, uint, string>(syncCommand, 0, objectType)] = data;
					break;

				case SyncCommand.Delete:
					var updateTuple = Tuple.Create<SyncCommand, uint, string>(syncCommand, objectID, objectType);

					// remove any updates
					if (syncInBuffer.ContainsKey(updateTuple))
						syncInBuffer.Remove(updateTuple);

					syncInBuffer[Tuple.Create<SyncCommand, uint, string>(syncCommand, objectID, objectType)] = "";
					break;

				case SyncCommand.Update:
					syncInBuffer[Tuple.Create<SyncCommand, uint, string>(syncCommand, objectID, objectType)] = data;
					break;

				default:
					return;
			}

			if (syncInDelay != null)
				syncInDelay.Dispose();
			syncInDelay = new Timer(PerformSyncIn, null, 500, 3600000);
		}

		#endregion

		#region Event handlers

		/// <summary>
		/// Invoked when an object changes.
		/// </summary>
		/// <param name="objectType">The name of the type of object</param>
		/// <param name="objectID">The object ID</param>
		/// <param name="properties">The updated properties encoded in JSON</param>
		public void UpdateObject(string objectType, uint objectID, string properties)
		{
			Buffer(SyncCommand.Update, objectID, objectType, properties);
		}

		/// <summary>
		/// Invoked when an object is created.
		/// </summary>
		/// <param name="objectType">The name of the type of object</param>
		/// <param name="objectJSON">The object encoded as JSON</param>
		public void CreateObject(string objectType, string objectJSON)
		{
			Buffer(SyncCommand.Create, 0, objectType, objectJSON);
		}

		/// <summary>
		/// Invoked when an object is deleted.
		/// </summary>
		/// <param name="objectType">The name of the type of object</param>
		/// <param name="objectID">The object id</param>
		public void DeleteObject(string objectType, uint objectID)
		{
			Buffer(SyncCommand.Delete, objectID, objectType, "");
		}

		/// <summary>
		/// Invoked when a command is to be executed.
		/// </summary>
		/// <param name="command">The name of the command</param>
		/// <param name="configID">The ID of the synchronization configuration object</param>
		public void Execute(string command, uint configID)
		{
			Buffer(SyncCommand.Execute, configID, "", command);
		}

		/// <summary>
		/// Performs actions according to the commands stored in
		/// syncInBuffer.
		/// </summary>
		/// <param name="state">The state (not used)</param>
		private static void PerformSyncIn(object state)
		{
			if (syncInDelay != null)
				syncInDelay.Dispose();
			syncInDelay = null;

			ThreadStart thread = delegate()
			{
				Dictionary<Tuple<SyncCommand, uint, string>, string> buf = new Dictionary<Tuple<SyncCommand, uint, string>, string>();
				for (int i = 0; i < syncInBuffer.Count; i++)
					buf.Add(syncInBuffer.Keys.ElementAt(i), syncInBuffer.Values.ElementAt(i));
				syncInBuffer.Clear();
				foreach (KeyValuePair<Tuple<SyncCommand, uint, string>, string> p in buf)
				{
					switch (p.Key.Item1)
					{
						case SyncCommand.Execute:
							ServiceManager.ExecuteCommand(p.Value, p.Key.Item2);
							break;

						case SyncCommand.Create:
							ServiceManager.CreateObject(p.Key.Item3, p.Value);
							break;

						case SyncCommand.Delete:
							ServiceManager.DeleteObject(p.Key.Item3, p.Key.Item2);
							break;

						case SyncCommand.Update:
							ServiceManager.UpdateObject(p.Key.Item3, p.Key.Item2, p.Value);
							break;
					}
				}
			};
			Thread th = new Thread(thread);
			th.Name = "Sync in thread";
			th.Priority = ThreadPriority.Lowest;
			th.Start();
		}

		#endregion

		#endregion

		#region Enums

		/// <summary>
		/// Describes a command of a synchronization operation.
		/// </summary>
		private enum SyncCommand
		{
			/// <summary>
			/// Create a new object.
			/// </summary>
			Create,

			/// <summary>
			/// Update an existing object.
			/// </summary>
			Update,

			/// <summary>
			/// Delete an existing object.
			/// </summary>
			Delete,

			/// <summary>
			/// Execute a command.
			/// </summary>
			Execute
		}

		#endregion
	}

	#region Delegates
	#endregion

	#region Event arguments
	#endregion

	#region Data structures

	/// <summary>
	/// Describes a synchronization operation.
	/// </summary>
	public class SyncOperation
	{
		/// <summary>
		/// The command.
		/// </summary>
		/// <remarks>
		/// Possible values:
		/// create, delete, update
		/// </remarks>
		public string Command { get; private set; }

		/// <summary>
		/// The type of object.
		/// </summary>
		/// <remarks>
		/// Example values:
		/// playlists, configurations, devices, users
		/// </remarks>
		public string ObjectType { get; private set; }

		/// <summary>
		/// The ID of the object.
		/// </summary>
		/// <remarks>
		/// Not used for the create command.
		/// </remarks>
		public uint ObjectID { get; private set; }

		/// <summary>
		/// The parameters of the command.
		/// </summary>
		/// <remarks>
		/// Not used for the delete command.
		/// </remarks>
		public JObject Params { get; private set; }

		/// <summary>
		/// Create a synchronization operation.
		/// </summary>
		/// <param name="command">The sync command</param>
		/// <param name="objectType">The type of the object</param>
		/// <param name="objectID">The object ID</param>
		/// <param name="parameters">Parameters for the object</param>
		public SyncOperation(string command, string objectType, uint objectID = 0, JObject parameters = null)
		{
			Command = command;
			ObjectType = objectType;
			ObjectID = objectID;
			Params = parameters;
		}

		/// <summary>
		/// Create a synchronization operation.
		/// </summary>
		/// <param name="command">The sync command</param>
		/// <param name="objectType">The type of the object</param>
		/// <param name="parameters">Parameters for the object</param>
		/// <param name="objectID">The object ID</param>
		public SyncOperation(string command, string objectType, JObject parameters = null, uint objectID = 0) :
			this(command, objectType, objectID, parameters)
		{
		}
	}

	#endregion
}