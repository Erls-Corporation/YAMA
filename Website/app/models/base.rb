module StaticBase
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
end

module Base
	def kind
		self.class.name.downcase
	end
	
	def base_url
		"http://beta.stoffiplayer.com"
	end
	
	def url
		"#{base_url}/#{kind.pluralize}/#{id}"
	end
	
	def to_param
		if display.to_s.empty?
			id.to_s
		else
			"#{id}-#{display.gsub(/[^a-z0-9]+/i, '-')}"
		end
	end

	def serialize_options
		{
			:methods => [ :kind, :display, :url ]
		}
	end
	
	def as_json(options = {})
		super(DeepMerge.deep_merge!(serialize_options, options))
	end
	
	def to_xml(options = {})
		super(DeepMerge.deep_merge!(serialize_options, options))
	end
	
	def h(str)
		self.class.h(str)
	end
end