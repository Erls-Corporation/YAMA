class SyncController < ApplicationController
	def self.create_properties(resource, params = nil)
		type = resource.type.to_s.downcase
		
		chkdiff = (params != nil and params[type] != nil)
		
		properties = Hash.new
		fields = ['id']
		case type
		when 'device'
			fields = fields.concat(['name','version','status'])
			
		when 'listen'
			fields = fields.concat(['user_id','song_id'])
			properties['title'] = h(resource.song.title)
			properties['picture'] = h(resource.song.picture)
			
		when 'playlist'
			fields = fields.concat(['name','user_id','is_public'])
			
			if chkdiff
				properties['songs'] = { 'added' => [], 'removed' => [] }
			else
				properties['songs'] = resource.songs
			end
			
		when 'donation'
			fields = fields.concat(['user_id','artist_id','artist_percentage','stoffi_percentage','charity_percentage','status','amount'])
			properties['user_name'] = h(resource.user.name)
			properties['user_picture'] = h(resource.user.picture)
			properties['artist_name'] = h(resource.artist.name)
			properties['artist_picture'] = h(resource.artist.picture)
		
		when 'share'
			fields = fields.concat(['user_id'])
			
			if resource.song
				properties['song_id'] = resource.song.id
				properties['title'] = h(resource.song.title)
				properties['picture'] = h(resource.song.picture)
			
			elsif resource.playlist
				properties['playlist_id'] = resource.playlist.id
				properties['title'] = h(resource.playlist.name)
				properties['picture'] = h(resource.playlist.picture)
			end
		
		when 'configuration'
			fields = fields.concat(['name', 'media_state', 'current_track', 'currently_selected_navigation', 
			               'currently_active_navigation', 'shuffle', 'repeat', 'volume', 'seek',
			               'search_policy', 'upgrade_policy', 'add_policy', 'play_policy',
						   'current_shortcut_profile', 'current_equalizer_profile'])
			properties['now_playing'] = resource.current_track.full_name if resource.current_track
			
		when 'link'
			fields = fields.concat(['provider', 'do_share', 'do_listen', 'do_donate', 'show_button',
			               'do_create_playlist', 'display', 'name', 'url', 'connectURL', 'can_share',
			               'can_listen','can_donate','can_button','can_create_playlist'])
		end
		
		fields.uniq.each do |f|
			old = resource.send(f)
			new = old
			new = params[type][f] if chkdiff
			properties[f] = new if !chkdiff or (new and new != old)
		end
		
		return properties
	end
	
	def self.send(verb, resource, arg = nil)
		
		type = resource.type.to_s.downcase
		id = resource.id
		chans = Array.new
		chans << "hash_#{resource.user.unique_hash}" if resource.user
		
		case type
		when 'listen'
			chans << "user_#{resource.user.id}"
			
		when 'playlist'
			if resource.is_public
				chans << "user_#{resource.user.id}"
				resource.subscribers.each do |subscriber|
					chans << "hash_#{subscriber.unique_hash}"
				end
			end
			
		when 'donation'
			chans << "user_#{resource.user.id}"
			chans << "artist_#{resource.artist.id}"
		
		when 'share'
			chans << "user_#{resource.user.id}"
		end
		
		properties = arg if verb == 'update' and arg
		properties = create_properties(resource) unless properties
		
		# sanitize
		properties.each do |k,v|
			properties[k] = h(properties[k])
		end
		
		properties = properties.to_json
	
		cmd = case verb
		when 'update' then "updateObject('#{type}', #{id}, '#{properties}');"
		when 'create' then "createObject('#{type}', '#{properties}');"
		when 'delete' then "deleteObject('#{type}', #{id});"
		when 'execute' then "execute('#{arg}', '#{id}');"
		else ""
		end
	
		Juggernaut.publish(chans, cmd)
	end
	
	private
	
	def self.h(str)
		return unless str
		if str.is_a?(String)
			str = CGI.escapeHTML(str)
			str.gsub!(/[']/, "&#39;")
			str.gsub!(/["]/, "&#34;")
			str.gsub!(/[\\]/, "&#92;")
			return str
		end
		return str.map { |s| h(s) } if str.is_a?(Array)
		return str.each { |a,b| str[a] = h(b) } if str.is_a?(Hash)
		str
	end
end
