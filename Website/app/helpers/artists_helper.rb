module ArtistsHelper
	def artist_item(artist, options = {})
		@artist = artist
		@options = options
		render :partial => "artists/item"
	end
end
