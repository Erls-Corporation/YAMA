require 'base'
class Playlist < ActiveRecord::Base
	include Base

	has_and_belongs_to_many :songs do
		def page(limit=25, offset=0)
			all(:limit => limit, :offset => offset)
		end
	end
	
	has_many :listens, :through => :songs
	has_and_belongs_to_many :subscribers, :class_name => "User", :join_table => "playlist_subscribers"
	belongs_to :user
	
	validates :name, :presence => true
	
	def display
		name
	end
	
	def picture
		"#{base_url}/assets/media/disc.png"
		#songs.count == 0 ? "/assets/media/disc.png" : songs.first.picture
	end
	
	def paginate_songs(limit, offset)
		@paginated_songs = Array.new
		songs.limit(limit).offset(offset).each do |song|
			@paginated_songs << song
		end
	end
	
	def paginated_songs
		return @paginated_songs
	end
	
	def self.get(current_user, value)
		value = current_user.playlists.find(value) if value.is_a?(Integer)
		value = current_user.playlists.find_or_create_by_name(value) if value.is_a?(String)
		return value if value.is_a?(Playlist)
		return nil
	end
	
	def self.top(limit = 5, offset = 0, user = nil)
		self.select("playlists.id, playlists.name, playlists.is_public, playlists.user_id, count(listens.id) AS listens_count").
		joins("LEFT JOIN listens ON listens.playlist_id = playlists.id").
		where("listens.user_id IS NULL" + (user == nil ? "" : " or listens.user_id = #{user.id}")).
		where(user == nil ? "playlists.is_public = true" : "playlists.user_id = #{user.id}").
		group("playlists.id").
		order("listens_count DESC, playlists.updated_at DESC").
		limit(limit).
		offset(offset)
	end
	
	def self.search(user, search, limit = 5, offset = 0)
		if search
			w = user == nil ? "" : "or user_id = #{user.id}"
			self.where("name LIKE ? and (is_public = 1 #{w})", "%#{search}%").
			limit(limit).
			offset(offset)
		else
			scoped
		end
	end
end
