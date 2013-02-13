require 'base'
class Album < ActiveRecord::Base
	include Base
	
	# associations
	has_and_belongs_to_many :artist
	has_and_belongs_to_many :songs
	
	def self.get(value)
		value = find(value) if value.is_a?(Integer)
		value = find_or_create_by_title(value) if value.is_a?(String)
		return value if value.is_a?(Playlist)
		return nil
	end
	
	def display
		title
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
end
