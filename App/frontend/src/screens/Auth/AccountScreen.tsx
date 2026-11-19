import React, { useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import Brand from '../../components/Brand';
import { colors } from '../../constants/theme';
import { useAuthStore } from '../../store/useAuthStore';
import { roleLabels, UserRole } from '../../types/auth';

const descriptions: Record<UserRole, string> = {
  player: 'Không gian của người chơi',
  owner: 'Không gian quản lý của chủ sân',
  admin: 'Không gian quản trị hệ thống',
};
const icons: Record<UserRole, React.ComponentProps<typeof Ionicons>['name']> = {
  player: 'tennisball-outline', owner: 'business-outline', admin: 'shield-checkmark-outline',
};

export default function AccountScreen() {
  const session = useAuthStore(state => state.session);
  const signOut = useAuthStore(state => state.signOut);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  if (!session) return null;
  const { user } = session;
  async function handleSignOut() {
    setBusy(true);
    try { await signOut(); }
    catch { setError('Chưa đăng xuất được. Vui lòng thử lại.'); setBusy(false); }
  }
  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView contentContainerStyle={styles.page}>
        <Brand />
        <View style={styles.card}>
          <View style={styles.icon}><Ionicons name={icons[user.role]} size={36} color={colors.green} /></View>
          <Text style={styles.role}>{roleLabels[user.role]}</Text>
          <Text accessibilityRole="header" style={styles.title}>Xin chào, {user.name}.</Text>
          <Text style={styles.description}>{descriptions[user.role]}</Text>
          <View style={styles.success}><Ionicons name="checkmark-circle" size={19} color={colors.green} /><Text style={styles.successText}>Đăng nhập thành công</Text></View>
          <View style={styles.profile}><Text style={styles.label}>EMAIL</Text><Text selectable style={styles.value}>{user.email}</Text><Text style={[styles.label, { marginTop: 21 }]}>VAI TRÒ</Text><Text style={styles.value}>{roleLabels[user.role]}</Text></View>
          {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
          <Pressable accessibilityRole="button" disabled={busy} onPress={() => void handleSignOut()} style={styles.button}>
            {busy ? <ActivityIndicator color={colors.green} /> : <><Ionicons name="log-out-outline" size={19} color={colors.green} /><Text style={styles.buttonText}>Đăng xuất</Text></>}
          </Pressable>
        </View>
        <Text style={styles.footer}>SUN SPORT · CÙNG NHAU RA SÂN</Text>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  page: { flexGrow: 1, padding: 28, maxWidth: 1000, width: '100%', alignSelf: 'center' },
  card: { maxWidth: 510, width: '100%', alignSelf: 'center', marginVertical: 55, backgroundColor: colors.white, padding: 30, borderRadius: 22, borderWidth: 1, borderColor: colors.line },
  icon: { width: 70, height: 70, alignItems: 'center', justifyContent: 'center', borderRadius: 21, backgroundColor: colors.greenLight, marginBottom: 23 },
  role: { color: colors.green, fontSize: 12, fontWeight: '700', marginBottom: 9 },
  title: { fontSize: 27, fontWeight: '700', color: colors.ink, lineHeight: 36 },
  description: { fontSize: 14, color: colors.muted, marginTop: 10, lineHeight: 23 },
  success: { flexDirection: 'row', alignItems: 'center', gap: 8, marginVertical: 26 },
  successText: { color: colors.green, fontSize: 13, fontWeight: '500' },
  profile: { padding: 22, backgroundColor: '#F7F8F2', borderRadius: 12 },
  label: { color: colors.muted, fontSize: 9, letterSpacing: 1.5, fontWeight: '600' },
  value: { color: colors.ink, fontSize: 15, marginTop: 7 },
  button: { padding: 15, flexDirection: 'row', gap: 9, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: colors.line, borderRadius: 10, marginTop: 26 },
  buttonText: { color: colors.green, fontSize: 14, fontWeight: '600' },
  error: { color: colors.error, marginTop: 15 },
  footer: { textAlign: 'center', color: colors.muted, fontSize: 10, letterSpacing: 1.5, marginBottom: 15 },
});
