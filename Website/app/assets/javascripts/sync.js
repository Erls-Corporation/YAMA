
/* * * * * * * * * * UPDATE * * * * * * * * * */

/*
 * Updates a synchronizable object.
 * 
 * This is called when an object is changed and we want to
 * modify it in real time for any user seeing it.
 *
 * @param object_type
 *   The type of the object.
 *
 * @param object_id
 *   The ID of the object.
 *
 * @param updated_params
 *   The parameters that were changed, encoded in JSON.
 */
function updateObject(object_type, object_id, updated_params)
{
	if (typeof(embedded) != "undefined")
		window.external.UpdateObject(object_type, object_id, updated_params);
	else
	{
		switch (object_type)
		{
			case "device":
				updateDevice(object_id, updated_params);
				break;
				
			case "configuration":
				updateConfig(object_id, updated_params);
				break;
				
			case "playlist":
				updatePlaylist(object_id, updated_params);
				break;
				
			case "listen":
				//updateListen(object_id, updated_params);
				break;
				
			case "link":
				updateLink(object_id, updated_params);
				break;
		}
	}
}

/*
 * Updates a device.
 * 
 * Called whenever a device is modified and we need
 * to adjust any property of a device that the user may
 * be viewing.
 *
 * @param object_id
 *   The ID of the device object.
 *
 * @param updated_params
 *   The parameters that were changed, encoded in JSON.
 */
function updateDevice(object_id, updated_params)
{
	params = JSON.parse(updated_params);
	for (field in params)
	{
		switch (field)
		{
			case "name":
			case "update_at":
			case "lastip":
				$('[data-field="device-'+object_id+'-'+field+'"]').text(params[field]);
				break;
				
			case "status":
				txt = trans[locale]['device.status.'+params[field]].toLowerCase();
				$('[data-field="device-'+object_id+'-status"]').text(txt);
				$('[data-field="device-'+object_id+'-status"]').attr("data-status", params[field]);
				if (params["status"] == "online")
				{
					$('[data-object="device-'+object_id+'"]').removeClass("inactive");
					$('[data-object="device-'+object_id+'"]').addClass("active");
				}
				else
				{
					$('[data-object="device-'+object_id+'"]').addClass("inactive");
					$('[data-object="device-'+object_id+'"]').removeClass("active");
				}
				break;
				
			default:
				break;
		}
	}
}

/*
 * Updates a link.
 * 
 * Called whenever a link is modified and we need
 * to adjust any property of a link that the user may
 * be viewing.
 *
 * @param object_id
 *   The ID of the link object.
 *
 * @param updated_params
 *   The parameters that were changed, encoded in JSON.
 */
function updateLink(object_id, updated_params)
{
	params = JSON.parse(updated_params);
	for (field in params)
	{
		switch (field)
		{
			case "do_share":
			case "do_listen":
			case "do_create_playlist":
			case "do_donate":
			case "do_button":
				$('[data-field="link-'+object_id+'-'+field+'"]').attr('checked', params[field] == "true");
				break;
				
			default:
				break;
		}
	}
}

/*
 * Updates a configuration.
 * 
 * Called whenever a configuration is modified and we need
 * to adjust any configuration state that the user may be
 * viewing.
 *
 * @param object_id
 *   The ID of the configuration object.
 *
 * @param updated_params
 *   The parameters that were changed, encoded in JSON.
 */
function updateConfig(object_id, updated_params)
{
	params = JSON.parse(updated_params);
	for (field in params)
	{
		switch (field)
		{
			case "media_state":
				setMediaState(object_id, params[field]);
				break;
				
			case "repeat":
				setRepeatState(object_id, params[field]);
				break;
				
			case "shuffle":
				setShuffleState(object_id, params[field]);
				break;
				
			case "volume":
				setVolumeLevel(object_id, params[field]);
				break;
				
			case "now_playing":
				setNowPlaying(object_id, params[field]);
				break;
				
			default:
				break;
		}
	}
}

/*
 * Updates a playlist.
 * 
 * Called whenever a playlist is modified and we need
 * to adjust what the user may be viewing.
 *
 * @param object_id
 *   The ID of the playlist object.
 *
 * @param updated_params
 *   The parameters that were changed, encoded in JSON.
 */
