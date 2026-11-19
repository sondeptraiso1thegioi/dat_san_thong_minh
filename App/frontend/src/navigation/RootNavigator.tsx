import React, { useEffect } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import LoginScreen from '../screens/Auth/LoginScreen';
import AccountScreen from '../screens/Auth/AccountScreen';
import RegisterScreen from '../screens/Auth/RegisterScreen';
import VenueDetailScreen from '../screens/Player/VenueDetailScreen';
import BookingHoldScreen from '../screens/Player/BookingHoldScreen';
import PlayerTabs from './PlayerTabs';
import OwnerTabs from './OwnerTabs';
import AdminTabs from './AdminTabs';
import AdminVenueReviewScreen from '../screens/Admin/AdminVenueReviewScreen';
import OwnerVenueEditorScreen from '../screens/Owner/OwnerVenueEditorScreen';
import { useAuthStore } from '../store/useAuthStore';
import { colors } from '../constants/theme';
import Brand from '../components/Brand';
import { roleLabels } from '../types/auth';
import { RootRoutes } from './types';

const Stack = createNativeStackNavigator<RootRoutes>();
const routeNames = { player: 'Player', owner: 'Owner', admin: 'Admin' } as const;

export default function RootNavigator() {
  const { session, status, initialize, signOut, initializationError } = useAuthStore();
  useEffect(() => { void initialize(); }, [initialize]);
  useEffect(() => {
    if (!session) return;
    const timer = setTimeout(() => { void signOut(); }, Math.max(0, Date.parse(session.expiresAt) - Date.now()));
    return () => clearTimeout(timer);
  }, [session, signOut]);

  if (status === 'loading' || status === 'unavailable') {
    return <View style={styles.loading}>
      <Brand />
      {status === 'loading' ? <><ActivityIndicator color={colors.green} style={styles.spinner} /><Text style={styles.message}>Đang mở Sun Sport…</Text></> : <>
        <Text accessibilityRole="alert" style={styles.message}>{initializationError}</Text>
        <Pressable accessibilityRole="button" style={styles.button} onPress={() => void initialize()}><Text style={styles.buttonText}>Thử lại</Text></Pressable>
        <Pressable accessibilityRole="button" style={styles.link} onPress={() => void signOut()}><Text style={styles.message}>Về màn hình đăng nhập</Text></Pressable>
      </>}
    </View>;
  }

  return <NavigationContainer>
    <Stack.Navigator screenOptions={{ headerShown: false, animation: 'fade' }}>
      {session?.user.role === 'player' ? <>
          <Stack.Screen name="Player" component={PlayerTabs} navigationKey="player" />
          <Stack.Screen name="VenueDetail" component={VenueDetailScreen} options={({ route }) => ({ title: `${route.params.name} · Sun Sport` })} />
          <Stack.Screen name="BookingHold" component={BookingHoldScreen} options={{ title: 'Giữ chỗ · Sun Sport' }} />
        </>
        : session?.user.role === 'owner' ? <>
          <Stack.Screen name="Owner" component={OwnerTabs} navigationKey="owner" />
          <Stack.Screen name="OwnerVenueEditor" component={OwnerVenueEditorScreen} options={({ route }) => ({ title: `${route.params?.venueId ? 'Sửa' : 'Đăng ký'} cụm sân · Sun Sport` })} />
        </>
        : session?.user.role === 'admin' ? <>
          <Stack.Screen name="Admin" component={AdminTabs} navigationKey="admin" />
          <Stack.Screen name="AdminVenueReview" component={AdminVenueReviewScreen} options={{ title: 'Xem xét cụm sân · Sun Sport' }} />
        </>
        : session ? <Stack.Screen name={routeNames[session.user.role]} component={AccountScreen} navigationKey={session.user.role} options={{ title: `${roleLabels[session.user.role]} · Sun Sport` }} />
        : <>
          <Stack.Screen name="Login" component={LoginScreen} options={{ title: 'Đăng nhập · Sun Sport' }} />
          <Stack.Screen name="Register" component={RegisterScreen} options={{ title: 'Đăng ký · Sun Sport' }} />
        </>}
    </Stack.Navigator>
  </NavigationContainer>;
}

const styles = StyleSheet.create({
  loading: { flex: 1, backgroundColor: colors.background, alignItems: 'center', justifyContent: 'center', padding: 30 },
  spinner: { marginTop: 30 },
  message: { color: colors.muted, fontSize: 14, lineHeight: 23, textAlign: 'center', marginTop: 18 },
  button: { backgroundColor: colors.green, borderRadius: 10, padding: 15, marginTop: 23 },
  buttonText: { color: colors.white, fontWeight: '600' },
  link: { padding: 8 },
});
