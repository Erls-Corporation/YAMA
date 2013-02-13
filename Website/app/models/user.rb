require 'base'
class User < ActiveRecord::Base
	include Base
	
	# Include default devise modules. Others available are:
	# :token_authenticatable, :encryptable, :confirmable, :omniauthable, :timeoutable and 
	devise :database_authenticatable, :registerable, 
	       :recoverable, :rememberable, :trackable, :validatable, :lockable
		 
	has_many :links, :dependent => :destroy
	has_many :devices, :dependent => :destroy
	has_many :sources, :dependent => :destroy
	has_many :configurations, :dependent => :destroy
	has_many :list_configs, :dependent => :destroy
	has_many :columns, :dependent => :destroy
	has_many :column_sorts, :dependent => :destroy
	has_many :playlists, :dependent => :destroy
	has_many :shares, :dependent => :destroy
	has_many :listens, :dependent => :destroy
	has_many :keyboard_shortcuts, :dependent => :destroy
	has_many :keyboard_shortcut_profiles, :dependent => :destroy
	has_many :equalizer_profiles, :dependent => :destroy
	has_many :votes, :dependent => :destroy
	has_many :translations
	has_many :donations
	has_many :apps, :class_name => "ClientApplication", :dependent => :destroy
	has_many :tokens, :class_name => "OauthToken", :order => "authorized_at desc", :include => [:client_application], :dependent => :destroy
	has_and_belongs_to_many :songs
	has_and_belongs_to_many :playlist_subscriptions, :class_name => "Playlist", :join_table => "playlist_subscribers"

	# Setup accessible (or protected) attributes for your model
	attr_accessible :email, :password, :password_confirmation, :remember_me, :unique_token, :id,
		:name_source, :custom_name, :image, :show_ads, :has_password
	
	def name
		s = name_source.to_s
		
		return User.default_name if email.to_s == "" and s == "" and custom_name.to_s == ""
		return email.split('@')[0].titleize if s == "" and custom_name.to_s == ""
		return custom_name if s == ""
		
		p,v = s.split("::",2)
		return s unless ["twitter","facebook","google_oauth2","lastfm","vimeo"].include? p
		l = self.links.find_by_provider(p)
		return User.default_name unless l
		names = l.names
		return User.default_name unless (names.is_a?(Hash) and v and names[v.to_sym])
		names[v.to_sym]
	end
	
	def self.default_pic
		"/assets/media/user.png"
	end
	
	def self.default_name
		"Anon"
	end
	
	def unique_hash
		if self.unique_token.blank?
			update_attribute(:unique_token, Devise.friendly_token[0,50].to_s)
		end
		Digest::SHA2.hexdigest(self.unique_token + id.to_s)
	end
	
	def points(artist = nil)
		w = artist ? " AND artist_id = #{artist.id}" : ""
		w = "status != 'returned' AND status != 'failed' AND status != 'revoked'#{w}"
		if artist
			l = artist.listens.where("user_id = ?", id).count
		else
			l = listens.count
		end
		d = donations.where(w).sum(:amount)
		
		return l + (d * 1000)
	end
	
	def picture
		s = image.to_s
		return User.default_pic if s == ""
		if [:gravatar, :identicon, :monsterid, :wavatar, :retro].include? s.to_sym
			s = s == :gravatar ? :mm : s.to_sym
			return gravatar(type)
		else
			l = self.links.find_by_provider(s)
			return User.default_pic unless l
			pic = l.picture
			return User.default_pic unless pic
			return pic
		end
		return User.default_pic
	end
	
	def get_apps(scope)
		case scope
		when :added
			return ClientApplication.select("client_applications.*").
			joins(:oauth_tokens).
			where("oauth_tokens.invalidated_at is null and oauth_tokens.authorized_at is not null and oauth_tokens.type = 'AccessToken'").
			where("client_applications.id = oauth_tokens.client_application_id and oauth_tokens.user_id = #{id}").
			group("client_applications.id").
			order("oauth_tokens.authorized_at DESC")
		
		when :created
			return apps
		end
	end
	
	def is_admin?
		self.admin
	end
	
	def url
		"http://beta.stoffiplayer.com/profile/#{id}"
	end
	
	def gravatar(type)
		gravatar_id = Digest::MD5.hexdigest(email.to_s.downcase)
		force = type == :mm ? "" : "&f=y"
		"https://gravatar.com/avatar/#{gravatar_id}.png?s=48&d=#{type}#{force}"
	end
	
	def self.find_or_create_with_omniauth(auth)
		link = Link.find_by_provider_and_uid(auth['provider'], auth['uid'])
		user = User.find_by_email(auth['info']['email']) if auth['info']['email']
		
		# link found
		if link
			d = auth['credentials']['expires_at']
			d = DateTime.strptime("#{d}",'%s') if d
			link.update_attributes(
				:access_token => auth['credentials']['token'],
				:access_token_secret => auth['credentials']['secret'],
				:refresh_token => auth['credentials']['refresh_token'],
				:token_expires_at => d
			)
			return link.user
		
		# email already registrered, create link for that user
		elsif auth['info'] && auth['info']['email'] && user
			user.create_link(auth)
			return user
		
		# create a new user and a link for that user
		else
			return create_with_omniauth(auth)
		end
	end

	def self.create_with_omniauth(auth)
		email = auth['info']['email']
		pass = Devise.friendly_token[0,20]
	
		# create user
		user = User.new(
			:email => email,
			:password => pass,
			:password_confirmation => pass,
			:has_password => false
		)
		user.save(:validate => false)
		
		# create link
		user.create_link(auth)
		
		return user
	end
	
	def update_with_password(params={})
		current_password = params.delete(:current_password) if !params[:current_password].blank?
		
		if params[:password].blank?
			params.delete(:password)
			params.delete(:password_confirmation) if params[:password_confirmation].blank?
		end
		
		result = if has_no_password? || valid_password?(current_password)
			update_attributes(params)
			update_attribute(:has_password, true)
		else
			self.errors.add(:current_password, current_password.blank? ? :blank : :invalid)
			self.attributes = params
			false
		end
		
		clean_up_passwords
		result
	end
	
	def has_no_password?
		!self.has_password
	end
	
	def charity
		donations.where("status != 'returned' AND status != 'failed' AND status != 'revoked'")
	end
	
	def charity_sum
		charity.sum("amount * (charity_percentage / 100)").to_f.round(2)
	end
	
	def donated
		donations.where("status != 'returned' AND status != 'failed' AND status != 'revoked'")
	end
	
	def donated_sum
		donated.sum(:amount).to_f.round(2)
	end
	
	def self.top(limit = 5, type = :supporters)
		
		case type
		when :supporters
			self.select("users.id, users.name_source, users.custom_name, users.image, users.email, sum(donations.amount) AS c").
			joins(:donations).
			where("donations.status != 'returned' AND donations.status != 'failed' AND donations.status != 'revoked'").
			group("users.id").
			order("c DESC")
		else
			raise "Unsupported type"
		end
	end
	
	alias display name
	
	def create_link(auth)
		exp = auth['credentials']['expires_at']
		exp = DateTime.strptime("#{exp}",'%s') if exp
		links.create(
			:provider => auth['provider'],
			:uid => auth['uid'],
			:access_token => auth['credentials']['token'],
			:access_token_secret => auth['credentials']['secret'],
			:refresh_token => auth['credentials']['refresh_token'],
			:token_expires_at => exp
		)
	end
	
	def encrypted_uid(provider)
		links.each do |link|
			if link.provider == provider
				return link.encrypted_uid
			end
		end
		return nil
	end
	
	def serialize_options
		{
			:except =>
			[
				:has_password, :created_at, :unique_token, :updated_at, :custom_name,
				:admin, :show_ads, :name_source, :image, :email
				
			],
			:methods => [ :kind, :display, :url ]
		}
	end
end
