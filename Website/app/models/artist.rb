require 'wikipedia'
require 'mediacloth'
require 'wikicloth'
require 'base'

class Artist < ActiveRecord::Base
	extend StaticBase
	include Base
	
	# associations
	has_and_belongs_to_many :albums
	has_and_belongs_to_many :songs
	has_many :listens, :through => :songs
	has_many :donations
	
	def self.default_pic
		"/assets/media/artist.png"
	end
	
	def image
		return @wiki_img if @wiki_img
		logger.info "finding picture for #{name}"
		Wikipedia.Configure do
			domain "en.wikipedia.org"
			path   "w/api.php"
		end
		p = page
		if p && p.image_urls && p.image_urls.count > 0
			@wiki_img = p.image_urls.last # last one is the one on the right
			self.picture = @wiki_img
			self.save
			@wiki_img
		else
			nil
		end
	end
	
	def photo
		s = picture
		return s if s.to_s != ""
		
		#i = image
		return Artist.default_pic
		
		update_attribute(:picture, i)
		save
		return i
	end
	
	def twitter?; twitter.to_s != "" end
	def facebook?; facebook.to_s != "" end
	def googleplus?; googleplus.to_s != "" end
	def myspace?; myspace.to_s != "" end
	def youtube?; youtube.to_s != "" end
	def soundcloud?; soundcloud.to_s != "" end
	def spotify?; spotify.to_s != "" end
	def lastfm?; lastfm.to_s != "" end
	def website?; website.to_s != "" end
	
	def twitter_url; "https://twitter.com/#{twitter}" end
	def facebook_url; "https://facebook.com/#{facebook}" end
	def googleplus_url; "https://plus.google.com/#{googleplus}" end
	def myspace_url; "https://myspace.com/#{myspace}" end
	def youtube_url; "https://youtube.com/user/#{youtube}" end
	def soundcloud_url; "https://soundcloud.com/#{soundcloud}" end
	def spotify_url; "http://open.spotify.com/artist/#{spotify}" end
	def lastfm_url; "https://last.fm/music/#{lastfm}" end
	def website_url; website end
	
	def any_places?
		twitter? or facebook? or googleplus? or myspace? or
		youtube? or soundcloud? or spotify? or lastfm? or website?
	end
	
	def play
		"http://www.google.com"
	end
	
	def info
		p = localized_page
		return "" unless p && p.content
		logger.debug "parsing page #{p.title}"
			
		# extract the top-most section (the one before any ToC)
		c = WikiCloth::Parser.new({ :data => WikiParser::sanitize(p.content) })
		c = c.sections.first
		
		l = I18n.locale
		ret = WikiParser.new({ :data => c }).to_html
		I18n.locale = l
		return ret
	end
	
	def wikipedia_link
		p = localized_page
		base = "https://#{langtag(I18n.locale)}.wikipedia.org"
		return base unless p && p.title
		"#{base}/wiki/#{p.title}"
	end
	
	def unknown?
		return name.to_s == ""
	end
	
	def display
		name
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
		donated.sum("amount * (artist_percentage / 100)").to_f.round(2)
	end
	
	def pending
		donations.where("donations.status = 'pending' AND created_at < ?", Donation.revoke_time)
	end
	
	def pending_sum
		pending.sum("amount * (artist_percentage / 100)").to_f.round(2)
	end
	
	def undonatable
		unknown? || donatable_status.to_s == ""
	end
	
	def paginate_songs(limit, offset)
		@paginated_songs = Array.new
		songs.limit(limit).offset(offset).each do |song|
			@paginated_songs << song
		end
	end
	
	def paginated_songs
		return @paginated_songs
	end
	
	def serialize_options
		{
			:methods => [ :kind, :display, :url, :info, :photo ],
			:except => [ :picture ]
		}
	end
	
	def self.search(search, limit = 5)
		if search
			self.select("artists.id, artists.name, artists.picture, count(listens.id) AS listens_count").
			joins(:songs).
			joins("LEFT JOIN listens ON listens.song_id = songs.id").
			where("artists.name LIKE ?", "%#{search}%").
			group("artists.id").
			limit(limit)
		else
			scoped
		end
	end
	
	def self.get(value)
		value = self.find(value) if value.is_a?(Integer)
		value = self.find_or_create_by_name(h(value)) if value.is_a?(String)
		return value if value.is_a?(Artist)
		return nil
	end
	
	def self.top(limit = 5, type = :played, user = nil)
		case type
		when :supported
			self.select("artists.id, artists.name, artists.picture, sum(donations.amount) AS c").
			joins(:donations).
			where(user == nil ? "" : "donations.user_id = #{user.id}").
			where("donations.status != 'returned' AND donations.status != 'failed' AND donations.status != 'revoked'").
			group("artists.id").
			order("c DESC")
			
		when :played
			self.select("artists.id, artists.name, artists.picture, count(listens.id) AS c").
			joins(:songs).
			joins("LEFT JOIN listens ON listens.song_id = songs.id").
			where(user == nil ? "" : "listens.user_id = #{user.id}").
			where("artists.name != '' AND artists.name IS NOT NULL").
			group("artists.id").
			order("c DESC").
			limit(limit)
		
		else
			raise "Unsupported type"
		end
	end
	
	def self.donations
	end
	
	# we keep a list of already visited pages
	# to prevent endless loops when hunting for
	# an artist classified page
	@visited_pages = []
	
	private
	
	# cache wikipedia stuff
	@wiki_page = nil
	@wiki_img = nil
	
	# returns a translated wikipedia page for
	# the artist. defaults to english if none found.
	def localized_page
		return @page if @page
		l = langtag(I18n.locale)
		
		Wikipedia.Configure do
			domain "#{l}.wikipedia.org"
			path   "w/api.php"
		end
		p = page
		if p == nil || p.content == nil
			logger.debug "defaulting to english"
			Wikipedia.Configure do
				domain "en.wikipedia.org"
				path   "w/api.php"
			end
			p = page
		else
			logger.debug "sending translated"
		end
		@page = p
		@page
	end
	
	# returns the wikipedia page for the artist
	def page
		@visited_pages = []
		logger.debug "retreiving page for #{name}"
		find_page(name, false)
	end
	
	# analyses a page and follows 
	def find_page(pname, verify = true)
		logger.debug "looking up page: #{pname}"
		logger.debug "verify find: #{verify}"
		r = Wikipedia.find(pname)
		
		# parse disambiguation meta data
		unless is_artist_page? r
			logger.debug "check for disambiguation meta data"
		
			# check for {about|A|B|1|C|2...|Z|N} patterns
			# where we are intrested in B|1 - Z|N
			m = r.content.scan(/\{about\|[\w\s]*((\|[^\}\{\|]*\|[^\}\{\|]*)*)\}/) if r.content
			unless m == nil || m.empty? || m.first.empty?
				# l = ["B", "1", "C", "2", ... , "Z", "N"]
				l = m.first.first[1..-1].split("|")
				1.step(l.size-1,2).each do |i|
					# check pages "1", "2" .. "N"
					p = find_page(l[i])
					r = p if p
					break
				end
			end
		end
		
		# parse links
		logger.debug "follow links (desperate!)" if !is_artist_page?(r) && is_disambiguation_page?(r)
		r = follow_links(r) if !is_artist_page?(r) && is_disambiguation_page?(r)
		
		# verify category
		(!verify || is_artist_page?(r)) ? r : nil
	end
	
	# follows a page's links in the hunt for an artist page
	def follow_links(p)
		if p.links
			p.links.each do |l|
				next if @visited_pages.include? l
				
				lp = Wikipedia.find(l)
				if lp.is_a? Array
					lp.each do |lp_i|
						logger.debug "following to: #{lp_i.title}"
						if is_artist_page? lp_i
							p = lp_i
							break
						end
					end
				elsif not is_artist_page? p
					if is_artist_page? lp
						p = lp
						break
					end
				end
			end
		end
		p
	end
	
	# checks if a wikipedia page is classified as
	# an artist page
	def is_artist_page?(p)
		return false unless p && p.content && p.categories && p.title
		belongs_to_categories? p, [
			"musicians",
			"artists",
			"duos",
			"groups",
			"singers",
			"guitarists",
			"gitarrister",
			"sångare",
			"grupper",
			"musiker"
		], true
	end
	
	# checks if a wikipedia page is classified as
	# an disambiguation page
	def is_disambiguation_page?(p)
		logger.debug "is disambiguation page?"
		belongs_to_categories? p, [
			"Category:All article disambiguation pages",
			"Category:All disambiguation pages",
			"Category:Disambiguation pages",
			"Kategori:Förgreningssidor"
		]
	end
	
	# checking if a wikipedia page belongs to any of a set of category
	def belongs_to_categories?(p, categories, only_last = false)
		return false unless p && p.content && p.categories && p.title
		@visited_pages << p.title
		p.categories.each do |c|
			c = c.split.last if only_last
			if categories.include? c
				logger.debug "found category!"
				return true
			else
				logger.debug "missed category: #{c}"
			end
		end
		logger.debug "not in any category"
		return false
	end
	
	def langtag(locale)
		case locale.to_s
		when 'se' then 'se'
		when 'us' then 'en'
		when 'uk' then 'en'
		when 'cn' then 'zh'
		else locale
		end
	end
end
