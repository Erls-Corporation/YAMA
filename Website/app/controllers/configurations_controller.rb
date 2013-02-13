class ConfigurationsController < ApplicationController

	#before_filter :authenticate_user!
	oauthenticate
	respond_to :html, :mobile, :embedded, :xml, :json
	
	# GET /configurations
	def index
		l, o = pagination_params
		@title = t("remote.title")
		@description = t("remote.description")
		@configurations = current_user.configurations.limit(l).offset(o)
		respond_with(@configurations)
	end

	# GET /configurations/1
	def show
		@configuration = current_user.configurations.find(params[:id])
		@devices = current_user.devices.order(:name)
		@title = t("remote.title")
		@description = t("remote.description")
		respond_with(@configuration)
	end

	# GET /configurations/new
	def new
		@configuration = current_user.configurations.new
		respond_with(@configuration)
	end

	# GET /configurations/1/edit
	def edit
		render :text => "not yet" and return
		@configuration = current_user.configurations.find(params[:id])
		@title = "Edit synchronization profile '#{h(@configuration.name)}'"
		@description = "Modify the synchronization profile named #{h(@configuration.name)}"
	end

	# POST /configurations
	def create
		@configuration = current_user.configurations.new(params[:configuration])
		
		success = @configuration.save
		SyncController.send('create', @configuration) if success
		respond_with(@configuration)
	end

	# PUT /configurations/1
	def update
		@configuration = current_user.configurations.find(params[:id])
		
		if params[:configuration] && params[:configuration][:current_track]
			song = Song.get(current_user, params[:configuration][:current_track])
			params[:configuration][:current_track_id] = song.id if song.is_a?(Song)
			params[:configuration].delete(:current_track)
		end
		success = @configuration.update_attributes(params[:configuration])
		SyncController.send('update', @configuration) if success
		respond_with(@configuration)
	end

	# DELETE /configurations/1
	def destroy
		@configuration = current_user.configurations.find(params[:id])
		SyncController.send('delete', @configuration) if success
		@configuration.destroy
		respond_with(@configuration)
	end
	
	# PUT /configurations/1/next
	def next
		@configuration = current_user.configurations.find(params[:id])
		SyncController.send('execute', @configuration, 'next')
		respond_with(@configuration)
	end
	
	# PUT /configurations/1/prev
	def prev
		@configuration = current_user.configurations.find(params[:id])
		SyncController.send('execute', @configuration, 'prev')
		respond_with(@configuration)
	end
	
	# PUT /configurations/1/play-pause
	def play_pause
		@configuration = current_user.configurations.find(params[:id])
		SyncController.send('execute', @configuration, 'play-pause')
		respond_with(@configuration)
	end
	
	# PUT /configurations/1/play
	def play
		@configuration = current_user.configurations.find(params[:id])
		SyncController.send('execute', @configuration, 'play')
		respond_with(@configuration)
	end
	
	# PUT /configurations/1/pause
	def pause
		@configuration = current_user.configurations.find(params[:id])
		SyncController.send('execute', @configuration, 'pause')
		respond_with(@configuration)
	end
end
