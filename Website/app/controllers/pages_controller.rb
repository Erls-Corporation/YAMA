class PagesController < ApplicationController
	oauthenticate :only => [ :remote ]
	
	before_filter :set_title_and_description, :except => :search
	respond_to :html, :mobile, :embedded, :json, :xml
	
	def foo
		#l = Listen.find(4)
		
		#l = Playlist.find(7)
		#l = Playlist.find(15)
		#l = Playlist.find(17)
		#l = Playlist.find(27)
		#l = Playlist.find(28)
		
		#current_user.links.first.create_playlist(l)
		#current_user.links.first.delete_playlist(l)
		
		#lnk = Link.find(36)
		#lnk = Link.find(10)
		
		#current_user.links.first.fetch_encrypted_uid
		
		#lnk.start_listen(l)
		#l.update_attribute(:ended_at, Time.now)
		#lnk.update_listen(l)
		#lnk.delete_listen(l)
		#resp = lnk.end_listen(l)
		
		#render :text => current_user.links.first.provider
		#render :xml => resp
		render :text => "ok"
	end

	def old
		render :layout => false
	end
	
	def index
	end

	def news
	end

	def get
	end

	def download
		params[:channel] = "stable" unless params[:channel]
		params[:arch] = "32" unless params[:arch]
		@type = params[:type] || "installer"
		
		unless ["alpha", "beta", "stable"].include? params[:channel]
			redirect_to "/get" and return
		end
		
		unless ["32", "64"].include? params[:arch]
			redirect_to "/get" and return
		end
		
		unless ["installer", "checksum"].include? @type
			redirect_to "/get" and return
		end
		
		filename = "InstallStoffi"
		filename = "InstallStoffiAlpha" if params[:channel] == "alpha"
		filename = "InstallStoffiBeta" if params[:channel] == "beta"
		
		filename += "AndDotNet" if params[:fat] && params[:fat] == "1"
		
		@fname = filename
		
		filename += case @type
			when "checksum" then ".sum"
			else ".exe"
		end
		
		@file = "/downloads/" + params[:channel] + "/" + params[:arch] + "bit/" + filename
		@autodownload = @type == "installer"
	end

	def tour
		redirect_to :action => :index and return if params[:format] == "mobile"
	end

	def about
	end

	def contact
	end

	def legal
	end

	def money
	end

	def history
		redirect_to "http://dev.stoffiplayer.com/wiki/History"
	end

	def remote
	
		if current_user and current_user.configurations.count > 0 and current_user.configurations.first.devices.count > 0
			@configuration = current_user.configurations.first
			
			@devices = @configuration.devices.order(:name)
		end
		
		@title = t("remote.title")
		@description = t("remote.description")
		
		render "configurations/show", :layout => (params[:format] != "mobile" ? true : 'empty')
	end

	def language
		respond_to do |format|
			format.html { redirect_to root_url }
			format.mobile { render }
		end
	end

	def donate
		logger.info "redirecting donate shortcut"
		respond_with do |format|
			format.html { redirect_to donations_url, :flash => flash }
			format.mobile { redirect_to new_donation_url, :flash => flash }
		end
	end
  
	def mail				
		if !params[:name] or params[:name].length < 2
				flash[:error] = t("contact.errors.name")
				render :action => 'contact'
				
		elsif !params[:email] or params[:email].match(/^([a-z0-9_.\-]+)@([a-z0-9\-.]+)\.([a-z.]+)$/i).nil?
				flash[:error] = t("contact.errors.email")
				render :action => 'contact'
				
		elsif !params[:subject] or params[:subject].length < 4
				flash[:error] = t("contact.errors.subject") 
				render :action => 'contact'
				
		elsif !params[:message] or params[:message].length < 20
				flash[:error] = t("contact.errors.message")
				render :action => 'contact'

		elsif !verify_recaptcha
			flash[:error] = t("contact.errors.captcha")
			render :action => 'contact'
			
		else
			Mailer.contact(:domain => "beta.stoffiplayer.com",
						   :subject => params[:subject],
						   :from => params[:email],
						   :name => params[:name],
						   :message => params[:message]).deliver
			redirect_to :action => 'contact', :sent => 'success'
		end
	end

	def facebook
		render :layout => "facebook"
	end

	def channel
		render :layout => false
	end
	
	def search
		redirect_to :action => :index and return if params[:format] == "mobile"
	
		# get query parameter
		params[:q] = params[:term] if params[:q] == nil && params[:term]
		params[:q] = "" unless params[:q]
		params[:q] = CGI::escapeHTML(params[:q])
		
		# get category parameter
		params[:c] = params[:categories] if params[:c] == nil && params[:categories]
		params[:c] = params[:category] if params[:c] == nil && params[:category]
		params[:c] = "artists|songs|devices|playlists" unless params[:c]
		c = params[:c].split('|')
		
		# get limit parameter
		params[:l] = params[:limit] if params[:l] == nil && params[:limit]
		params[:l] = '5' unless params[:l]
		l = params[:l].to_i
		l = 50 if l > 50
		
		@result = Array.new
		
		if c.include? 'artists'
			Artist.search(params[:q]).limit(l).each do |i|
				@result.push(
				{
					:url => i.url,
					:display => i.display,
					:category => t("search.categories.artists"),
					:desc => t("plays", :count => i.listens_count),
					:id => i.id,
					:field => "artist_#{i.id}",
					:kind => i.kind
				})
			end
		end
		
		if c.include? 'songs'
		
			# get source parameter
			params[:s] = params[:sources] if params[:s] == nil && params[:sources]
			params[:s] = params[:source] if params[:s] == nil && params[:source]
			params[:s] = "files|youtube|soundcloud" unless params[:s]
			
			if params[:s]
				s = params[:s].split('|')
				q = params[:q].gsub(/ /, "+")
		
				if s.include? 'files'
					cat = t("search.categories.songs")
					cat = t("search.categories.files") if c.length == 1
					Song.search_files(params[:q]).limit(l).each do |i|
						@result.push(
						{
							:url => i.url,
							:path => i.path,
							:display => i.display,
							:title => i.title,
							:category => cat,
							:desc => "",
							:id => i.id,
							:field => "song_#{i.id}",
							:icon => "/assets/gfx/icons/file.ico",
							:kind => i.kind
						})
					end
				end
				
				if s.include? 'soundcloud'
					cat = t("search.categories.songs")
					cat = t("search.categories.soundcloud") if c.length == 1
					id = "2ad7603ebaa9cd252eabd8dd293e9c40"
					http = Net::HTTP.new("api.soundcloud.com", 443)
					http.use_ssl = true
					res, data = http.get("/tracks.json?client_id=#{id}&limit=#{l}&q=#{q}", nil)
					tracks = JSON.parse(data)
					tracks.each do |track|
						artist, title = Song.parse_title(track['title'])
						artist = track['user']['username'] unless artist
						@result.push(
						{
							:url => track['permalink_url'],
							:title => track['title'],
							:artist => artist,
							:length => track['duration'],
							:genre => track['genre'],
							:path => "stoffi:track:soundcloud:#{track['id']}",
							:display => title,
							:category => cat,
							:picture => track['artwork_url'],
							:desc => artist,
							:field => "song_soundcloud_#{track['id']}",
							:icon => "/assets/gfx/icons/soundcloud.png",
							:kind => "song"
						})
					end
				end
				
				if s.include? 'youtube'
					cat = t("search.categories.songs")
					cat = t("search.categories.youtube") if c.length == 1
					http = Net::HTTP.new("gdata.youtube.com", 443)
					http.use_ssl = true
					res, data = http.get("/feeds/api/videos?max-results=#{l}&category=Music&alt=json&v=2&q=#{q}", nil)
					feed = JSON.parse(data)
					feed['feed']['entry'].each do |entry|
					
						artist, title = Song.parse_title(entry['title']['$t'])
						artist = feed['entry']['author']['name']['$t'] unless artist
						id = entry['media$group']['yt$videoid']['$t']
					
						@result.push(
						{
							:url => "https://www.youtube.com/watch?v=#{id}",
							:path => "stoffi:track:youtube:#{id}",
							:display => entry['title']['$t'],
							:title => title,
							:artist => artist,
							:length => entry['media$group']['yt$duration']['seconds'],
							:category => cat,
							:picture => entry['media$group']['media$thumbnail'][0]['url'],
							:desc => artist,
							:field => "song_youtube_#{id}",
							:icon => "/assets/gfx/icons/youtube.gif",
							:kind => "song"
						})
					end
				end
			
			else
				Song.search(params[:q]).limit(l).each do |i|
					@result.push(
					{
						:url => i.url,
						:display => i.display,
						:category => t("search.categories.songs"),
						:picture => i.picture,
						:path => i.path,
						:desc => "",
						:id => i.id,
						:field => "song_#{i.id}",
						:kind => i.kind
					})
				end
			end
		end
		
		if c.include? 'devices' and signed_in?
			current_user.devices.search(params[:q]).limit(l).each do |i|
				@result.push(
				{
					:url => i.url,
					:display => i.display,
					:category => t("search.categories.devices"),
					:desc => "",
					:id => i.id,
					:field => "device_#{i.id}",
					:kind => i.kind
				})
			end
		end
		
		if c.include? 'playlists'
			Playlist.search(current_user, params[:q]).limit(l).each do |i|
				@result.push(
				{
					:url => i.url,
					:display => i.display,
					:category => t("search.categories.playlists"),
					:desc => "",
					:id => i.id,
					:field => "device_#{i.id}",
					:kind => i.kind
				})
			end
		end
		
		if c.include? 'users'
			#User.search(params[:q]).limit(5).each do |i|
			#	@result.push(
			#	{
			#		:url => i.url,
			#		:display => i.display,
			#		:category => t("search.categories.users"),
			#		:desc => "",
			#		:kind => i.kind
			#	})
			#end
		end
		
		@title = h(params[:q])
		@description = t("index.description")
		
		respond_with(@result)
	end
	
	private
	
	def set_title_and_description
		@title = t("#{action_name}.title")
		@description = t("#{action_name}.description")
	end

end
