/**
 * Welcome.cs
 * 
 * A task dialog allowing the user to configure file
 * associations.
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
using System.Linq;
using System.Text;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack;
using Microsoft.WindowsAPICodePack.Dialogs;

using System.Windows;

using Stoffi.Core;

namespace Stoffi
{
	/// <summary>
	/// A welcome procedure for new users to the application.
	/// </summary>
	public static class Welcome
	{
		/// <summary>
		/// Shows the dialog.
		/// </summary>
		public static TaskDialogResult Show(IntPtr owner)
		{
			TaskDialog td = new TaskDialog();

			TaskDialogCommandLink cusButton = new TaskDialogCommandLink("cusButton", U.T("AssociationsChoose"), U.T("AssociationsChooseText"));
			TaskDialogCommandLink skipButton = new TaskDialogCommandLink("skipButton", U.T("AssociationsSkip"), U.T("AssociationsSkipText"));
			TaskDialogCommandLink defButton = new TaskDialogCommandLink("defButton", U.T("AssociationsYes"), U.T("AssociationsYesText"));

			defButton.Click += new EventHandler(defButton_Click);
			skipButton.Click += new EventHandler(skipButton_Click);

			td.Controls.Add(defButton);
			td.Controls.Add(cusButton);
			td.Controls.Add(skipButton);

			td.Caption = U.T("AssociationsCaption");
			td.InstructionText = U.T("AssociationsInstruction");
			td.Text = U.T("AssociationsText");
			td.StartupLocation = TaskDialogStartupLocation.CenterOwner;
			td.OwnerWindowHandle = owner;
			return td.Show();
		}

		/// <summary>
		/// Invoked when the user clicks on the default button.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private static void defButton_Click(object sender, EventArgs e)
		{
			TaskDialog td = ((sender as TaskDialogCommandLink).HostingDialog as TaskDialog);
			td.Close(TaskDialogResult.Yes);
		}

		/// <summary>
		/// Invoked when the user clicks on the Skip button.
		/// </summary>
		/// <param name="sender">The sender of the event</param>
		/// <param name="e">The event data</param>
		private static void skipButton_Click(object sender, EventArgs e)
		{
			TaskDialog td = ((sender as TaskDialogCommandLink).HostingDialog as TaskDialog);
			td.Close(TaskDialogResult.Cancel);
		}
	}
}
