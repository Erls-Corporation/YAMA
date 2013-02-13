require 'base'
class Device < ActiveRecord::Base
	include Base
	
	# associations
	belongs_to :configuration
	belongs_to :app, :foreign_key => :client_application_id, :class_name => "ClientApplication"
	belongs_to :user
	
	attr_accessible :name, :version, :configuration_id
	
	def online?
		status == "online"
	end
	
	def display
		name
	end
	
	def poke(app, ip)
		client_application_id = app.id if app
		last_ip = ip if ip
		save
	end
	
	def serialize_options
		{
			:except => :last_ip,
			:methods => [ :kind, :display, :url ]
		}
	end
	
	def self.search(search, limit = 5)
		if search
			self.where("name LIKE ?", "%#{search}%").
			limit(limit)
		else
			scoped
		end
	end
end
