class Queue < ActiveRecord::Base
	has_many :queues_tracks
	has_many :tracks, :through => :queues_tracks
	belongs_to :configuration
end
