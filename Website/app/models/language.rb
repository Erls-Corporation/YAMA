class Language < ActiveRecord::Base
	has_many :translations

	def flag
		"/assets/flags/#{iso_tag}.png"
	end
end
