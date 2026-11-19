import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { BottomTabBarProps } from '@react-navigation/bottom-tabs';
import Brand from './Brand';
import { colors } from '../constants/theme';
import { roleLabels } from '../types/auth';
import { useAuthStore } from '../store/useAuthStore';

type IconName = React.ComponentProps<typeof Ionicons>['name'];
export type TabConfig = Record<string, { label: string; icon: IconName; activeIcon: IconName }>;

export const WIDE_LAYOUT = 960;

/** Một thanh điều hướng cho mọi vai trò: thanh dưới trên điện thoại, thanh bên trái trên web rộng. */
export default function AppTabBar({ state, navigation, wide, tabs }: BottomTabBarProps & { wide: boolean; tabs: TabConfig }) {
  const insets = useSafeAreaInsets();
  const user = useAuthStore(s => s.session?.user);
  const items = state.routes.map((route, index) => {
    const focused = state.index === index;
    const tab = tabs[route.name];
    if (!tab) return null;
    const onPress = () => {
      const event = navigation.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
      if (!focused && !event.defaultPrevented) navigation.navigate(route.name, route.params);
    };
    return (
      <Pressable
        key={route.key}
        accessibilityRole="tab"
        accessibilityState={{ selected: focused }}
        accessibilityLabel={tab.label}
        onPress={onPress}
        style={({ pressed }) => [wide ? styles.sideItem : styles.bottomItem, wide && focused && styles.sideItemActive, pressed && styles.pressed]}
      >
        <Ionicons name={focused ? tab.activeIcon : tab.icon} size={wide ? 20 : 23} color={focused ? colors.green : '#8C9388'} />
        <Text style={[wide ? styles.sideLabel : styles.bottomLabel, focused && styles.labelActive]}>{tab.label}</Text>
      </Pressable>
    );
  });

  if (wide) {
    return (
      <View style={styles.side}>
        <View style={styles.sideBrand}><Brand /></View>
        <View style={styles.sideItems}>{items}</View>
        {user && (
          <View style={styles.sideUser}>
            <Text style={styles.sideRole}>{roleLabels[user.role]}</Text>
            <Text style={styles.sideUserName} numberOfLines={1}>{user.name}</Text>
            <Text style={styles.sideUserEmail} numberOfLines={1}>{user.email}</Text>
          </View>
        )}
      </View>
    );
  }
  return <View style={[styles.bottom, { paddingBottom: Math.max(insets.bottom, 8) }]}>{items}</View>;
}

const styles = StyleSheet.create({
  pressed: { opacity: 0.7 },
  bottom: { flexDirection: 'row', backgroundColor: colors.white, borderTopWidth: 1, borderColor: colors.line, paddingTop: 8 },
  bottomItem: { flex: 1, alignItems: 'center', gap: 3, paddingVertical: 2 },
  bottomLabel: { color: '#8C9388', fontSize: 10, fontWeight: '600' },
  labelActive: { color: colors.green },
  side: { width: 248, backgroundColor: colors.white, borderRightWidth: 1, borderColor: colors.line, paddingHorizontal: 18, paddingVertical: 28 },
  sideBrand: { paddingHorizontal: 6, marginBottom: 34 },
  sideItems: { gap: 4, flex: 1 },
  sideItem: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 14, paddingVertical: 12, borderRadius: 11 },
  sideItemActive: { backgroundColor: colors.greenLight },
  sideLabel: { color: '#5F675C', fontSize: 14, fontWeight: '600' },
  sideUser: { borderTopWidth: 1, borderColor: colors.line, paddingTop: 16, paddingHorizontal: 6 },
  sideRole: { color: colors.green, fontSize: 10, fontWeight: '700', letterSpacing: 0.8, marginBottom: 3 },
  sideUserName: { color: colors.ink, fontSize: 13, fontWeight: '700' },
  sideUserEmail: { color: colors.muted, fontSize: 11, marginTop: 2 },
});
