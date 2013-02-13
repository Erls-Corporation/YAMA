module Links::Twitter
	extend ActiveSupport::Concern
	
	def share_song_on_twitter(s, msg)
		# shorten message to allow for an url of size 29
		max = 110
		msg = msg[0..max-3] + "..." if msg.length > max
		msg += " #{s.song.link}"
		
		post("http://api.twitter.com/1/statuses/update.json", :params =>
		{
			:status => msg,
			:wrap_links => "true"
		})
	end
	
	def share_playlist_on_twitter(p, msg)
		# shorten message to allow for an url of size 29
		max = 110
		msg = msg[0..max-3] + "..." if msg.length > max
		msg += " #{p.url}"
		
		post("http://api.twitter.com/1/statuses/update.json", :params =>
		{
			:status => msg,
			:wrap_links => "true"
		})
	end
	
	def show_donation_on_twitter(d)
		post("http://api.twitter.com/1/statuses/update.json", :params =>
		{
			:status => "I just donated $#{d.amount} to #{d.artist.name}",
			:wrap_links => "true"
		})
	end
end