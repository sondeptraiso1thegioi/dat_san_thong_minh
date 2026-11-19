import React from 'react';
import { useWindowDimensions } from 'react-native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import AppTabBar, { TabConfig, WIDE_LAYOUT } from '../components/AppTabBar';
import OwnerVenuesScreen from '../screens/Owner/OwnerVenuesScreen';
import { comingSoon } from '../screens/Player/ComingSoonScreen';
import AccountScreen from '../screens/Auth/AccountScreen';
import { OwnerTabRoutes } from './types';

const Tab = createBottomTabNavigator<OwnerTabRoutes>();

const tabs: TabConfig = {
  Venues: { label: 'Cụm sân', icon: 'business-outline', activeIcon: 'business' },
  OwnerBookings: { label: 'Lịch đặt', icon: 'calendar-outline', activeIcon: 'calendar' },
  Revenue: { label: 'Doanh thu', icon: 'wallet-outline', activeIcon: 'wallet' },
  OwnerAccount: { label: 'Tài khoản', icon: 'person-circle-outline', activeIcon: 'person-circle' },
};

const BookingsScreen = comingSoon('Lịch đặt sân', 'calendar-outline', 'Xem booking theo ngày và sân con; xác nhận hoặc hủy booking (F09).');
const RevenueScreen = comingSoon('Doanh thu', 'wallet-outline', 'Doanh thu, hoa hồng nền tảng đã trừ và số tiền được nhận (F10).');

export default function OwnerTabs() {
  const { width } = useWindowDimensions();
  const wide = width >= WIDE_LAYOUT;
  return (
    <Tab.Navigator
      tabBar={props => <AppTabBar {...props} wide={wide} tabs={tabs} />}
      screenOptions={{ headerShown: false, tabBarPosition: wide ? 'left' : 'bottom' }}
    >
      <Tab.Screen name="Venues" component={OwnerVenuesScreen} options={{ title: 'Cụm sân · Sun Sport' }} />
      <Tab.Screen name="OwnerBookings" component={BookingsScreen} options={{ title: 'Lịch đặt · Sun Sport' }} />
      <Tab.Screen name="Revenue" component={RevenueScreen} options={{ title: 'Doanh thu · Sun Sport' }} />
      <Tab.Screen name="OwnerAccount" component={AccountScreen} options={{ title: 'Tài khoản · Sun Sport' }} />
    </Tab.Navigator>
  );
}
