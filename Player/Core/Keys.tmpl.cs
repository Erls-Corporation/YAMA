using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stoffi.Core
{
	/// <summary>
	/// All API keys and secrets.
	/// </summary>
	public static partial class U
	{
		/// <summary>
		/// Checks if you have added the keys, secrets, etc.
		/// </summary>
		public static void Check()
		{
			string[] strings = new string[] { StoffiKey, StoffiSecret, LastFMKey, BassMail, BassKey };
			if (strings.Contains(""))
				throw new Exception("You need to add API keys. See http://dev.stoffiplayer.com/wiki/GetStarted for more info.");
		}

		/// <summary>
		/// The key for the stoffiplayer.com service
		/// </summary>
		public static string StoffiKey = "";

		/// <summary>
		/// The secret for the stoffiplayer.com service
		/// </summary>
		public static string StoffiSecret = "";

		/// <summary>
		/// The key for the Last.fm service
		/// </summary>
		public static string LastFMKey = "";

		/// <summary>
		/// The email for the Bass library.
		/// </summary>
		public static string BassMail = "";

		/// <summary>
		/// The key for the Bass library.
		/// </summary>
		public static string BassKey = "";
	}
}
