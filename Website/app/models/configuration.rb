require 'base'
class Configuration < ActiveRecord::Base
	include Base
	
	# associations
	belongs_to :user
	has_many :tracks
	has_many :playlists
	has_many :keyboard_shortcut_profiles
	has_many :equalizer_profiles
	has_many :devices
	belongs_to :current_track, :class_name => 'Song'
	belongs_to :current_shortcut_profile, :class_name => 'KeyboardShortcutProfile'
	belongs_to :current_equalizer_profile, :class_name => 'EqualizerProfile'
	
	def repeat_text
		case repeat
			when "NoRepeat" then I18n.t("media.repeat.disabled")
			when "RepeatAll" then I18n.t("media.repeat.all")
			else I18n.t("media.repeat.one")
		end
	end
	
	def shuffle_text
		case shuffle
			when "Random" then I18n.t("media.shuffle.random")
			when "MindReader" then I18n.t("media.shuffle.mind_reader")
			else I18n.t("media.shuffle.disabled")
		end
	end
	
	def media_button
		case media_state
			when "Playing" then "pause"
			else "play"
		end
	end
	
	def now_playing
		current_track ? current_track.full_name : I18n.t("media.nothing_playing")
	end
	
	def display
		name
	end
	
	def serialize_options
		{
			:include => [ :current_track ],
			:methods => [ :kind, :display, :url ]
		}
	end
end
