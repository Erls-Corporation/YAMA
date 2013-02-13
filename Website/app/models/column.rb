class Column < ActiveRecord::Base
	belongs_to :list_config
	has_many :column_sorts
end
