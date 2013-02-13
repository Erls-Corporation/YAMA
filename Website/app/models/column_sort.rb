class ColumnSort < ActiveRecord::Base
	belongs_to :list_config
	
	def direction
		return ascending ? "asc" : "desc"
	end
end
