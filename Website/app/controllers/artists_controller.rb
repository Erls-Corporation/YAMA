class ArtistsController < ApplicationController

	oauthenticate :interactive => true, :except => [ :index, :show ]
	before_filter :ensure_admin, :except => [ :index, :show ]
	respond_to :html, :mobile, :embedded, :xml, :json
	
	# GET /artists
	def index
		l, o = pagination_params
		respond_with(@artists = Artist.limit(l).offset(o))
	end

	# GET /artists/1
	def show
		l, o = pagination_params
		@artist = Artist.find(params[:id])
		@title = @artist.name
		@description = t "artist.description", :artist => @artist.name
		@head_prefix = "og: http://ogp.me/ns# fb: http://ogp.me/ns/fb# stoffiplayer: http://ogp.me/ns/fb/stoffiplayer#"
		@meta_tags =
		[
			{ :property => "og:title", :content => @artist.name },
			{ :property => "og:type", :content => "stoffiplayer:artist" },
			{ :property => "og:image", :content => @artist.picture },
			{ :property => "og:url", :content => @artist.url },
			{ :property => "og:site_name", :content => "Stoffi" },
			{ :property => "fb:app_id", :content => "243125052401100" },
			{ :property => "og:description", :content => t("artist.short_description", :artist => @artist.name) },
			{ :property => "stoffiplayer:donations", :content => @artist.donations.count },
			{ :property => "stoffiplayer:support_generated", :content => "$#{@artist.donated}" },
			{ :property => "stoffiplayer:charity_generated", :content => "$#{@artist.charity}" }
		]
		
		@donations = @artist.donations

		@artist.paginate_songs(l, o)
		respond_with(@artist, :methods => [ :paginated_songs ])
	end

	# GET /artists/new
	def new
		respond_with(@artist = Artist.new)
	end

	# GET /artists/1/edit
	def edit
		@artist = Artist.find(params[:id])
	end

	# POST /artists
	def create
		@artist = Artist.new(params[:artist])
		@artist.save
		respond_with(@artist)
	end

	# PUT /artists/1
	def update
		@artist = Artist.find(params[:id])
		
		if params[:donation_update]
			@artist.donations.each do |donation|
				unless donation.update_attributes(params[:donation])
					respond_to do |format|
						format.html { render :action => "edit" }
						format.xml  { render :xml => donation.errors, :status => :unprocessable_entity }
						format.json { render :json => donation.errors, :status => :unprocessable_entity }
						format.yaml { render :text => donation.errors.to_yaml, :content_type => 'text/yaml', :status => :unprocessable_entity }
					end and return
				end
			end
			result = true
		else
			result = @artist.update_attributes(params[:artist])
		end
		
		respond_with(@artist)
	end

	# DELETE /artists/1
	def destroy
		render :status => :forbidden and return if ["xml","json"].include?(params[:format])
		@artist = Artist.find(params[:id])
		@artist.destroy
		respond_with(@artist)
	end
end
