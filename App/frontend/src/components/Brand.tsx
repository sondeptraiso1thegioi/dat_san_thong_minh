import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../constants/theme';

export default function Brand() {
  return (
    <View style={styles.brand} accessibilityLabel="Sun Sport">
      <View style={styles.icon}><Ionicons name="sunny" color={colors.ink} size={24} /></View>
      <View><Text style={styles.name}>SUN SPORT</Text><Text style={styles.tag}>CÙNG NHAU RA SÂN</Text></View>
    </View>
  );
}

const styles = StyleSheet.create({
  brand: { flexDirection: 'row', alignItems: 'center', gap: 11 },
  icon: { width: 44, height: 44, backgroundColor: colors.yellow, borderRadius: 14, alignItems: 'center', justifyContent: 'center' },
  name: { fontSize: 21, fontWeight: '900', letterSpacing: 1.5, color: colors.ink },
  tag: { fontSize: 8, letterSpacing: 2.2, fontWeight: '700', color: colors.green, marginTop: 3 },
});
