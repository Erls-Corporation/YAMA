class KeyboardShortcutProfile < ActiveRecord::Base
	has_many :keyboard_shortcuts
	belongs_to :configuration
end
