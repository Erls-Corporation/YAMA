class Translation < ActiveRecord::Base
	belongs_to :translatee, :foreign_key => :translatee_id, :class_name => Admin::Translatee
	belongs_to :language
	belongs_to :user
	has_many :votes
end
