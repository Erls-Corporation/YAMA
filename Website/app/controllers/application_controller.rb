class ApplicationController < ActionController::Base
	# we need number_to_currency for our title formatting
	include ActionView::Helpers::NumberHelper
	
	#require 'juggernaut' # do we need this after rails 3 upgrade?
	require 'geoip'
	
	# prevent csrf
	protect_from_forgery
	
	before_filter :auth_with_params,
	              :check_device,
	              :ensure_proper_oauth,
				  :restrict_admin,
	              :set_tab,
	              :classify_device,
	              :prepare_for_mobile,
	              :prepare_for_embedded,
				  :reload_locales,
	              :set_locale,
				  :check_tracking,
				  :set_config,
				  :check_old_browsers
	
	# sets the configuration for the website
	def set_config
		@site_config = Admin::Config.first
	end
	
	# Reload the cached translations if we are running beta/alpha
	def reload_locales
		I18n.reload! if Rails.env.development?
	end
	
	# authenticate a user if the correct parameters are given
	# we need this to let applications login using their saved
	# tokens.
	#
	# We should require HTTPS for this one (in case we turn it
	# off on the whole site.
	def auth_with_params
		if params[:oauth_token] && params[:oauth_secret_token]
			t = params[:oauth_token]
			s = params[:oauth_secret_token]
			token = OauthToken.find_by_token_and_secret(t, s)
			if token
				sign_in(token.user)
			else
				redirect_to login_path
			end
		end
	end
	
	# check if the device ID header or param is present and
	# set @current_device accordingly.
	def check_device
		[
			params[:device_id],
			request.env['HTTP_DEVICE_ID']
		].each do |f|
			logger.info "checking for device id"
			if not f.to_s.empty?
				logger.info "found device id: " + f.to_s
				begin
					@current_device = Device.find(f)
					logger.info "@current_device set"
					return
				rescue Exception => e
					logger.warn "could not load device with ID #{f}: " + e.message
				end
			end
		end
	end
	
	# we require each oauth request to carry a device_id
	# parameter so we can keep track of devices.
	def ensure_proper_oauth
		if oauth?.is_a?(AccessToken)
				
			# /devices and /me OK
			if controller_name != "devices" &&
				!(
					controller_name == "registrations" &&
					action_name == "show" &&
					!(params[:id] && params[:id] != "me")
				)
				if params[:device_id]
					begin
						@current_device = Device.find(params[:device_id])
						@current_device.poke(current_client_application, request.ip)
					rescue
						error = { :message => "Invalid device ID. It either doesn't exist or is not owned by current user.", :code => 2 }
					end
				else
					error = { :message => "Missing device ID. Every request must have a device_id parameter.", :code => 1 }
				end
			end
			if error
				logger.debug "returning error: #{error[:message]}"
				respond_to do |format|
					format.xml  { render :xml => error, :status => :unprocessable_entity }
					format.json { render :json => error, :status => :unprocessable_entity }
					format.yaml { render :text => error.to_yaml, :content_type => 'text/yaml', :status => :unprocessable_entity }
				end
				return
			end
		end
	end
	
	helper_method :user_type
	def user_type
		return "Admin" if admin?
		return "Member" if user_signed_in?
		return "Visitor"
	end
	
	helper_method :admin?
	def admin?
		user_signed_in? && current_user.is_admin?
	end
	
	# make sure that non-admins cannot access the admin namespace
	def restrict_admin
		klass = self.class.name
		unless klass.index("::").nil?
			ns = klass.split("::").first.downcase
			ensure_admin if ns == "admin"
		end
	end
	
	def ensure_admin
		access_denied unless current_user && current_user.is_admin?
	end
	
	def access_denied
		if user_signed_in?
			redirect_to dashboard_url
			
		else
			if [:json, :xml].include? request.format.to_sym
				format = request.format.to_sym.to_s
				error = { :message => "authentication error", :code => 401 }
				self.status = 401
				self.content_type = request.format
				self.response_body = eval("error.to_#{format}")
			
			else
				session["user_return_to"] = request.url
				redirect_to login_url
			end
		end
	end
	
	def authenticate_user!(opts={})
		access_denied unless user_signed_in?
	end
	
	# map stuff between what oauth-plugin expects and what Devise gives:
	alias :logged_in? :user_signed_in?
	alias :login_required :authenticate_user!
	
	def current_user=(user)
		sign_in(:user, user)
	end
	
	def current_user
		user = super
		return current_token.user if user == nil and current_token != nil
		user
	end
	
	def process_me(id)
		if (!id || id == "me")
			redirect_to login_path and return unless current_user || current_token
			logger.debug "processing special user 'me'"
			user = current_user || current_token.user
			return user.id if user
		end
		return id
	end
	
	def h(str)
		return unless str
		if str.is_a?(String)
			str = CGI.escapeHTML(str)
			str.gsub!(/[']/, "&#39;")
			str.gsub!(/["]/, "&#34;")
			str.gsub!(/[\\]/, "&#92;")
			return str
		end
		return str.map { |s| h(s) } if str.is_a?(Array)
		return str.each { |a,b| str[a] = h(b) } if str.is_a?(Hash)
		str
	end
		
	def default_url_options(options={})
		case I18n.locale.to_s
		when 'cn', 'us'
			return { :l => nil }
		else
			return { :l => "#{I18n.locale}" }
		end
	end
	
	def verify_authenticity_token
		v = verified_request?
		o = oauth?
		logger.debug "verify_authenticity_token is passed: verified=#{v} || oauth=#{o}\n"
		v || o || raise(ActionController::InvalidAuthenticityToken)
	end
	
	private
	
	def after_sign_in_path_for(resource)
		stored_location_for(resource) || dashboard_path(:l => I18n.locale)
	end
	
	def after_sign_out_path_for(resource_or_scope)
		request.referer || login_path(:l => I18n.locale)
	end
	
	def set_locale
		I18n.locale =
			extract_locale_from_param ||
			extract_locale_from_tld || 
			extract_locale_from_subdomain ||
			extract_locale_from_cookie ||
			extract_locale_from_accept_language_header ||
			extract_locale_from_ip ||
			I18n.default_locale
	end
	
	# Get locale code from parameter.
	# Sets a session cookie if parameter found.
	def extract_locale_from_param
		parsed_locale = params[:l] || params[:locale] || params[:i18n_locale]
		if parsed_locale && I18n.available_locales.include?(parsed_locale.to_sym)
			cookies[:locale] = parsed_locale
			parsed_locale
		else
			nil
		end
	end
	
	# Get locale code from cookie.
	def extract_locale_from_cookie
		parsed_locale = cookies[:locale]
		if parsed_locale && I18n.available_locales.include?(parsed_locale.to_sym)
			parsed_locale
		else
			nil
		end
	end
	
	# Get locale from top-level domain or return nil if such locale is not available
	# You have to put something like:
	#   127.0.0.1 application.com
	#   127.0.0.1 application.it
	#   127.0.0.1 application.pl
	# in your /etc/hosts file to try this out locally
	def extract_locale_from_tld
		tld = request.host.split('.').last
		parsed_locale = case tld
			when 'hk' then 'cn'
			else tld
		end
		I18n.available_locales.include?(parsed_locale.to_sym) ? parsed_locale  : nil
	end
	
	# Get locale code from request subdomain (like http://it.application.local:3000)
	# You have to put something like:
	#   127.0.0.1 gr.application.local
	# in your /etc/hosts file to try this out locally
	def extract_locale_from_subdomain
		parsed_locale = request.subdomains.first
		I18n.available_locales.include?(parsed_locale.to_sym) ? parsed_locale : nil
	end
	
	# Get locale code from reading the "Accept-Language" header
	def extract_locale_from_accept_language_header
		l = request.env['HTTP_ACCEPT_LANGUAGE']
		if l
			l.scan(/^[a-z]{2}[-_]([a-zA-Z]{2})/).first.to_s.downcase
		end
	end
	
	# Get locale code from looking up location of the IP
	def extract_locale_from_ip
		o = origin_country(request.remote_ip)
		if o
			o.country_code2.downcase
		end
	end
	
	def check_tracking
		dnt = request.env['HTTP_DNT']
		@track = true
		@track = false if dnt == "1" && @browser != "ie"
	end
	
	# Gets the origin country of an IP
	def origin_country(ip)
		db = File.join(Rails.root, "lib", "assets", "GeoIP.dat")
		if File.exists? db
			GeoIP.new(db).country(ip)
		else
			nil
		end
	end
	helper_method :origin_country
	
	# Gets the origin city (and country) from an IP
	def origin_city(ip)
		db = File.join(Rails.root, "lib", "assets", "GeoLiteCity.dat")
		if File.exists? db
			GeoIP.new(db).city(ip)
		else
			nil
		end
	end
	helper_method :origin_city
	
	# Gets the origin network from an IP
	def origin_network(ip)
		db = File.join(Rails.root, "lib", "assets", "GeoIPASNum.dat")
		if File.exists? db
			GeoIP.new(db).asn(ip)
		else
			nil
		end
	end
	helper_method :origin_network
  
	def set_tab(controller = controller_name, action = action_name)
		@tab = ""
		if controller == "pages" and action == "download"
			@tab = "get"
		elsif controller == "pages"
			@tab = action
		elsif controller == "oauth_clients"
			@tab = "apps"
		elsif controller == "devices"
			@tab = "devices"
		elsif controller == "donations"
			@tab = "donations"
		elsif controller == "registrations" and action == "new"
			@tab = "join"
		elsif controller == "registrations" and (action == "edit" or action == "settings")
			@tab = "settings"
		elsif controller == "registrations"
			@tab = "dashboard"
		elsif controller == "contribute"
			@tab = "contribute"
		elsif controller == "sessions"
			@tab = "login"
		end
		@c = controller
		@a = action
	end
	
	def classify_device
		@ua = request.user_agent.to_s.downcase
		#@ua = "Opera/5.02 (Macintosh; U; id)"
		#@ua = "Mozilla/4.0 (compatible; MSIE 5.0; Windows 2000) Opera 6.03 [en]"
		#@ua = "Opera/9.64 (X11; Linux i686; U; Linux Mint; nb) Presto/2.1.1"
		#@ua = "Opera/9.80 (X11; Linux i686; U; pt-BR) Presto/2.2.15 Version/10.00"
		#@ua = "Opera/9.80 (Macintosh; Intel Mac OS X 10.6.8; U; de) Presto/2.9.168 Version/11.52"
		#@ua = "Opera/12.80 (Windows NT 5.1; U; en) Presto/2.10.289 Version/12.02"
		
		#@ua = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_8_2) AppleWebKit/537.17 (KHTML, like Gecko) Chrome/24.0.1309.0 Safari/537.17"
		#@ua = "Mozilla/5.0 (X11; Linux i686) AppleWebKit/535.1 (KHTML, like Gecko) Ubuntu/11.04 Chromium/14.0.825.0 Chrome/14.0.825.0 Safari/535.1"
		#@ua = "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US) AppleWebKit/534.14 (KHTML, like Gecko) Chrome/10.0.601.0 Safari/534.14"
		#@ua = "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US) AppleWebKit/533.8 (KHTML, like Gecko) Chrome/6.0.397.0 Safari/533.8"
		#@ua = "Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US) AppleWebKit/525.19 (KHTML, like Gecko) Chrome/1.0.154.46 Safari/525.19"
		
		#@ua = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)"
		#@ua = "Mozilla/5.0 (Windows; U; MSIE 9.0; Windows NT 9.0; en-US)"
		#@ua = "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; GTB7.4; InfoPath.2; SV1; .NET CLR 3.3.69573; WOW64; en-US)"
		#@ua = "Mozilla/5.0 (Windows; U; MSIE 7.0; Windows NT 5.2)"
		#@ua = "Mozilla/4.0 (Windows; MSIE 6.0; Windows NT 5.2)"
		
		#@ua = "Mozilla/5.0 (Windows NT 6.2; Win64; x64; rv:16.0.1) Gecko/20121011 Firefox/16.0.1"
		#@ua = "Mozilla/5.0 (Windows NT 6.1; rv:12.0) Gecko/20120403211507 Firefox/12.0"
		#@ua = "Mozilla/5.0 (X11; U; Linux x86_64; pl-PL; rv:2.0) Gecko/20110307 Firefox/4.0"
		#@ua = "Mozilla/5.0 (Macintosh; U; Intel Mac OS X; de-AT; rv:1.9.1.8) Gecko/20100625 Firefox/3.6.6"
		#@ua = "Mozilla/5.0 (X11; U; Linux i686; en; rv:1.8.1.2) Gecko/20070220 Firefox/2.0.0.2"
		
		#@ua = "Mozilla/5.0 (iPad; CPU OS 6_0 like Mac OS X) AppleWebKit/536.26 (KHTML, like Gecko) Version/6.0 Mobile/10A5355d Safari/8536.25"
		#@ua = "Mozilla/5.0 (Macintosh; U; Intel Mac OS X 10_5_8; zh-tw) AppleWebKit/533.16 (KHTML, like Gecko) Version/5.0 Safari/533.16"
		#@ua = "Mozilla/5.0 (Macintosh; U; Intel Mac OS X 10_5_7; en-us) AppleWebKit/530.19.2 (KHTML, like Gecko) Version/4.0.1 Safari/530.18"
		#@ua = "Mozilla/5.0 (Macintosh; U; PPC Mac OS X; ca-es) AppleWebKit/522.11.1 (KHTML, like Gecko) Version/3.0.3 Safari/522.12.1"
		#@ua = "Mozilla/5.0 (Macintosh; U; PPC Mac OS X; de-de) AppleWebKit/124 (KHTML, like Gecko) Safari/125.1"
		
		embedder = request.env['HTTP_EMBEDDER']
		begin
			embedder = embedder.split('/')
			@browser = embedder[0].downcase
			@embedder_version = embedder[1]
		rescue
			embedder = nil
		end
			
		if embedder.to_s == ""
			case @ua
			when /facebook/i
				@browser = "facebook"
			when /googlebot/i
				@browser = "google"
			when /chrome/i
				@browser = "chrome"
			when /opera/i
				@browser = "opera"
			when /firefox/i
				@browser = "firefox"
			when /safari/i
				@browser = "safari"
			when /msie/i
				@browser = "ie"
			else
				@browser = "unknown"
			end
		end

		case @ua
		when /windows nt 6.2/i
			@os = "windows 8"
		when /windows nt 6.1/i
			@os = "windows 7"
		when /windows phone/i
			@os = "windows phone"
		when /windows/i
			@os = "windows old"
		when /iphone/i
			@os = "iphone"
		when /android/i
			@os = "android"
		when /linux/i
			@os = "linux"
		when /mac/i
			@os = "mac"
		else
			@os = "unknown"
		end
	end
	
	def check_old_browsers
		return if cookies[:skip_old]
		return if mobile_device? or embedded_device?
		return if ["facebook","google"].include? @browser
		return if controller_name == "pages" and action_name == "old"
		
		logger.info "ua: #{@ua}"
		logger.info "browser: #{@browser}"
		logger.info "os: #{@os}"
		logger.info "embedded_device? #{embedded_device?}"
		logger.info "mobile_device? #{mobile_device?}"
		
		if params[:dangerous]
			cookies[:skip_old] = "1"
		else
			begin
				old = case @browser
				when "ie"
					v = @ua.match(/ msie (\d+\.\d+)/)[1].to_i
					v < 9
					
				when "firefox"
					v = @ua.match(/ firefox\/(\d[\d\.]*\d)/)[1].to_i
					v < 10
					
				when "opera"
					ua = @ua.split
					ua.pop if ua[-1].match(/\[\w\w\]/)
					if ua[-1].start_with? "version/"
						v = ua[-1].split('/')[1].to_i
					elsif ua[-2] == "opera"
						v = ua[-1].to_i
					elsif ua[0].start_with? "opera/"
						v = ua[0].split('/')[1].to_i
					else
						v = 0
					end
					v < 10
					
				when "chrome"
					v = @ua.match(/ chrome\/(\d[\d\.]*\d) /)[1].to_i
					v < 10
					
				when "safari"
					m = @ua.match(/ version\/(\d[\d\.]*\d) /)
					if m
						v = m[1].to_i
					else
						v = 0
					end
					v < 5
					
				else
					false
				end
				
				if old
					render("pages/old", :l => I18n.locale, :layout => false) and return
				end
			rescue
			end
		end
	end
	
	def mobile_device?
		if cookies[:mobile_param]
			cookies[:mobile_param] == "1"
		else
			request.user_agent =~ /Mobile|webOS/
		end
	end
	helper_method :mobile_device?
	
	def embedded_device?
		if cookies[:embedded_param]
			cookies[:embedded_param] == "1"
		else
			request.env['HTTP_EMBEDDER'] != nil or @ua.match(/ msie 7\.0;.* \.net4.0/)
		end
	end
	helper_method :embedded_device?
	
	def prepare_for_mobile
		cookies[:mobile_param] = params[:mobile] if params[:mobile]
		request.format = :mobile if mobile_device? && adaptable_format?
	end
	
	def prepare_for_embedded
		cookies[:embedded_param] = params[:embedded] if params[:embedded]
		request.format = :embedded if embedded_device? && adaptable_format?
	end
	
	def adaptable_format?
		request.format == :html || request.format.to_s == "*/*"
	end
	
	def pagination_params
		max_l = 50
		min_l = 1
		default_l = 25
		
		min_o = 0
		default_o = 0
		
		l = params[:limit] || default_l
		o = params[:offset] || default_o
		
		l = l.to_i
		o = o.to_i
		
		l = max_l if l > max_l
		l = min_l if l < min_l
		o = min_o if o < min_o
		
		return l, o
	end
end

class String
	def possessive
		l = ""
		l = I18n.locale.to_s if I18n
		case l
		
		when 'se'
			self + case self[-1,1]
			when 's' then ""
			else "s"
			end
			
		else
			self + case self[-1,1]
			when 's' then "'"
			else "'s"
			end
		end
	end
	
	def downcase
		self.tr 'A-ZÅÄÖƐƆŊ', 'a-zåäöɛɔŋ'
	end
end
