require 'base'
class Donation < ActiveRecord::Base
	include Base
	
	# associations
	belongs_to :artist
	belongs_to :user
	
	def self.pending_artists
		Artist.select("artists.id, artists.name, artists.picture, sum(donations.amount) AS donations_sum").
		where("donations.status = 'pending' AND donations.created_at < ?", Donation.revoke_time.ago).
		joins(:donations).
		group("artists.id").
		order("donations_sum DESC")
	end
	
	def self.pending_artists_count
		self.select("distinct(artist_id)").
		where("donations.status = 'pending' AND donations.created_at < ?", Donation.revoke_time.ago).
		count
	end
	
	def self.artists
		Artist.select("artists.id, artists.name, artists.picture, sum(donations.amount) AS donations_sum").
		joins(:donations).
		group("artists.id").
		order("donations_sum DESC")
	end
	
	def self.pending_charity
		self.where("status = 'pending' AND created_at < ?", Donation.revoke_time.ago)
	end
	
	def self.pending_charity_sum
		pending_charity.sum("amount * (charity_percentage / 100)").to_f.round(2)
	end
	
	def self.revoke_time
		14.days
	end
	
	def revoke_time
		((created_at + Donation.revoke_time - Time.now) / 86400).ceil
	end
	
	def currency
		"USD"
	end
	
	def revokable?
		status == "pending" && created_at >= Donation.revoke_time.ago
	end
	
	def charity
		amount * (charity_percentage.to_f / 100)
	end
	
	def stoffi
		amount * (stoffi_percentage.to_f / 100)
	end
	
	def artist_share
		amount * (artist_percentage.to_f / 100)
	end
	
	def display
		""
	end
end
