class Users::UnlocksController < Devise::UnlocksController
	def new
		@title = t "unlock.title"
		@description = t "unlock.description"
		super
	end
end