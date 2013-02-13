require 'test_helper'

class YoutubeControllerTest < ActionController::TestCase
  test "should get player" do
    get :player
    assert_response :success
  end

  test "should get search" do
    get :search
    assert_response :success
  end

end
