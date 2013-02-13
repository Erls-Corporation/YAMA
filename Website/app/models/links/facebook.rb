module Links::Facebook
	extend ActiveSupport::Concern
	
	def share_song_on_facebook(s, msg)
		logger.info "share song on facebook"
		
		l = s.song.url
		n = s.song.title
		c = "by #{s.song.artist.name}"
		o = s.song.source
		p = s.song.picture
	
		post('/me/feed', :params =>
		{
			:message => msg,
			:link => l,
			:name => n,
			:caption => c,
			:source => o,
			:picture => p
		})
	end
	
	def share_playlist_on_facebook(s, msg)
		logger.info "share playlist on facebook"
		
		p = s.playlist
		return unless p
		l = p.url
		n = p.name
		c = "A playlist on Stoffi"
		i = p.picture
		
		post('/me/feed', :params =>
		{
			:message => msg,
			:link => l,
			:name => n,
			:caption => c,
			:picture => i
		})
	end
	
	def start_listen_on_facebook(l)
		params = { :song => l.song.url, :end_time => l.ended_at, :start_time => l.created_at }
		if l.playlist
			params[:playlist] = l.playlist.url
		#elsif l.album
		#	params[:album] = l.album.url
		end
		post('/me/music.listens', :params => params)
	end
	
	def update_listen_on_facebook(l)
		id = find_listen_by_url(l.song.url)
		if id == nil
			start_listen_on_facebook(l)
		else
			if l.ended_at - l.created_at < 15.seconds
				delete('/' + id)
			else
				post("/#{id}", :params => { :end_time => l.ended_at })
			end
		end
	end
	
	def delete_listen_on_facebook(l)
		id = find_listen_by_url(l.url)
		delete('/' + id) unless id == nil
	end
	
	def show_donation_on_facebook(d)
		post('/me/stoffiplayer:support', :params =>
		{
			:artist => d.artist.url,
			:amount => '%.5f' % d.amount,
			:charity => '%.5f' % d.charity,
			:currency => d.currency
		})
	end
	
	def create_playlist_on_facebook(p)
		resp = post('/me/music.playlists', :params => { :playlist => p.url })
	end
	
	def update_playlist_on_facebook(p)
		id = find_playlist_by_url(p.url)
		if id == nil
			create_playlist_on_facebook(p)
		else
			get("/?id=#{id}&scrape=true")
		end
	end
	
	def delete_playlist_on_facebook(p)
		id = find_playlist_by_url(p.url)
		delete('/' + id) unless id == nil
	end
	
	def find_playlist_by_url(url)
		
		begin
			batch_size = 25
			offset = 0
			while true
				resp = get("/me/music.playlists?limit=#{batch_size}&offset=#{offset}")
				entries = resp['data']
				
				if entries.size == 0
					logger.warn "could not find playlist on facebook"
					return nil
				end
				
				entries.each do |entry|
					if entry['application']['id'] == creds[:id] and entry['data']['playlist']['url'].starts_with? url
						id = entry['id']
						return id
					end
				end
				
				offset += batch_size
			end
			
		rescue Exception => e
			logger.warn "could not find playlist '#{name}' on facebook"
			logger.warn e.inspect
		end
		
		return nil
	end
	
	def find_listen_by_url(url)
		
		begin
			batch_size = 25
			offset = 0
			while true
				resp = get("/me/music.listens?limit=#{batch_size}&offset=#{offset}")
				entries = resp['data']
				
				if entries.size == 0
					logger.warn "could not find listen on facebook"
					return nil
				end
				
				entries.each do |entry|
					if entry['application']['id'] == creds[:id] and entry['data']['song']['url'].starts_with? url
						id = entry['id']
						return id
					end
				end
				
				offset += batch_size
			end
			
		rescue Exception => e
			logger.warn "could not find playlist '#{name}' on facebook"
			logger.warn e.inspect
		end
		
		return nil
	end
	
	def fetch_encrypted_facebook_uid
		resp = get("/dmp?fields=third_party_id")
		update_attribute(:encrypted_uid, resp['third_party_id'])
	end
end