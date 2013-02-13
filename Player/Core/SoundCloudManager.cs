/**
 * SoundCloudManager.cs
 * 
 * Takes care of searching and finding music on SoundCloud
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
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

using Newtonsoft.Json.Linq;

namespace Stoffi.Core
{
	/// <summary>
	/// Represents a manager that takes care of talking to SoundCloud
	/// </summary>
	public static class SoundCloudManager
	{
		#region Members

		private static string uriBase = "https://api.soundcloud.com";
		private static string clientID = "2ad7603ebaa9cd252eabd8dd293e9c40";
		private static string pathPrefix = "stoffi:track:soundcloud:";
		private static int failedRequests = 0;

		#endregion

		#region Properties

		/// <summary>
		/// Gets the current source of tracks used as ItemsSource for the SoundCloud track list.
		/// </summary>
		public static ObservableCollection<TrackData> TrackSource { get; private set; }

		#endregion

		#region Methods

		#region Public

		/// <summary>
		/// Returns a list of top tracks
		/// </summary>
		/// <returns>An observable collection of TrackData that represents the top SoundCloud tracks</returns>
		public static ObservableCollection<TrackData> TopFeed()
		{
			ObservableCollection<TrackData> tracks = new ObservableCollection<TrackData>();
			try
			{
				string url = CreateURL("tracks", new string[] { "order=hotness", "limit=50" });
				var request = (HttpWebRequest)WebRequest.Create(url);
				using (var response = (HttpWebResponse)request.GetResponse())
				{
					using (var reader = new StreamReader(response.GetResponseStream()))
					{
						tracks = ParseTracks(JArray.Parse(reader.ReadToEnd()));
					}
				}
				failedRequests = 0;
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SOUNDCLOUD", "Could not retrieve top tracks: " + e.Message);
				failedRequests++;
				if (failedRequests < 3)
					tracks = TopFeed();
			}
			TrackSource = tracks;
			return tracks;
		}

		/// <summary>
		/// Searches SoundCloud for tracks matching a certain query
		/// </summary>
		/// <param name="query">The query to search for</param>
		/// <returns>An observable collection of TrackData with all SoundCloud tracks that match query</returns>
		public static ObservableCollection<TrackData> Search(string query)
		{
			ObservableCollection<TrackData> tracks = new ObservableCollection<TrackData>();
			try
			{
				string url = CreateURL("tracks", new string[] { "limit=50", U.CreateParam("q", query, "") });
				var request = (HttpWebRequest)WebRequest.Create(url);
				using (var response = (HttpWebResponse)request.GetResponse())
				{
					using (var reader = new StreamReader(response.GetResponseStream()))
					{
						tracks = ParseTracks(JArray.Parse(reader.ReadToEnd()));
					}
				}
				failedRequests = 0;
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SOUNDCLOUD", "Could not perform search: " + e.Message);
				failedRequests++;
				if (failedRequests < 3)
					tracks = TopFeed();
			}
			TrackSource = tracks;
			return tracks;
		}

		/// <summary>
		/// Retrieves the URL to the thumbnail for a SoundCloud track
		/// </summary>
		/// <param name="track">The SoundCloud track</param>
		public static string GetThumbnail(TrackData track)
		{
			return track.ArtURL;
		}

		/// <summary>
		/// Retrieves the stream URL for a SoundCloud track
		/// </summary>
		/// <param name="track">The SoundCloud track</param>
		public static string GetStreamURL(TrackData track)
		{
			return String.Format("{0}/tracks/{1}/stream?client_id={2}", uriBase, track.Path.Substring(pathPrefix.Length), clientID);
		}

		/// <summary>
		/// Creates a track given a SoundCloud path.
		/// </summary>
		/// <param name="path">The path of the track</param>
		/// <returns>The track if it could be found, otherwise null</returns>
		public static TrackData CreateTrack(string path)
		{
			try
			{
				string url = CreateURL("tracks/" + GetID(path), new string[] {});
				var request = (HttpWebRequest)WebRequest.Create(url);
				using (var response = (HttpWebResponse)request.GetResponse())
				{
					using (var reader = new StreamReader(response.GetResponseStream()))
					{
						return ParseTrack(JObject.Parse(reader.ReadToEnd()));
					}
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SOUNDCLOUD", "Could not fetch track: " + e.Message);
			}
			return null;
		}

		/// <summary>
		/// Checks whether a given track path corresponds to a soundcloud track
		/// </summary>
		/// <param name="path">The path of the track to check</param>
		/// <returns>True if the track is a soundcloud track</returns>
		public static bool IsSoundCloud(string path)
		{
			return path.StartsWith(pathPrefix);
		}

		#endregion

		#region Private

		/// <summary>
		/// Create a request URL.
		/// </summary>
		/// <param name="property">The property object</param>
		/// <param name="arguments">A list of arguments in the format "key=value"</param>
		/// <param name="format">The format (either json or xml)</param>
		/// <returns></returns>
		private static string CreateURL(string property, string[] arguments, string format = "json")
		{
			string url = String.Format("{0}/{1}.{2}?client_id={3}", uriBase, property, format, clientID);
			foreach (string arg in arguments)
				url += "&" + arg;
			return url;
		}

		/// <summary>
		/// Parses a JSON array into a list of tracks.
		/// </summary>
		/// <param name="json">The JSON data</param>
		/// <returns>A list of tracks</returns>
		private static ObservableCollection<TrackData> ParseTracks(JArray json)
		{
			ObservableCollection<TrackData> tracks = new ObservableCollection<TrackData>();
			foreach (JObject o in json)
			{
				TrackData t = ParseTrack(o);
				if (t != null)
					tracks.Add(t);
			}
			return tracks;
		}

		/// <summary>
		/// Parses a JSON object into a track.
		/// </summary>
		/// <param name="json">The JSON data</param>
		/// <returns>A track</returns>
		private static TrackData ParseTrack(JObject json)
		{
			if (json == null) return null;
			try
			{
				JObject user = (JObject)json["user"];

				TrackData track = new TrackData();
				track.Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/SoundCloud.ico";

				track.Path = String.Format("{0}{1}", pathPrefix, json["id"]);
				track.Length = Convert.ToDouble((int)json["duration"]) / 1000.0;
				track.Genre = (string)json["genre"];
				track.ArtURL = (string)json["artwork_url"];
				track.URL = (string)json["permalink_url"];

				track.Image = track.ArtURL;
				if (track.Image == null || track.Image == "")
					track.Image = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/SoundCloud.ico";

				JToken token = json["release_year"];
				if (token != null && token.Type != JTokenType.Null)
					track.Year = (uint)token;

				string[] str = U.ParseTitle((string)json["title"]);
				track.Artist = str[0];
				track.Title = str[1];
				if (String.IsNullOrWhiteSpace(track.Artist) && user != null)
					track.Artist = (string)user["username"];

				return track;
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "SOUNDCLOUD", "Could not parse track JSON data: " + e.Message);
				return null;
			}
		}

		/// <summary>
		/// Extracts the track ID of a SoundCloud track's path
		/// </summary>
		/// <param name="path">The path of the track</param>
		/// <returns>The track ID</returns>
		public static string GetID(string path)
		{
			if (path.StartsWith(pathPrefix))
			{
				path = path.Substring(pathPrefix.Length);
				if (path[path.Length - 1] == '/')
					path = path.Substring(0, path.Length - 1);
				return path;
			}

			throw new Exception("Trying to extract SoundCloud track ID from non-SoundCloud track: " + path);
		}

		#endregion

		#endregion
	}
}
