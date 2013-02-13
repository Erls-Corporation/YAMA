/**
 * YouTubeManager.cs
 * 
 * Takes care of searching and finding music on YouTube
 * as well as converting results into TrackData structures.
 * 
 * * * * * * * * *
 * 
 * Copyright 2011 Simplare
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Google.GData.Client;
using Google.GData.Extensions;
using Google.GData.YouTube;
using Google.GData.Extensions.MediaRss;
using Google.YouTube;

namespace Stoffi.Core
{
	/// <summary>
	/// Represents a manager that takes care of talking to YouTube
	/// </summary>
	public static class YouTubeManager
	{
		#region Members
		private static YouTubeRequestSettings settings = new YouTubeRequestSettings("Stoffi", "AI39si4y_vkAW2Ngyc2BlMdgkBghua2w5hheyesEI-saNU_CNDIMs5YMPpIBk-HpmFG4qDPAHAvE_YYNWH5qV5S1x5euKKRodw");
		private static string pathPrefix = "stoffi:track:youtube:";
		#endregion

		#region Properties
		/// <summary>
		/// Gets or sets whether the user has Adobe Flash installed or not
		/// </summary>
		public static bool HasFlash { get; set; }

		/// <summary>
		/// Gets the current source of tracks used as ItemsSource for the YouTube track list.
		/// </summary>
		public static ObservableCollection<TrackData> TrackSource { get; private set; }
		#endregion

		#region Methods

		#region Public

		/// <summary>
		/// Returns a list of tracks from one of the YouTube feeds
		/// </summary>
		/// <param name="feed">The feed</param>
		/// <returns>An observable collection of TrackData that represents the most viewed YouTube tracks</returns>
		public static ObservableCollection<TrackData> TopFeed(string feed)
		{
			ObservableCollection<TrackData> tracks = new ObservableCollection<TrackData>();
			YouTubeRequest request = new YouTubeRequest(settings);

			int maxFeedItems = 50;

			int i = 1;
			Feed<Video> videoFeed = request.Get<Video>(new Uri("http://gdata.youtube.com/feeds/api/standardfeeds/" + feed + "_Music?format=5"));
			foreach (Video entry in videoFeed.Entries)
			{
				if (i++ > maxFeedItems) break;
				if (entry != null)
					tracks.Add(CreateTrack(entry));
			}

			TrackSource = tracks;

			return tracks;
		}

		/// <summary>
		/// Searches YouTube for tracks matching a certain query
		/// </summary>
		/// <param name="query">The query to search for</param>
		/// <returns>An observable collection of TrackData with all YouTube tracks that match query</returns>
		public static ObservableCollection<TrackData> Search(string query)
		{
			ObservableCollection<TrackData> tracks = new ObservableCollection<TrackData>();

			AtomCategory category = new AtomCategory("Music", YouTubeNameTable.CategorySchema);

			YouTubeQuery q = new YouTubeQuery(YouTubeQuery.DefaultVideoUri);
			q.OrderBy = "relevance";
			q.Query = query;
			q.Formats.Add(YouTubeQuery.VideoFormat.Embeddable);
			q.NumberToRetrieve = 50;
			q.SafeSearch = YouTubeQuery.SafeSearchValues.None;
			q.Categories.Add(new QueryCategory(category));

			try
			{
				YouTubeRequest request = new YouTubeRequest(settings);

				Feed<Video> videoFeed = request.Get<Video>(q);
				foreach (Video entry in videoFeed.Entries)
				{
					tracks.Add(YouTubeManager.CreateTrack(entry));
				}
			}
			catch (Exception exc)
			{
				U.L(LogLevel.Error, "YOUTUBE", "Error while performing search: " + exc.Message);
			}

			TrackSource = tracks;

			return tracks;
		}

		/// <summary>
		/// Retrieves the URL to the thumbnail for a YouTube track
		/// </summary>
		/// <param name="track">The YouTube track</param>
		public static string GetThumbnail(TrackData track)
		{
			if (IsYouTube(track))
				return "https://img.youtube.com/vi/" + GetYouTubeID(track.Path) + "/1.jpg";
			else
				return "";
		}

		/// <summary>
		/// Retrieves the URL for a YouTube track
		/// </summary>
		/// <param name="track">The YouTube track</param>
		public static string GetURL(TrackData track)
		{
			if (IsYouTube(track))
				return "https://www.youtube.com/watch?v=" + GetYouTubeID(track.Path);
			else
				return "";
		}

		/// <summary>
		/// Creates a track using a YouTube video ID
		/// </summary>
		/// <param name="path">The path of the track</param>
		/// <returns>A TrackData structure representing the YouTube track</returns>
		public static TrackData CreateTrack(string path)
		{
			try
			{
				string id = GetYouTubeID(path);
				YouTubeRequest request = new YouTubeRequest(settings);
				Uri url = new Uri("http://gdata.youtube.com/feeds/api/videos/" + id);
				Video v = request.Retrieve<Video>(url);
				if (v == null)
				{
					U.L(LogLevel.Warning, "YOUTUBE", "Could not find video with ID '" + id + "'");
					return null;
				}
				return CreateTrack(v);
			}
			catch (Exception e)
			{
				throw e;
				//return new TrackData { Title = "Error", Artist = "Error" };
			}
		}

		/// <summary>
		/// Creates a track using a YouTube video entry
		/// </summary>
		/// <param name="v">The video entry</param>
		/// <returns>A TrackData structure representing the YouTube track</returns>
		public static TrackData CreateTrack(Video v)
		{
			TrackData track = new TrackData();
			track.Path = pathPrefix + v.VideoId;
			track.Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/YouTube.ico";

			track.ArtURL = GetThumbnail(track);
			track.Image = track.ArtURL;

			track.Bookmarks = new List<double>();
			track.Processed = true;
			track.Length = Convert.ToDouble(v.Media.Duration.Seconds);
			string[] str = U.ParseTitle(v.Title);
			track.Artist = str[0];
			track.Title = str[1];
			if (String.IsNullOrWhiteSpace(track.Artist))
				track.Artist = v.Uploader;
			track.Views = v.ViewCount;
			track.URL = "https://www.youtube.com/watch?v=" + v.VideoId;
			return track;
		}

		/// <summary>
		/// Checks whether a given track is a youtube track
		/// </summary>
		/// <param name="t">The track to check</param>
		/// <returns>True if the track is a youtube track</returns>
		public static bool IsYouTube(TrackData t)
		{
			return (t != null && IsYouTube(t.Path));
		}

		/// <summary>
		/// Checks whether a given track path corresponds to a youtube track
		/// </summary>
		/// <param name="path">The path of the track to check</param>
		/// <returns>True if the track is a YouTube track</returns>
		public static bool IsYouTube(string path)
		{
			return path.StartsWith(pathPrefix);
		}

		/// <summary>
		/// Checks whether a given URL is a well formatted adress to a playlist
		/// </summary>
		/// <param name="url">The URL to check</param>
		/// <returns>True if the URL is a well formatted adress to a playlist</returns>
		public static bool IsPlaylist(string url)
		{
			string pattern = @"https?://(www\.)?youtube.com/playlist\?(\w+=[^&=]*&)*list=(PLA)?\w+(&\w+=[^&=]*)*";
			Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);

			return rgx.IsMatch(url);
		}

		/// <summary>
		/// Parses a URL to a playlist.
		/// </summary>
		/// <param name="url">The URL to the playlist object</param>
		/// <returns>A collection of the tracks corresponding to the playlist</returns>
		public static PlaylistData ParseURL(string url)
		{
			PlaylistData pl = new PlaylistData();
			pl.Tracks = new ObservableCollection<TrackData>();
			string id = GetPlaylistID(url);
			YouTubeRequest request = new YouTubeRequest(settings);
			Uri uri = new Uri("https://gdata.youtube.com/feeds/api/playlists/" + id + "?v=2");
			Feed<Video> feed = request.Get<Video>(uri);
			
			if (feed != null)
			{
				pl.Name = feed.AtomFeed.Title.Text;
				try
				{
					foreach (Video v in feed.Entries)
					{
						try
						{
							pl.Tracks.Add(CreateTrack(v));
						}
						catch (Exception e)
						{
							U.L(LogLevel.Warning, "YOUTUBE", "Problem adding track from playlist '" + pl.Name + "': " + e.Message);
						}
					}
				}
				catch (Exception exc)
				{
					U.L(LogLevel.Warning, "YOUTUBE", "Problem parsing playlist '" + pl.Name + "': " + exc.Message);
				}
			}
			else
			{
				U.L(LogLevel.Warning, "YOUTUBE", "Could not find playlist with ID '" + id + "'");
				return null;
			}

			return pl;
		}

		/// <summary>
		/// Extracts the video ID of a YouTube track's path.
		/// </summary>
		/// <param name="path">The path of the track</param>
		/// <returns>The video ID</returns>
		public static string GetYouTubeID(string path)
		{
			if (IsYouTube(path))
			{
				path = path.Substring(pathPrefix.Length);
				if (path[path.Length - 1] == '/')
					path = path.Substring(0, path.Length - 1);
				return path;
			}

			throw new Exception("Trying to extract YouTube video ID from non-YouTube track: " + path);
		}

		/// <summary>
		/// Extracts the playlist ID of a YouTube playlist's URL.
		/// </summary>
		/// <param name="path">The URL of the playlist</param>
		/// <returns>The playlist ID</returns>
		public static string GetPlaylistID(string url)
		{
			if (IsPlaylist(url))
			{
				string pattern = @"https?://(www\.)?youtube.com/playlist\?(\w+=[^&=]*&)*list=(\w+)";
				Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
				Match m = rgx.Match(url);
				if (m != null)
				{
					GroupCollection groups = m.Groups;
					return groups[groups.Count - 1].Value;
				}
			}

			throw new Exception("Trying to extract YouTube playlist ID from non-YouTube playlist: " + url);
		}

		#endregion

		#region Private

		#endregion

		#endregion
	}

	/// <summary>
	/// Describes the interface that the chromeless YouTube player can call via JavaScript
	/// </summary>
	[ComVisibleAttribute(true)]
	public class YouTubePlayerInterface
	{
		/// <summary>
		/// Invoked when an error occurs within the YouTube player
		/// </summary>
		/// <param name="errorCode">The error code</param>
		public void OnVideoError(int errorCode)
		{
			switch (errorCode)
			{
				case 2:
					U.L(LogLevel.Error, "YOUTUBE", "Player reported that we used bad parameters");
					break;

				case 100:
					U.L(LogLevel.Error, "YOUTUBE", "Player reported that the track has either been removed or marked as private");
					break;

				case 101:
				case 150:
					U.L(LogLevel.Error, "YOUTUBE", "Player reported that the track is restricted");
					break;

				default:
					U.L(LogLevel.Error, "YOUTUBE", "Player reported an unknown error code: " + errorCode);
					break;
			}
			DispatchError(errorCode.ToString());
		}

		/// <summary>
		/// Invoked when user tries to play a youtube track but doesn't have flash installed
		/// </summary>
		public void OnNoFlash()
		{
			DispatchNoFlashDetected();
		}

		/// <summary>
		/// Invoked when the player changes state
		/// </summary>
		/// <param name="state">The new state of the player</param>
		public void OnStateChanged(int state)
		{
			switch (state)
			{
				case -1: // unstarted
					break;

				case 0: // ended
					SettingsManager.MediaState = MediaState.Ended;
					break;

				case 1: // playing
					SettingsManager.MediaState = MediaState.Playing;
					break;

				case 2: // paused
					SettingsManager.MediaState = MediaState.Paused;
					break;

				case 3: // buffering
					break;

				case 5: // cued
					break;
			}
		}

		/// <summary>
		/// Invoked when player is ready
		/// </summary>
		public void OnPlayerReady()
		{
			DispatchPlayerReady();
		}

		/// <summary>
		/// Dispatches the ErrorOccured event
		/// </summary>
		/// <param name="message">The error message</param>
		private void DispatchError(string message)
		{
			if (ErrorOccured != null)
				ErrorOccured(this, message);
		}

		/// <summary>
		/// Dispatches the NoFlashDetected event
		/// </summary>
		private void DispatchNoFlashDetected()
		{
			if (NoFlashDetected != null)
				NoFlashDetected(this, new EventArgs());
		}

		/// <summary>
		/// Dispatches the PlayerReady event
		/// </summary>
		private void DispatchPlayerReady()
		{
			if (PlayerReady != null)
				PlayerReady(this, new EventArgs());
		}

		/// <summary>
		/// Occurs when there's an error from the player
		/// </summary>
		public event ErrorEventHandler ErrorOccured;

		/// <summary>
		/// Occurs when the user tries to play a youtube track but there's no flash installed
		/// </summary>
		public event EventHandler NoFlashDetected;

		/// <summary>
		/// Occurs when the player is ready
		/// </summary>
		public event EventHandler PlayerReady;
	}
}
