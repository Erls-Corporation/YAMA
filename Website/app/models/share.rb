require 'base'
class Share < ActiveRecord::Base
	include Base
	
	# associations
	belongs_to :song
	belongs_to :playlist
	belongs_to :user
	belongs_to :device
	
	def display
		object == "song" ? song.display : playlist.display
	end
	
	def serialize_options
		{
			:include => [ object == "song" ? :song : :playlist ],
			:methods => [ :kind, :display, :url ]
		}
	end
end
