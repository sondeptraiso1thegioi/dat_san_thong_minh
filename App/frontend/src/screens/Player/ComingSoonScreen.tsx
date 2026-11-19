import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../../constants/theme';

type IconName = React.ComponentProps<typeof Ionicons>['name'];

/** Màn tạm cho các tab chưa xây dựng, để khung điều hướng đầy đủ ngay từ bước đầu. */
export function comingSoon(title: string, icon: IconName, description: string) {
  return function ComingSoonScreen() {
    return (
      <SafeAreaView style={styles.safe}>
        <View style={styles.card}>
          <View style={styles.icon}><Ionicons name={icon} size={28} color={colors.green} /></View>
          <Text accessibilityRole="header" style={styles.title}>{title}</Text>
          <Text style={styles.text}>{description}</Text>
          <View style={styles.badge}><Text style={styles.badgeText}>Đang xây dựng</Text></View>
        </View>
      </SafeAreaView>
    );
  };
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background, alignItems: 'center', justifyContent: 'center', padding: 24 },
  card: { maxWidth: 420, width: '100%', alignItems: 'center', backgroundColor: colors.white, borderRadius: 20, borderWidth: 1, borderColor: '#E8EAE1', padding: 28 },
  icon: { width: 60, height: 60, borderRadius: 18, backgroundColor: colors.greenLight, alignItems: 'center', justifyContent: 'center', marginBottom: 18 },
  title: { color: colors.ink, fontSize: 20, fontWeight: '800' },
  text: { color: colors.muted, fontSize: 13, lineHeight: 20, textAlign: 'center', marginTop: 8 },
  badge: { marginTop: 18, backgroundColor: '#FFF4D6', borderRadius: 6, paddingHorizontal: 9, paddingVertical: 4 },
  badgeText: { color: '#7A5A10', fontSize: 11, fontWeight: '600' },
});
