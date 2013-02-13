class Users::PasswordsController < Devise::PasswordsController
	def edit
		u = User.find_by_reset_password_token(params[:reset_password_token])
		@email = u.email if u
		
		@title = t "reset.title"
		@description = t "reset.description"
		
		super
	end
	
	def new
		@title = t "forgot.title"
		@description = t "forgot.description"
		super
	end
end