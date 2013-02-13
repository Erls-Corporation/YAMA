require 'base'
require 'app/models/links/facebook'
require 'app/models/links/twitter'
require 'app/models/links/lastfm'

class Link < ActiveRecord::Base
	include Base

	belongs_to :user
	
	def update_credentials(auth)
		exp = auth['credentials']['expires_at']
		exp = DateTime.strptime("#{exp}",'%s') if exp
		self.update_attributes(
			:access_token => auth['credentials']['token'],
			:access_token_secret => auth['credentials']['secret'],
			:refresh_token => auth['credentials']['refresh_token'],
			:token_expires_at => exp
		)
	end
	
	def self.available
		[
			{ :name => "Twitter" },
			{ :name => "Facebook" },
			{ :name => "Google", :link_name => "google_oauth2" },
			{ :name => "Vimeo" },
			{ :name => "SoundCloud" },
			{ :name => "LastFM" },
			{ :name => "MySpace" },
			{ :name => "Yahoo" },
			{ :name => "Weibo" },
			{ :name => "vKontakte" },
			{ :name => "LinkedIn" },
			{ :name => "Windows Live", :link_name => "windowslive" }
		]
	end
	
	def picture?
		["twitter", "facebook", "google_oauth2", "lastfm", "vimeo"].include?(provider)
	end
	
	def can_button?
		["twitter", "google_oauth2"].include?(provider)
	end
	
	def button?
		can_button? and show_button
	end
	
	def can_share?
		["facebook", "twitter", "lastfm"].include?(provider)
	end

	def share?
		do_share && can_share?
	end
	
	def can_listen?
		["facebook", "lastfm"].include?(provider)
	end
	
	def listen?
		do_listen && can_listen?
	end
	
	def can_donate?
		["facebook", "twitter"].include?(provider)
	end
	
	def donate?
		do_donate && can_donate?
	end
	
	def can_create_playlist?
		["facebook", "lastfm"].include?(provider)
	end
	
	def create_playlist?
		do_create_playlist && can_create_playlist?
	end
	
	alias_method "can_button", "can_button?"
	alias_method "can_share", "can_share?"
	alias_method "can_listen", "can_listen?"
	alias_method "can_donate", "can_donate?"
	alias_method "can_create_playlist", "can_create_playlist?"
	
	def picture
		begin
			case provider
			when "facebook"
				response = get("/me?fields=picture")
				return response['picture']['data']['url']
				
			when "twitter"
				response = get("/1/users/show.json?user_id=#{uid}")
				return response['profile_image_url']
				
			when "google_oauth2"
				response = get("/oauth2/v1/userinfo")
				return response['picture'] if response['picture']
				
			when "myspace"
				response = get("/v1.0/people/@me")
				return response['thumbnailUrl'] if response['thumbnailUrl']
				
			when "vimeo"
				response = get("/api/v2/#{uid}/info.json")
				return response['portrait_medium']
				
			when "yahoo"
				response = get("/v1/user/#{uid}/profile/tinyusercard")
				return response['profile']['image']['imageUrl']
				
			when "vkontakte"
				response = get("api.php?method=getProfiles?uids=#{uid}&fields=photo_medium")
				logger.debug response.inspect
				return response['Response'][0]['Photo']
				
			when "lastfm"
				response = get("/2.0/?method=user.getinfo&format=json&user=#{uid}&api_key=#{creds[:id]}")
				return response['user']['image'][1]['#text']
				
			when "linkedin"
				response = get("/v1/people/~")
				logger.debug response.inspect
				#return ???
				
			when "windowslive"
				response = get("/v5.0/me/picture?access_token=#{access_token}")
				logger.debug response.inspect
				return response['person']['picture_url']
				
			end
		rescue Exception => e
			logger.debug "error fetching pictures from service: #{provider}"
			logger.debug e.to_yaml
		end
		return nil
	end
	
	def names
		begin
			case provider
			when "facebook"
				response = get("/me?fields=name,username")
				r = { :fullname => response['name'] }
				r[:username] = response['username'] if response['username']
				return r
				
			when "twitter"
				response = get("/1/users/show.json?user_id=#{uid}")
				return {
					:username => response['screen_name'],
					:fullname => response['name']
				}
				
			when "google_oauth2"
				response = get("/oauth2/v1/userinfo")
				return {
					:fullname => response['name'],
					:username => response['email'].split('@',2)[0]
				}
				
			when "vimeo"
				response = get("/api/v2/#{uid}/info.json")
				return { :fullname => response['display_name'] }
				
			when "lastfm"
				response = get("/2.0/?method=user.getinfo&format=json&user=#{uid}&api_key=#{creds[:id]}")
				return {
					:username => response['user']['name'],
					:fullname => response['user']['realname']
				}
				
			end
		rescue Exception => e
			logger.debug "error fetching names from service: #{provider}"
			logger.debug e.to_yaml
		end
		return {}
	end
	
	def name
		n = names
		return n[:fullname] if n[:fullname]
		return n[:username] if n[:username]
		return I18n.translate("user.name.unknown")
	end
	
	def share(s)
		return unless share?
		
		if s.object == "song"
		
			# fix message to either
			#  - the user's message
			#  - title by artist
			#  - title
			msg = s.message
			if msg == "" || msg == nil
				msg = s.song.title
				a = s.song.artist.name
				msg += " by #{a}" unless a == "" || a == nil
			end
			
		elsif s.object == "playlist"
		
			# fix message to either
			#  - the user's message
			#  - playlist by user
			msg = s.message
			if msg.to_s == ""
				msg = "#{s.playlist.name} by #{s.user.name}"
			end
			logger.info "share message: #{msg}"
		end
		
		begin
			case [provider, s.object]
			when ["facebook", "song"]
				share_song_on_facebook(s, msg)
			
			when ["facebook", "playlist"]
				share_playlist_on_facebook(s, msg)
				
			when ["twitter", "song"]
				share_song_on_twitter(s, msg)
			
			when ["twitter", "playlist"]
				share_playlist_on_twitter(s, msg)		
			end
		rescue Exception => e
			logger.debug "error sharing #{s.object} on service: #{provider}"
			logger.debug e.message
		end
	end
	
	def start_listen(l)
		return unless listen?
		
		begin
			case provider
			when "facebook"
				start_listen_on_facebook(l)
			when "lastfm"
				start_listen_on_lastfm(l)
			end
		rescue Exception => e
			logger.debug "error starting listen on service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def update_listen(l)
		return unless listen?
		
		begin
			case provider
			when "facebook"
				update_listen_on_facebook(l)
			end
		rescue Exception => e
			logger.debug "error updating listen on service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def end_listen(l)
		return unless listen?
		
		begin
			case provider
			when "lastfm"
				end_listen_on_lastfm(l)
			end
		rescue Exception => e
			logger.debug "error ending listen on service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def delete_listen(l)
		return unless listen?
		
		begin
			case provider
			when "facebook"
				delete_listen_on_facebook(l)
			end
		rescue Exception => e
			logger.debug "error deleting listen from service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def donate(d)
		return unless donate?
		
		begin
			case provider
			when "facebook"
				show_donation_on_facebook(d)
				
			when "twitter"
				show_donation_on_twitter(d)
			end
		rescue Exception => e
			logger.debug "error sharing donation on service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def create_playlist(p)
		return unless create_playlist?
		
		begin
			case provider
			when "facebook"
				create_playlist_on_facebook(p)
			end
		rescue Exception => e
			logger.debug "error creating playlist on service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def update_playlist(p)
		return unless create_playlist?
		
		begin
			case provider
			when "facebook"
				update_playlist_on_facebook(p)
			end
		rescue Exception => e
			logger.debug "error updating playlist on service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def delete_playlist(p)
		return unless create_playlist?
		
		begin
			case provider
			when "facebook"
				delete_playlist_on_facebook(p)
			end
		rescue Exception => e
			logger.debug "error deleting playlist on service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def fetch_encrypted_uid
		begin
			case provider
			when "facebook"
				fetch_encrypted_facebook_uid
			end
		rescue Exception => e
			logger.debug "error fetching encrypted uid from service: #{provider}"
			logger.debug e.to_yaml
		end
	end
	
	def display
		Link.pretty_name(provider)
	end
	
	def connectURL
		"http://beta.stoffiplayer.com/auth/#{provider}"
	end
	
	def self.pretty_name(provider)
		case provider
		when "google_oauth2" then "Google"
		when "linked_in" then "LinkedIn"
		when "soundcloud" then "SoundCloud"
		when "lastfm" then "Last.fm"
		when "myspace" then "MySpace"
		when "linkedin" then "LinkedIn"
		when "vkontakte" then "vKontakte"
		when "windowslive" then "Live"
		else provider.titleize
		end
	end
	
	def serialize_options
		{
			:except =>
			[
				:access_token_secret, :access_token, :uid, :created_at,
				:refresh_token, :token_expires_at, :user_id, :updated_at
			],
			:methods =>
			[
				:kind, :display, :url, :connectURL,
				:can_button, :can_share, :can_listen, :can_donate, :can_create_playlist,
				:name
			]
		}
	end
	
	private

	include Links::Facebook
	include Links::Twitter
	include Links::Lastfm
	
	def get(path, params = {})
		request(path, :get, params)
	end
	
	def post(path, params = {})
		request(path, :post, params)
	end
	
	def delete(path)
		request(path, :delete)
	end
	
	def request(path, method = :get, params = {})
	
		# google uses a refresh_token
		# we need to request a new access_token using the
		# refresh_token if it has expired
		if provider == "google_oauth2" and refresh_token and token_expires_at < DateTime.now
			http = Net::HTTP.new("accounts.google.com", 443)
			http.use_ssl = true
			res, data = http.post("/o/oauth2/token", 
			{
				:refresh_token => refresh_token,
				:client_id => creds[:id],
				:client_secret => creds[:key],
				:grant_type => 'refresh_token'
			}.map { |k,v| "#{k}=#{v}" }.join('&'))
			response = JSON.parse(data)
			exp = response['expires_in'].seconds.from_now
			update_attributes({ :token_expires_at => exp, :access_token => response['access_token']})
		end
	
		client = OAuth2::Client.new(creds[:id], creds[:key], :site => creds[:url], :ssl => {:ca_path => "/etc/ssl/certs"})
		token = OAuth2::AccessToken.new(client, access_token, :header_format => "OAuth %s")
	
		case method
		when :get
			return token.get(path).parsed
			
		when :post
			return token.post(path, params).parsed
			
		when :delete
			return token.delete(path).parsed
		end
	end
	
	def creds
		V6::Application::OA_CRED[provider.to_sym]
	end
end
