class Admin::Translatee < ActiveRecord::Base
	has_many :translations
	has_and_belongs_to_many :parameters, :class_name => "TranslateeParam"
	
	validates_presence_of :name
	validates_presence_of :description
end