function updatePlaylist(object_id, updated_params)
{
	var params = JSON.parse(updated_params);
	if (params['name'] != null)
		$('[data-field="playlist-'+object_id+'-name"]').text(params['name']);
		
	if (params['songs'] != null)
	{
		if (params['songs']['added'] != null)
		{
			for (var i=0; i < params['songs']['added'].length; i++)
			{
				var name = params['songs']['added'][i]['title'];
				var picture = params['songs']['added'][i]['picture'];
				var id = params['songs']['added'][i]['id'];

				var url = "/"+locale+"/songs/"+id;
				var title = trans[locale]['playlists.songs.remove'].replace("%{song}", name);
				var e = "<li data-object=\"song-"+id+"\" style='display:none;'>";
				e += "<div class=\"delete-wrap\"><a class=\"delete\" href=\"#\" ";
				e += "onclick=\"removeSong('"+url+".json', 'song_"+id+"', true, event); return false;\" ";
				e += "title=\""+title+"\">x</a></div>";
				e += "<a href=\""+url+"\" class=\"item\">";
				e += "<img height='120' width='120' src='"+picture+"'/>";
				e += "<p data-field=\"song-"+id+"-title\">"+name+"</p>";
				e += "</a></li>";

				var inserted = false;
				
				var items = $('[data-field="playlist-'+object_id+'-songs"]').children('li')

				for (j=0; j < items.length; j++)
				{
					if (name < $(items[j]).find('p:first').text())
					{
						$(items[j]).before(e);
						inserted = true;
						break;
					}
				}

				if (!inserted)
					$('[data-field="playlist-'+object_id+'-songs"]').append(e);
					
				$('#song_'+id).show('slide', { direction: 'left' });
				if ($('[data-field="playlist-'+object_id+'-songs"] li').length == 1)
					$('[data-field="playlist-'+object_id+'-empty"]').hide('slide', { direction: 'left' });
			}
		}
	}
	if (params['songs']['removed'] != null)
	{
		for (var i=0; i < params['songs']['removed'].length; i++)
		{
			var id = params['songs']['removed'][i]['id'];
			if ($('[data-object="song-'+id+'"]').is(':visible'))
			{
				$('[data-object="song-'+id+'"]').hide('slide', { direction: 'left' }, function()
				{
					$(this).remove();
					if ($('[data-field="playlist-'+object_id+'-songs"] li').length == 0)
						$('[data-field="playlist-'+object_id+'-empty"]').show('slide', { direction: 'left' });
				});
			}
		}
	}
}
 

/* * * * * * * * * * CREATE * * * * * * * * * */
 
/*
 * Creates a synchronizable object.
 * 
 * This is called when an object is created and we want to
 * show it in real time for any user supposed to see it.
 *
 * @param object_type
 *   The type of the object.
 *
 * @param object_params
 *   The parameters of the object, encoded in JSON.
 */
function createObject(object_type, object_params)
{
	if (typeof(embedded) != "undefined")
		window.external.CreateObject(object_type, object_params);
	else
	{
		switch (object_type)
		{
			case "device":
				createDevice(object_params);
				break;
				
			case "configuration":
				//createConfig(object_params);
				break;
				
			case "playlist":
				createPlaylist(object_params);
				break;
				
			case "listen":
				createListen(object_params);
				break;
		}
	}
}

/*
 * Creates a device.
 * 
 * Called whenever a device is created and we need
 * to show it in real time for any user who should
 * be seeing it.
 *
 * @param params
 *   The parameters of the device, encoded in JSON.
 */
function createDevice(object_params)
{
	var params = JSON.parse(object_params);
	var name = params["name"];
	var id = params["id"];
	var status = params["status"];
	var version = params["version"];
	var statusLabel = trans[locale]['device.status.label'].toLowerCase();
	var statusText = trans[locale]['device.status.'+status].toLowerCase();

	var url = "/"+locale+"/devices/"+id;
	var title = trans[locale]['delete'].replace("%{item}", name);
	var c = "inactive";
	if (status == "active")
		c = "active";
	
	var e = "<li data-object='device-"+id+"' style='display:none;' class='"+c+"'>";
	e += "<div class='delete-wrap'>";
	e += "<a class=\"delete\" href=\"#\" ";
	e += "onclick=\"removeItem('"+url+".json', 'device-"+id+"', event, 'devices'); return false;\" ";
	e += "title=\""+title+"\">x</a>";
	e += "</div>";
	e += "<a href='"+url+"' class='item'>";
	e += "<div class='text'>";
	e += "<p data-field='device-"+id+"-name'>"+name+"</p>";
	e += "<p class='meta'>"+statusLabel+": ";
	e += "<span data-field='device-"+id+"-status' data-status='"+status+"'>"+statusText+"</span>";
	e += "</p></div></a></li>";

	var inserted = false;
	
	var items = $('[data-list="devices"]').children('li');

	for (i=0; i < items.length; i++)
	{
		if (name < $(items[i]).find('p:first').text())
		{
			$(items[i]).before(e);
			inserted = true;
			break;
		}
	}

	if (!inserted)
		$('[data-list="devices"]').append(e);
		
	$('[data-object="device-'+id+'"]').slideDown();
	$('[data-field="no-devices"]').slideUp();
}

