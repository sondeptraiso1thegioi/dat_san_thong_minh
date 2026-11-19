import React, { useRef, useState } from 'react';
import { ActivityIndicator, Keyboard, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import Brand from '../../components/Brand';
import CourtIllustration from '../../components/CourtIllustration';
import AuthField from '../../components/AuthField';
import { colors } from '../../constants/theme';
import { useAuthStore } from '../../store/useAuthStore';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { RootRoutes } from '../../navigation/types';

type Props = NativeStackScreenProps<RootRoutes, 'Login'>;

export default function LoginScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const desktop = width >= 960;
  const passwordInput = useRef<TextInput>(null);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const signIn = useAuthStore(state => state.signIn);
  const inFlight = useRef(false);

  const emailError = submitted && (!email.trim() ? 'Vui lòng nhập email.'
    : !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) ? 'Email không đúng định dạng.' : '');
  const passwordError = submitted && !password ? 'Vui lòng nhập mật khẩu.' : '';

  async function handleLogin() {
    if (inFlight.current) return;
    setSubmitted(true);
    setServerError(null);
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) || !password) return;
    Keyboard.dismiss();
    inFlight.current = true;
    setSubmitting(true);
    try {
      await signIn(email, password);
    } catch (error) {
      setServerError(error instanceof Error ? error.message : 'Đăng nhập chưa thành công. Vui lòng thử lại.');
    } finally {
      inFlight.current = false;
      setSubmitting(false);
    }
  }

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView style={styles.safe} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.scroll}>
          <View style={[styles.page, !desktop && styles.pageMobile]}>
            <View style={styles.header}>
              <Brand />
              {desktop && <Text style={styles.headerNote}>Một điểm hẹn. Nhiều niềm vui.</Text>}
            </View>

            <View style={[styles.main, !desktop && styles.mainMobile]}>
              {desktop && (
                <View style={styles.hero}>
                  <View style={styles.eyebrow}><View style={styles.dot} /><Text style={styles.eyebrowText}>DÀNH THỜI GIAN CHO ĐAM MÊ</Text></View>
                  <Text style={styles.headline}>Hẹn nhau{ '\n' }<Text style={styles.headlineAccent}>ra sân.</Text></Text>
                  <Text style={styles.heroText}>Sân phù hợp, đúng thời điểm.{ '\n' }Thêm một trận hay, thêm một ngày vui.</Text>
                  <View style={styles.illustration}><CourtIllustration /></View>
                  <View style={styles.sportRow}>
                    <Text style={styles.sport}>TENNIS</Text><Text style={styles.sportDot}>·</Text>
                    <Text style={styles.sport}>PICKLEBALL</Text><Text style={styles.sportDot}>·</Text>
                    <Text style={styles.sport}>CẦU LÔNG</Text><Text style={styles.sportDot}>·</Text>
                    <Text style={styles.sport}>BÓNG BÀN</Text>
                  </View>
                </View>
              )}

              <View style={[styles.formColumn, !desktop && styles.formColumnMobile]}>
                <View style={[styles.card, !desktop && styles.cardMobile]}>
                  <View style={styles.welcomeIcon}><Ionicons name="tennisball-outline" size={26} color={colors.green} /></View>
                  <Text style={styles.kicker}>BẮT ĐẦU MỘT TRẬN HAY</Text>
                  <Text accessibilityRole="header" style={styles.title}>Chào mừng trở lại.</Text>
                  <Text style={styles.subtitle}>Đăng nhập để tiếp tục cùng Sun Sport.</Text>
                  <View style={styles.actors}>
                    <View style={styles.actor}><Ionicons name="person-outline" size={13} color={colors.green} /><Text style={styles.actorText}>Người chơi</Text></View>
                    <View style={styles.actor}><Ionicons name="business-outline" size={13} color={colors.green} /><Text style={styles.actorText}>Chủ sân</Text></View>
                    <View style={styles.actor}><Ionicons name="shield-checkmark-outline" size={13} color={colors.green} /><Text style={styles.actorText}>Quản trị viên</Text></View>
                  </View>

                  <AuthField label="Email" icon="mail-outline" placeholder="Nhập địa chỉ email của bạn" keyboardType="email-address" autoComplete="email" textContentType="username" value={email} maxLength={150} editable={!submitting} onChangeText={value => { setEmail(value); setServerError(null); }} returnKeyType="next" onSubmitEditing={() => passwordInput.current?.focus()} error={emailError || undefined} />
                  <AuthField ref={passwordInput} label="Mật khẩu" icon="lock-closed-outline" placeholder="Nhập mật khẩu" autoComplete="current-password" textContentType="password" value={password} maxLength={128} editable={!submitting} onChangeText={value => { setPassword(value); setServerError(null); }} returnKeyType="go" onSubmitEditing={() => void handleLogin()} password error={passwordError || undefined} />

                  {!!serverError && <View accessibilityRole="alert" style={styles.serverError}><Ionicons name="alert-circle-outline" size={19} color={colors.error} /><Text style={styles.serverErrorText}>{serverError}</Text></View>}

                  <Pressable accessibilityRole="button" accessibilityLabel="Đăng nhập" accessibilityState={{ disabled: submitting, busy: submitting }} disabled={submitting} onPress={() => void handleLogin()} style={({ pressed }) => [styles.loginButton, pressed && styles.buttonPressed, submitting && styles.buttonLoading]}>
                    {submitting ? <ActivityIndicator color={colors.white} /> : <><Text style={styles.buttonText}>Đăng nhập</Text><Ionicons name="arrow-forward" size={19} color={colors.white} /></>}
                  </Pressable>
                  <View style={styles.accountNote}><Ionicons name="checkmark-circle-outline" size={16} color={colors.green} /><Text style={styles.accountNoteText}>Tự động vào không gian dành cho tài khoản của bạn.</Text></View>
                  <View style={styles.divider}><View style={styles.dividerLine} /><Text style={styles.dividerText}>hoặc</Text><View style={styles.dividerLine} /></View>
                  <Pressable accessibilityRole="button" accessibilityLabel="Tạo tài khoản Sun Sport" disabled={submitting} onPress={() => navigation.navigate('Register')} style={({ pressed }) => [styles.registerButton, pressed && styles.registerPressed]}>
                    <Ionicons name="person-add-outline" size={18} color={colors.green} /><Text style={styles.registerText}>Tạo tài khoản Sun Sport</Text>
                  </Pressable>
                  <View style={styles.cardFooter}><Text style={styles.cardFooterText}>Mỗi cuộc hẹn trên sân đều bắt đầu từ đây.</Text></View>
                </View>
              </View>
            </View>

            <View style={[styles.footer, !desktop && styles.footerMobile]}>
              <Text style={styles.footerText}>© {new Date().getFullYear()} Sun Sport</Text>
              <View style={styles.footerRight}><View style={styles.tinySun} /><Text style={styles.footerText}>CHƠI HẾT MÌNH. VUI MỖI NGÀY.</Text></View>
            </View>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { flexGrow: 1 },
  page: { flex: 1, width: '100%', maxWidth: 1420, alignSelf: 'center', paddingHorizontal: 64, paddingTop: 33 },
  pageMobile: { paddingHorizontal: 22, paddingTop: 24 },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  headerNote: { fontSize: 12, color: colors.muted },
  main: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 50, paddingVertical: 44, minHeight: 650 },
  mainMobile: { flexDirection: 'column', gap: 0, minHeight: 0, paddingTop: 35, paddingBottom: 30, justifyContent: 'center' },
  hero: { flex: 1.15, paddingTop: 10 },
  eyebrow: { flexDirection: 'row', gap: 9, alignItems: 'center', marginBottom: 19 },
  dot: { backgroundColor: colors.green, width: 6, height: 6, borderRadius: 3 },
  eyebrowText: { color: colors.green, fontSize: 10, fontWeight: '700', letterSpacing: 1.8 },
  headline: { fontSize: 72, lineHeight: 80, fontWeight: '800', letterSpacing: -3.2, color: colors.ink },
  headlineAccent: { color: '#6F8652' },
  heroText: { color: colors.muted, fontSize: 15, lineHeight: 25, marginTop: 17 },
  illustration: { width: '100%', height: 290, marginTop: -5, marginLeft: -20 },
  sportRow: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 10, marginTop: 6 },
  sport: { color: '#63725D', fontSize: 9, letterSpacing: 1.6, fontWeight: '600' },
  sportDot: { color: '#AAAFAA', fontSize: 12 },
  formColumn: { flex: 1, maxWidth: 475, width: '100%' },
  formColumnMobile: { maxWidth: 470, flex: 0, flexBasis: 'auto' },
  card: { backgroundColor: colors.white, paddingHorizontal: 36, paddingTop: 32, borderRadius: 23, borderWidth: 1, borderColor: '#E8EAE1', boxShadow: '0px 16px 50px rgba(40, 64, 34, 0.045)' },
  cardMobile: { paddingHorizontal: 23, paddingTop: 27, borderRadius: 19 },
  welcomeIcon: { backgroundColor: '#F0F3E9', width: 49, height: 49, borderRadius: 15, alignItems: 'center', justifyContent: 'center', marginBottom: 25 },
  kicker: { color: '#829171', fontSize: 9, fontWeight: '700', letterSpacing: 1.8, marginBottom: 10 },
  title: { fontSize: 29, fontWeight: '700', color: colors.ink, letterSpacing: -0.9 },
  subtitle: { color: colors.muted, fontSize: 13, marginTop: 10, lineHeight: 21 },
  actors: { flexDirection: 'row', gap: 9, flexWrap: 'wrap', marginTop: 22, marginBottom: 29 },
  actor: { flexDirection: 'row', gap: 5, alignItems: 'center', paddingHorizontal: 8, paddingVertical: 6, backgroundColor: '#F4F6EF', borderRadius: 6 },
  actorText: { color: colors.green, fontSize: 10, fontWeight: '500' },
  loginButton: { backgroundColor: colors.green, minHeight: 53, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 12, borderRadius: 11, marginTop: 4 },
  buttonPressed: { backgroundColor: '#1B3F2A' },
  buttonLoading: { opacity: 0.7 },
  buttonText: { color: colors.white, fontWeight: '600', fontSize: 14 },
  accountNote: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, marginVertical: 20 },
  accountNoteText: { color: colors.muted, fontSize: 10, lineHeight: 16, flexShrink: 1 },
  divider: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 18 },
  dividerLine: { flex: 1, height: 1, backgroundColor: '#ECEEE6' },
  dividerText: { color: '#A0A59D', fontSize: 11 },
  registerButton: { minHeight: 50, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, borderRadius: 11, borderWidth: 1, borderColor: '#D5DCCF', backgroundColor: '#FCFCF8', marginBottom: 24 },
  registerPressed: { backgroundColor: colors.greenLight },
  registerText: { color: colors.green, fontWeight: '600', fontSize: 14 },
  cardFooter: { borderTopWidth: 1, borderColor: '#F0F0E9', paddingVertical: 18, alignItems: 'center' },
  cardFooterText: { color: '#92998B', fontSize: 10 },
  serverError: { backgroundColor: colors.errorBackground, borderRadius: 9, padding: 12, gap: 8, flexDirection: 'row', alignItems: 'center', marginBottom: 15 },
  serverErrorText: { color: colors.error, fontSize: 12, lineHeight: 19, flex: 1 },
  footer: { borderTopWidth: 1, borderColor: colors.line, paddingVertical: 24, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: 15 },
  footerMobile: { flexDirection: 'column', gap: 11, paddingVertical: 19 },
  footerText: { color: '#8C9388', fontSize: 9, letterSpacing: 0.8 },
  footerRight: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  tinySun: { height: 6, width: 6, borderRadius: 3, backgroundColor: colors.yellow },
});
