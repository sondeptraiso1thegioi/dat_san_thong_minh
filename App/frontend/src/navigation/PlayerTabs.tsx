import React from 'react';
import { useWindowDimensions } from 'react-native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import AppTabBar, { TabConfig, WIDE_LAYOUT } from '../components/AppTabBar';
import HomeScreen from '../screens/Player/HomeScreen';
import SearchScreen from '../screens/Player/SearchScreen';
import { comingSoon } from '../screens/Player/ComingSoonScreen';
import AccountScreen from '../screens/Auth/AccountScreen';
import { PlayerTabRoutes } from './types';

const Tab = createBottomTabNavigator<PlayerTabRoutes>();

const tabs: TabConfig = {
  Home: { label: 'Trang chủ', icon: 'home-outline', activeIcon: 'home' },
  Search: { label: 'Tìm sân', icon: 'search-outline', activeIcon: 'search' },
  Suggest: { label: 'Gợi ý AI', icon: 'sparkles-outline', activeIcon: 'sparkles' },
  Bookings: { label: 'Lịch đặt', icon: 'calendar-outline', activeIcon: 'calendar' },
  Account: { label: 'Tài khoản', icon: 'person-circle-outline', activeIcon: 'person-circle' },
};

const SuggestScreen = comingSoon('Gợi ý cho bạn', 'sparkles-outline', 'Nhập nhu cầu bằng lời, nhận 5 gợi ý sân và giờ chơi kèm lý do và thời tiết.');
const BookingsScreen = comingSoon('Lịch đặt của tôi', 'calendar-outline', 'Xem trận sắp tới, đã chơi, đã hủy; thanh toán tiếp hoặc hủy sân.');

export default function PlayerTabs() {
  const { width } = useWindowDimensions();
  const wide = width >= WIDE_LAYOUT;
  return (
    <Tab.Navigator
      tabBar={props => <AppTabBar {...props} wide={wide} tabs={tabs} />}
      screenOptions={{ headerShown: false, tabBarPosition: wide ? 'left' : 'bottom' }}
    >
      <Tab.Screen name="Home" component={HomeScreen} options={{ title: 'Trang chủ · Sun Sport' }} />
      <Tab.Screen name="Search" component={SearchScreen} options={{ title: 'Tìm sân · Sun Sport' }} />
      <Tab.Screen name="Suggest" component={SuggestScreen} options={{ title: 'Gợi ý AI · Sun Sport' }} />
      <Tab.Screen name="Bookings" component={BookingsScreen} options={{ title: 'Lịch đặt · Sun Sport' }} />
      <Tab.Screen name="Account" component={AccountScreen} options={{ title: 'Tài khoản · Sun Sport' }} />
    </Tab.Navigator>
  );
}
