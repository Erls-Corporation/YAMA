function removeSong(playlist_id, song_id, submit, event)
{
	event.stopPropagation();
	
	console.log("removeSong");
	
	if (!submit || confirm(trans[locale]['confirm']))
	{
		console.log("perform!");
		if ($('#add_rem_track_'+song_id).length > 0)
			$('#add_rem_track_'+song_id).remove();
			
		var playlist = '#playlist_'+playlist_id;
		if (playlist_id == -1)
			playlist = '#new_playlist'
		else if ($(playlist).length == 0 && $('#edit_playlist_'+playlist_id).length > 0)
			playlist = '#edit_playlist_'+playlist_id;
			
		$(playlist + ' [data-object="song-'+song_id+'"]').hide('slide', { direction: 'left' });
		
		if (submit)
		{
			$.ajax({
				url: '/playlists/'+playlist_id+'.json',
				data: "tracks[removed][][id]="+song_id,
				type: 'PUT',
				error: function(jqXHR)
				{
					if (jqXHR.status != 200)
						$('#song_'+song_id).show('slide', { direction: 'left' });
				},
				success: function()
				{
					$('#song_'+song_id).remove();
					if ($('#playlist_'+playlist_id+' [data-field="songs"] li').length == 0)
						$('#playlist_'+playlist_id+' [data-field="empty"]').show('slide', { direction: 'left' });
				}
			});
		}
		else if (playlist_id != -1)
		{
			$('#tracks_removed').append(
				"<div id='add_rem_track_"+song_id+"'>"+
				"<input type='hidden' name='tracks[removed][][id]' value='"+song_id+"'/>"+
				"</div>"
			);
		}
	}
}

function addSong(playlist_id, id, path, name, length, art_url, url, artist, album, genre, submit)
{
	if ($('#add_rem_track_'+id).length > 0)
		$('#add_rem_track_'+id).remove();
		
	console.log("addSong("+playlist_id+","+id+","+path+","+name+")");
		
	if (playlist_id == -1 || $('#song_'+id).length == 0)
	{
		if (art_url == "" || art_url == null)
			var picture = "/assets/media/disc.png";
		else
			var picture = art_url;

		var title = trans[locale]['playlists.songs.remove'].replace("%{song}", name);
		var e = "<li data-object=\"song-"+id+"\" style='display:none;'>";
		e += "<div class=\"delete-wrap\"><a class=\"delete\" href=\"#\" ";
		e += "onclick=\"removeSong('"+playlist_id+"', '"+id+"', "+submit+", event); return false;\" ";
		e += "title=\""+title+"\">x</a></div>";
		e += "<a href=\""+url+"\" class=\"item\">";
		e += "<img height='120' width='120' src='"+picture+"'/>";
		e += "<p data-field=\"song-name\">"+name+"</p>";
		e += "</a></li>";

		var inserted = false;
		
		var playlist = '#playlist_'+playlist_id;
		if (playlist_id == -1)
			playlist = '#new_playlist'
		else if ($(playlist).length == 0 && $('#edit_playlist_'+playlist_id).length > 0)
			playlist = '#edit_playlist_'+playlist_id;
		
		var items = $(playlist+' [data-field="songs"]').children('li')

		for (j=0; j < items.length; j++)
		{
			if (name < $(items[j]).find('p:first').text())
			{
				console.log("insert at " + j);
				$(items[j]).before(e);
				inserted = true;
				break;
			}
		}

		if (!inserted)
		{
			$(playlist+' [data-field="songs"]').append(e);
		}
		
		$('[data-object="song-'+id+'"]').show('slide', { direction: 'left' });
		if ($(playlist+' [data-field="songs"] li').length == 1)
			$(playlist+' [data-field="empty"]').hide('slide', { direction: 'left' });
	}
	
	var container = "tracks[added][]";
	if (playlist_id == -1)
		container = "tracks[]";
	
	$('#tracks_added').append(
		"<div id='add_rem_track_"+id+"'>"+
		"<input type='hidden' name='"+container+"[path]' value='"+path+"'/>"+
		"<input type='hidden' name='"+container+"[title]' value='"+name+"'/>"+
		"<input type='hidden' name='"+container+"[length]' value='"+length+"'/>"+
		"<input type='hidden' name='"+container+"[art_url]' value='"+art_url+"'/>"+
		"<input type='hidden' name='"+container+"[url]' value='"+url+"'/>"+
		"<input type='hidden' name='"+container+"[artist]' value='"+artist+"'/>"+
		"<input type='hidden' name='"+container+"[album]' value='"+album+"'/>"+
		"<input type='hidden' name='"+container+"[genre]' value='"+genre+"'/>"+
		"</div>"
	);
}