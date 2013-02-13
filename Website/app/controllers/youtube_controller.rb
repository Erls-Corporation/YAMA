class YoutubeController < ApplicationController
	def player
		render :layout => @browser != "stoffi" && false
	end

	def search
	end
end
