class Users::SessionsController < Devise::SessionsController
	def new
		if request.referer && ![login_url, join_url, unlock_url, forgot_url].index(request.referer)
			session["user_return_to"] = request.referer
		end
		
		@title = t "login.title"
		@description = t "login.description"
		
		super
	end
end