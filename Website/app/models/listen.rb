require 'base'
class Listen < ActiveRecord::Base
	include Base
	
	# associations
	belongs_to :user
	belongs_to :song
	belongs_to :playlist
	belongs_to :device
	#belongs_to :album
	
	def display
		song.display
	end
	
	def serialize_options
		{
			:except => [ :device_id ],
			:include => [ :song, :playlist ],
			:methods => [ :kind, :display, :url ]
		}
	end
end
