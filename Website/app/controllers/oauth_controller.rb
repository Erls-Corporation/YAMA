require 'oauth/controllers/provider_controller'
class OauthController < ApplicationController
	include OAuth::Controllers::ProviderController

	protected
	
	# Override this to match your authorization page form
	# It currently expects a checkbox called authorize
	def user_authorizes_token?
		if @token && @token.client_application
			logger.info "app : " + @token.client_application.id.to_s
		end
		if @token && @token.user
			logger.info "user: " + current_user.id.to_s
		end
		
		# look for existing token if previously authorized
		token = current_user.tokens.where(
			"client_application_id = ? AND type = 'AccessToken' AND invalidated_at IS NULL AND authorized_at IS NOT NULL",
			@token.client_application.id).first
		
		(request.post? && params[:authorize] == '1') || (token && !token.invalidated?)
	end
	
	#def authorize
	#	logger.info "##################### AUTHORIZE! ###############################"
	#	super
	#end

	# should authenticate and return a user if valid password.
	# This example should work with most Authlogic or Devise. Uncomment it
	# def authenticate_user(username,password)
	#   user = User.find_by_email params[:username]
	#   if user && user.valid_password?(params[:password])
	#     user
	#   else
	#     nil
	#   end
	# end

end
