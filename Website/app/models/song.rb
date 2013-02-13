require 'base'
class Song < ActiveRecord::Base
	extend StaticBase
	include Base

	# associations
	has_and_belongs_to_many :albums
	has_and_belongs_to_many :artists
	has_and_belongs_to_many :users
	has_and_belongs_to_many :playlists
	has_many :listens
	
	scope :top5,
		select("songs.id, songs.title, songs.path, count(listens.id) AS listens_count").
		joins(:listens).
		group("songs.title").
		order("listens_count DESC").
		limit(5)
	
	def source
		if youtube?
			return "http://www.youtube.com/v/" + youtube_id + "?fs=1"
		else
			return ""
		end
	end
	
	def picture(size = :medium)
		return "https://img.youtube.com/vi/" + youtube_id + "/default.jpg" if youtube?
		return art_url if (art_url.to_s != "" and art_url.to_s.downcase != "null")
		return "http://beta.stoffiplayer.com/assets/media/disc.png"
	end
	
	def youtube_id
		if youtube?
			return path["stoffi:track:youtube:".length .. -1]
		else
			return ""
		end
	end
	
	def youtube?
		return path && path.starts_with?("stoffi:track:youtube:")
	end
	
	def soundcloud_id
		if soundcloud?
			return path["stoffi:track:soundcloud:".length .. -1]
		else
			return ""
		end
	end
	
	def soundcloud?
		return path && path.starts_with?("stoffi:track:soundcloud:")
	end
	
	def pretty_name
		s = title
		s += " by #{artist.name}" if artist
		return s
	end
	
	def full_name
		s = title
		s = "#{artist.name} - #{s}" if artist
		return s
	end
	
	def play
		return "stoffi:track:youtube:#{youtube_id}" if youtube?
		return "stoffi:track:soundcloud:#{soundcloud_id}" if soundcloud?
		return url
	end
	
	def artist
		#artists.map { |a| a.name } * " & "
		artists == nil ? nil : artists.first
	end
	
	def display
		title
	end
	
	def description
		s = "#{title}, a song "
		s+= "by #{artist.name} " if artist
		s+= "on Stoffi"
	end
	
	def serialize_options
		{
			:methods => [ :kind, :display, :url, :picture ]
		}
	end
	
	def self.search(search, limit = 5)
		if search
			self.where("title LIKE ?", "%#{search}%").
			limit(limit)
		else
			scoped
		end
	end
	
	def self.search_files(search, limit = 5)
		if search
			self.where("title LIKE ? AND path NOT LIKE 'stoffi:track:%'", "%#{search}%").
			limit(limit)
		else
			scoped
		end
	end
	
	def self.foo
		#return self.h("foobar")
		return bar()
	end
	
	def self.get(current_user, value)
		value = self.find(value) if value.is_a?(Integer)
		if value.is_a?(Hash)
			p = value
			
			if p.key? :path
				if p[:path].starts_with? "youtube://"
					id = p[:path]["youtube://".length .. -1]
					p[:path] = "stoffi:track:youtube:#{id}"
				
				elsif p[:path].starts_with? "soundcloud://"
					id = p[:path]["soundcloud://".length .. -1]
					p[:path] = "stoffi:track:soundcloud:#{id}"
					
				end
			end
			
			value = self.get_by_path(p[:path])
			value = self.find_by_path_and_length(p[:path], p[:length].to_f) unless value.is_a?(Song)
			
			unless value.is_a?(Song)
			
				# fix artist, album objects
				if p[:artist].to_s != ""
					artist = Artist.get(p[:artist])
					p.delete(:artist)
				end
				if p[:album].to_s != ""
					album = Album.get(p[:album])
					p.delete(:album)
				end
				
				# fix params
				p[:length] = p[:length].to_f if p[:length]
				p[:score] = p[:score].to_i if p[:score]
				
				value = Song.new
				
				value.art_url = p[:art_url] if p[:art_url]
				value.foreign_url = p[:foreign_url] if p[:foreign_url]
				value.length = p[:length] if p[:length]
				value.score = p[:score] if p[:score]
				value.title = h(p[:title]) if p[:title]
				value.path = p[:path] if p[:path]
				
				if value.save
					value.artists << artist if artist and artist.songs.find_all_by_id(value.id).count == 0
					value.albums << album if album and albums.songs.find_all_by_id(value.id).count == 0
					artist.albums << album if artist and album and artist.albums.find_all_by_id(album.id).count == 0
				end
			end
		end
		current_user.songs << value if value.is_a?(Song) and current_user.songs.find_by_id(value.id) == nil
		return value if value.is_a?(Song)
		return nil
	end
	
	def self.get_by_path(path)
		begin
			song = nil
			if path.start_with? "stoffi:track:youtube:"
				song = find_by_path(path)
				unless song
					song = create(:path => path)
					
					http = Net::HTTP.new("gdata.youtube.com", 443)
					http.use_ssl = true
					res, data = http.get("/feeds/api/videos/#{song.youtube_id}?v=2&alt=json", nil)
					feed = JSON.parse(data)
					
					artist, title = parse_title(feed['entry']['title']['$t'])
					artist = feed['entry']['author']['name']['$t'] unless artist
					artist = Artist.get(artist) if artist
					
					id = feed['entry']['media$group']['yt$videoid']['$t']
					
					song.foreign_url = "https://www.youtube.com/watch?v=#{id}"
					song.title = h(title)
					song.length = feed['entry']['media$group']['yt$duration']['seconds']
					song.art_url = feed['entry']['media$group']['media$thumbnail'][0]['url']
					
					artist = Artist.get(artist)
					
					song.artists << artist if artist
				end
			
			elsif path.start_with? "stoffi:track:soundcloud:"
				song = find_by_path(path)
				unless song
					song = create(:path => path)
					
					client_id = "2ad7603ebaa9cd252eabd8dd293e9c40"
					http = Net::HTTP.new("api.soundcloud.com", 443)
					http.use_ssl = true
					res, data = http.get("/tracks/#{song.soundcloud_id}.json?client_id=#{client_id}", nil)
					track = JSON.parse(data)
					
					artist, title = parse_title(track['title'])
					artist = track['user']['username'] unless artist
					artist = Artist.get(artist) if artist
					
					song.foreign_url = track['permalink_url']
					song.title = h(title)
					song.length = track['duration'] / 1000.0
					song.genre = h(track['genre'])
					song.art_url = track['artwork_url']
					
					song.artists << artist if artist
				end
				
			end
			song.save if song
			return song
		rescue
			return nil
		end
	end
	
	def self.top(limit = 5, user = nil)
		self.select("songs.id, songs.title, songs.art_url, songs.path, count(listens.id) AS listens_count").
		joins("LEFT JOIN listens ON listens.song_id = songs.id").
		where(user == nil ? "" : "listens.user_id = #{user.id}").
		group("songs.id").
		order("listens_count DESC").
		limit(limit)
	end
	
	def self.parse_title(title)
		["-",":","~"].each do |sep|
			[" #{sep} ", " #{sep}", "#{sep} "].each do |variant|
				if title.downcase.include? variant
					str = title.split(/#{variant}/i, 2)
					["by ", "ft ", "ft. ", "feat ", "feat. ", "with "].each do |prefix|
						if str[0].downcase.start_with? prefix
							return str[0][prefix.length .. -1], str[1]
						elsif str[1].downcase.start_with? prefix
							return str[1][prefix.length .. -1], str[0]
						end
					end
					return str[0], str[1]
				end
			end
		end
		
		["feat.","feat ","ft.","by ","with "].each do |sep|
			[" #{sep} ", " #{sep}", "#{sep} "].each do |variant|
				if title.downcase.include? variant
					str = title.match(/(.*)#{variant}(.*)/i)[1..2]
					return str[1], str[0]
				end
			end
		end
		
		return "", title
	end
end