/*
 * Creates a playlist.
 * 
 * Called whenever a playlist is created and we need
 * to show it in real time for any user who should
 * be seeing it.
 *
 * @param params
 *   The parameters of the playlist, encoded in JSON.
 */
function createPlaylist(object_params)
{
	var params = JSON.parse(object_params);
	var name = params["name"];
	var id = params["id"];

	var url = "/"+locale+"/playlists/"+id;
	var title = trans[locale]['delete'].replace("%{item}", name);
	
	var e = "<li data-object='playlist-"+id+"' style='display:none;'>";
	e += "<div class='delete-wrap'>";
	e += "<a class=\"delete\" href=\"#\" ";
	e += "onclick=\"removeItem('"+url+".json', 'playlist-"+id+"', event, 'playlists'); return false;\" ";
	e += "title=\""+title+"\">x</a>";
	e += "</div>";
	e += "<a href='"+url+"' class='item'>";
	e += "<div class='text'>";
	e += "<p data-field='playlist-"+id+"-name'>"+name;
	e += "</p></div></a></li>";

	var inserted = false;
	
	var items = $('[data-list="playlists"]').children('li');

	for (i=0; i < items.length; i++)
	{
		if (name < $(items[i]).find('p:first').text())
		{
			$(items[i]).before(e);
			inserted = true;
			break;
		}
	}

	if (!inserted)
		$('[data-list="playlists"]').append(e);
		
	$('[data-object="playlist-'+id+'"]').slideDown();
	$('[data-field="no-playlists"]').slideUp();
}

/*
 * Creates a listen.
 * 
 * Called whenever a listen is created and we need
 * to show it in real time for any user who should
 * be seeing it.
 *
 * @param object_params
 *   The parameters of the object, encoded in JSON.
 */
function createListen(object_params)
{
	console.log("create listen");
	console.log(object_params);
	var params = JSON.parse(object_params);
	var title = params["title"];
	var id = params["id"];
	var song_id = params["song_id"];
	var picture = params["picture"];
	
	console.log("building element");

	var url = "/"+locale+"/songs/"+song_id;
	var e = "<li data-object=\"listen-"+id+"\" style='display:none;'>";
	e += "<a href=\""+url+"\" class=\"item\">";
	e += "<img alt='"+title+"' height='120' width='120' src='"+picture+"'/>";
	e += "<div class='text'>"
	e += "<p data-field=\"song-"+song_id+"-title\">"+title+"</p>";
	e += "</div></a></li>";
	
	console.log("inserting element");

	$('[data-list="listens"]').prepend(e);
	
	console.log("removing trailing element");
	var list = $('[data-list="listens"]');
	var max = list.attr('data-maxLength');
	var items = list.children('li');
	
	$('[data-field="no-listens"]').slideUp();
	
	var last = list.find('li:last');
	if (max && items.length > max && last)
	{
		last.slideUp(400, function()
		{
			last.remove();
			console.log("showing element");
			$('[data-object="listen-'+id+'"]').slideDown();
		});
	}
	else
	{
		console.log("showing element");
		$('[data-object="listen-'+id+'"]').slideDown();
	}
}

/* * * * * * * * * * DELETE * * * * * * * * * */

/*
 * Deletes a synchronizable object.
 * 
 * This is called when an object is deleted and we want to
 * removed it in real time for any user seeing it.
 *
 * @param object_type
 *   The type of the object.
 *
 * @param object_id
 *   The ID of the object.
 */
function deleteObject(object_type, object_id)
{
	if (typeof(embedded) != "undefined")
		window.external.DeleteObject(object_type, object_id);
	else
	{
		switch (object_type)
		{
			case "device":
				//deleteDevice(object_id);
				break;
				
			case "configuration":
				//deleteConfig(object_id);
				break;
				
			case "playlist":
				deletePlaylist(object_id);
				break;
				
			case "listen":
				//deleteListen(object_id);
				break;
		}	
	}
}

/*
 * Deletes a playlist.
 * 
 * This is called when a playlist is deleted and we
 * want to removed it in real time for any user
 * seeing it.
 *
 * @param object_id
 *   The ID of the playlist.
 */
function deletePlaylist(object_id)
{
	$('[data-object="playlist-'+object_id+'"]').slideUp("normal", function()
	{
		$(this).remove();
		if ($('[data-list="playlists"] li').length == 0)
			$('[data-field="no-playlists"]').slideDown();
	});
}

/* * * * * * * * * * EXECUTE * * * * * * * * * */

/*
 * Executes a command.
 * 
 * Sends the execution of a command.
 *
 * @param command
 *   The command name.
 *
 * @param config_id
 *   The ID of the configuration that the command is associated with.
 */
function execute(command, config_id)
{
	if (typeof(embedded) != "undefined")
	{
		window.external.Execute(command, config_id);
	}
}