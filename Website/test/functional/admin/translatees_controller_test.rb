require 'test_helper'

class Admin::TranslateesControllerTest < ActionController::TestCase
  setup do
    @admin_translatee = admin_translatees(:one)
  end

  test "should get index" do
    get :index
    assert_response :success
    assert_not_nil assigns(:admin_translatees)
  end

  test "should get new" do
    get :new
    assert_response :success
  end

  test "should create admin_translatee" do
    assert_difference('Admin::Translatee.count') do
      post :create, :admin_translatee => @admin_translatee.attributes
    end

    assert_redirected_to admin_translatee_path(assigns(:admin_translatee))
  end

  test "should show admin_translatee" do
    get :show, :id => @admin_translatee.to_param
    assert_response :success
  end

  test "should get edit" do
    get :edit, :id => @admin_translatee.to_param
    assert_response :success
  end

  test "should update admin_translatee" do
    put :update, :id => @admin_translatee.to_param, :admin_translatee => @admin_translatee.attributes
    assert_redirected_to admin_translatee_path(assigns(:admin_translatee))
  end

  test "should destroy admin_translatee" do
    assert_difference('Admin::Translatee.count', -1) do
      delete :destroy, :id => @admin_translatee.to_param
    end

    assert_redirected_to admin_translatees_path
  end
end
