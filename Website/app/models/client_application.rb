require 'oauth'
require 'base'
class ClientApplication < ActiveRecord::Base
	include Base
	
	# associations
	belongs_to :user
	has_many :tokens, :class_name => "OauthToken"
	has_many :access_tokens
	has_many :oauth2_verifiers
	has_many :oauth_tokens
	
	# validations
	validates_presence_of :name, :website, :key, :secret
	validates_uniqueness_of :key
	before_validation :generate_keys, :on => :create

	validates_format_of :website, :with => /\Ahttp(s?):\/\/(\w+:{0,1}\w*@)?(\S+)(:[0-9]+)?(\/|\/([\w#!:.?+=&%@!\-\/]))?/i
	validates_format_of :support_url, :with => /\Ahttp(s?):\/\/(\w+:{0,1}\w*@)?(\S+)(:[0-9]+)?(\/|\/([\w#!:.?+=&%@!\-\/]))?/i, :allow_blank=>true
	validates_format_of :callback_url, :with => /\Ahttp(s?):\/\/(\w+:{0,1}\w*@)?(\S+)(:[0-9]+)?(\/|\/([\w#!:.?+=&%@!\-\/]))?/i, :allow_blank=>true

	attr_accessor :token_callback_url
	
	def self.not_added_by(user)
		if user == nil
			return self.all
		else
			tokens = "SELECT * from oauth_tokens WHERE oauth_tokens.invalidated_at IS NULL AND oauth_tokens.authorized_at IS NOT NULL AND oauth_tokens.user_id = #{user.id}"
		
			return select("client_applications.*").
			joins("LEFT JOIN (#{tokens}) oauth_tokens ON oauth_tokens.client_application_id = client_applications.id").
			group("client_applications.id").
			having("count(oauth_tokens.id) = 0")
		end
	end

	def self.find_token(token_key)
		token = OauthToken.find_by_token(token_key, :include => :client_application)
		logger.debug "checking token: #{token_key}"
		logger.debug "found token: #{token != nil}"
		logger.debug "token authed: #{token}"
		if token && token.authorized?
			token
		else
			nil
		end
	end

	def self.verify_request(request, options = {}, &block)
		begin
			signature = OAuth::Signature.build(request, options, &block)
			return false unless OauthNonce.remember(signature.request.nonce, signature.request.timestamp)
			value = signature.verify
			value
		rescue OAuth::Signature::UnknownSignatureMethod => e
			false
		end
	end

	def oauth_server
		@oauth_server ||= OAuth::Server.new("http://beta.stoffiplayer.com")
	end

	def credentials
		@oauth_client ||= OAuth::Consumer.new(key, secret)
	end

	# If your application requires passing in extra parameters handle it here
	def create_request_token(params={})
		RequestToken.create :client_application => self, :callback_url=>self.token_callback_url
	end
	
	def large_icon
		return icon_64 unless icon_64.to_s.empty?
		"/assets/gfx/app_default_icon_64.png"
	end
	
	def small_icon
		return icon_16 unless icon_16.to_s.empty?
		"/assets/gfx/app_default_icon_16.png"
	end
	
	def kind
		"app"
	end
	
	def display
		name
	end
	
	def serialize_options
		{
			:except => :secret,
			:methods => [ :kind, :display, :url ]
		}
	end

protected

	def generate_keys
		self.key = OAuth::Helper.generate_key(40)[0,40]
		self.secret = OAuth::Helper.generate_key(40)[0,40]
	end
end
