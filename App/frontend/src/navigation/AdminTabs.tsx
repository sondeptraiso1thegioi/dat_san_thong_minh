import React from 'react';
import { useWindowDimensions } from 'react-native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import AppTabBar, { TabConfig, WIDE_LAYOUT } from '../components/AppTabBar';
import AdminVenuesScreen from '../screens/Admin/AdminVenuesScreen';
import { comingSoon } from '../screens/Player/ComingSoonScreen';
import AccountScreen from '../screens/Auth/AccountScreen';
import { AdminTabRoutes } from './types';

const Tab = createBottomTabNavigator<AdminTabRoutes>();

const tabs: TabConfig = {
  AdminVenues: { label: 'Duyệt sân', icon: 'shield-checkmark-outline', activeIcon: 'shield-checkmark' },
  Stats: { label: 'Thống kê', icon: 'bar-chart-outline', activeIcon: 'bar-chart' },
  Settings: { label: 'Cấu hình', icon: 'settings-outline', activeIcon: 'settings' },
  AdminAccount: { label: 'Tài khoản', icon: 'person-circle-outline', activeIcon: 'person-circle' },
};

const StatsScreen = comingSoon('Thống kê', 'bar-chart-outline', 'Lưu lượng đặt sân theo ngày, giờ, khu vực (F13).');
const SettingsScreen = comingSoon('Cấu hình hệ thống', 'settings-outline', 'Gói VIP, tỷ lệ hoa hồng, đối soát tiền trả chủ sân, trọng số module AI (F14) và khóa tài khoản (F15).');

export default function AdminTabs() {
  const { width } = useWindowDimensions();
  const wide = width >= WIDE_LAYOUT;
  return (
    <Tab.Navigator
      tabBar={props => <AppTabBar {...props} wide={wide} tabs={tabs} />}
      screenOptions={{ headerShown: false, tabBarPosition: wide ? 'left' : 'bottom' }}
    >
      <Tab.Screen name="AdminVenues" component={AdminVenuesScreen} options={{ title: 'Duyệt sân · Sun Sport' }} />
      <Tab.Screen name="Stats" component={StatsScreen} options={{ title: 'Thống kê · Sun Sport' }} />
      <Tab.Screen name="Settings" component={SettingsScreen} options={{ title: 'Cấu hình · Sun Sport' }} />
      <Tab.Screen name="AdminAccount" component={AccountScreen} options={{ title: 'Tài khoản · Sun Sport' }} />
    </Tab.Navigator>
  );
}
