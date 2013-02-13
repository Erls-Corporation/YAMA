/**
 * JamendoManager.cs
 * 
 * Takes care of searching and finding music on Jamendo
 * as well as converting results into TrackData structures.
 * 
 * * * * * * * * *
 * 
 * Copyright 2013 Simplare
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
	/// Represents a manager that takes care of talking to Jamendo.
	/// </summary>
	public static class JamendoManager
	{
		#region Members

		private static string uriBase = "http://api.jamendo.com/get2";
		private static string pathPrefix = "stoffi:track:jamendo:";

		#endregion

		#region Properties

		/// <summary>
		/// Gets the current source of tracks used as ItemsSource for the Jamendo track list.
		/// </summary>
		public static ObservableCollection<TrackData> TrackSource { get; private set; }

		#endregion

		#region Methods

		#region Public

		/// <summary>
		/// Returns a list of top tracks
		/// </summary>
		/// <returns>An observable collection of TrackData that represents the top Jamendo tracks</returns>
		public static ObservableCollection<TrackData> TopFeed()
		{
			ObservableCollection<TrackData> tracks = new ObservableCollection<TrackData>();
			try
			{
				string url = CreateURL(new string[] { "order=ratingmonth_desc", "n=50" });
				var request = (HttpWebRequest)WebRequest.Create(url);
				using (var response = (HttpWebResponse)request.GetResponse())
				{
					using (var reader = new StreamReader(response.GetResponseStream()))
					{
						tracks = ParseTracks(JArray.Parse(reader.ReadToEnd()));
					}
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "JAMENDO", "Could not retrieve top tracks: " + e.Message);
			}
			TrackSource = tracks;
			return tracks;
		}

		/// <summary>
		/// Searches Jamendo for tracks matching a certain query.
		/// </summary>
		/// <param name="query">The query to search for</param>
		/// <returns>An observable collection of TrackData with all Jamendo tracks that match query</returns>
		public static ObservableCollection<TrackData> Search(string query)
		{
			ObservableCollection<TrackData> tracks = new ObservableCollection<TrackData>();
			try
			{
				string url = CreateURL(new string[] { "n=50", "order=searchweight_desc", U.CreateParam("searchquery", query, "") });
				var request = (HttpWebRequest)WebRequest.Create(url);
				using (var response = (HttpWebResponse)request.GetResponse())
				{
					using (var reader = new StreamReader(response.GetResponseStream()))
					{
						tracks = ParseTracks(JArray.Parse(reader.ReadToEnd()));
					}
				}
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "JAMENDO", "Could not perform search: " + e.Message);
			}
			TrackSource = tracks;
			return tracks;
		}

		/// <summary>
		/// Retrieves the URL to the thumbnail for a Jamendo track.
		/// </summary>
		/// <param name="track">The Jamendo track</param>
		public static string GetThumbnail(TrackData track)
		{
			return track.ArtURL;
		}

		/// <summary>
		/// Retrieves the stream URL for a Jamendo track
		/// </summary>
		/// <param name="track">The Jamendo track</param>
		public static string GetStreamURL(TrackData track)
		{
			return String.Format("http://storage-new.newjamendo.com/?trackid={0}&format=mp31&u=0", GetID(track.Path));
		}

		/// <summary>
		/// Creates a track given a Jamendo path.
		/// </summary>
		/// <param name="path">The path of the track</param>
		/// <returns>The track if it could be found, otherwise null</returns>
		public static TrackData CreateTrack(string path)
		{
			try
			{
				string url = CreateURL(new string[] { });
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
				U.L(LogLevel.Warning, "JAMENDO", "Could not fetch track: " + e.Message);
			}
			return null;
		}

		/// <summary>
		/// Checks whether a given track path corresponds to a Jamendo track
		/// </summary>
		/// <param name="path">The path of the track to check</param>
		/// <returns>True if the track is a Jamendo track</returns>
		public static bool IsJamendo(string path)
		{
			return path.StartsWith(pathPrefix);
		}

		#endregion

		#region Private

		/// <summary>
		/// Create a request URL.
		/// </summary>
		/// <param name="arguments">A list of arguments in the format "key=value"</param>
		/// <param name="format">The format (either json or xml)</param>
		/// <returns></returns>
		private static string CreateURL(string[] arguments, string format = "json")
		{
			string fields = "id+name+url+stream+duration+genre+image+album_name+album_image+artist_name+artist_image";
			string url = String.Format("{0}/{1}/track/{2}/track_album+album_artist/?", uriBase, fields, format);
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
				TrackData track = new TrackData();
				track.Icon = "pack://application:,,,/Platform/Windows 7/GUI/Images/Icons/Jamendo.ico";

				track.Path = String.Format("{0}{1}", pathPrefix, json["id"]);
				track.Title = (string)json["name"];
				track.Genre = (string)json["genre"];
				track.URL = (string)json["url"];
				track.Artist = (string)json["artist_name"];
				track.Album = (string)json["album_name"];

				int d = (int)json["duration"];
				track.Length = (double)d;

				if (json["image"].Type == JTokenType.String)
					track.ArtURL = (string)json["image"];
				else if (json["album_image"].Type == JTokenType.String)
					track.ArtURL = (string)json["album_image"];
				else if (json["artist_image"].Type == JTokenType.String)
					track.ArtURL = (string)json["artist_image"];
				track.Image = track.ArtURL;

				if (track.Image == null || track.Image == "")
					track.Image = track.Icon;

				return track;
			}
			catch (Exception e)
			{
				U.L(LogLevel.Warning, "JAMENDO", "Could not parse track JSON data: " + e.Message);
				return null;
			}
		}

		/// <summary>
		/// Extracts the track ID of a Jamendo track's path
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

			throw new Exception("Trying to extract Jamendo track ID from non-Jamendo track: " + path);
		}

		#endregion

		#endregion
	}
}
