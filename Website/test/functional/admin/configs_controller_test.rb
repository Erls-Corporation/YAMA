require 'test_helper'

class Admin::ConfigsControllerTest < ActionController::TestCase
  setup do
    @admin_config = admin_configs(:one)
  end

  test "should get index" do
    get :index
    assert_response :success
    assert_not_nil assigns(:admin_configs)
  end

  test "should get new" do
    get :new
    assert_response :success
  end

  test "should create admin_config" do
    assert_difference('Admin::Config.count') do
      post :create, :admin_config => @admin_config.attributes
    end

    assert_redirected_to admin_config_path(assigns(:admin_config))
  end

  test "should show admin_config" do
    get :show, :id => @admin_config.to_param
    assert_response :success
  end

  test "should get edit" do
    get :edit, :id => @admin_config.to_param
    assert_response :success
  end

  test "should update admin_config" do
    put :update, :id => @admin_config.to_param, :admin_config => @admin_config.attributes
    assert_redirected_to admin_config_path(assigns(:admin_config))
  end

  test "should destroy admin_config" do
    assert_difference('Admin::Config.count', -1) do
      delete :destroy, :id => @admin_config.to_param
    end

    assert_redirected_to admin_configs_path
  end
end
