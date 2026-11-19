import React, { useRef, useState } from 'react';
import { ActivityIndicator, Keyboard, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, type TextInputInstance, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import Brand from '../../components/Brand';
import AuthField from '../../components/AuthField';
import { ApiError } from '../../api/axiosClient';
import { colors } from '../../constants/theme';
import { RootRoutes } from '../../navigation/types';
import { useAuthStore } from '../../store/useAuthStore';
import { RegisterRole } from '../../types/auth';

type Props = NativeStackScreenProps<RootRoutes, 'Register'>;
type Field = 'name' | 'phone' | 'email' | 'password' | 'confirm';
type Values = Record<Field, string>;
type IconName = React.ComponentProps<typeof Ionicons>['name'];

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const phonePattern = /^0\d{9}$/;

const roleOptions: { value: RegisterRole; title: string; description: string; icon: IconName }[] = [
  { value: 'player', title: 'Người chơi', description: 'Tìm sân gần bạn, đặt và thanh toán nhanh.', icon: 'tennisball-outline' },
  { value: 'owner', title: 'Chủ sân', description: 'Đưa cụm sân lên Sun Sport, quản lý lịch và doanh thu.', icon: 'business-outline' },
];

// Same rules as RegisterRequest on the API; the API remains the source of truth.
function validate(values: Values): Partial<Record<Field, string>> {
  const errors: Partial<Record<Field, string>> = {};
  const name = values.name.trim().replace(/\s+/g, ' ');
  if (!name) errors.name = 'Vui lòng nhập họ tên.';
  else if (name.length < 2) errors.name = 'Họ tên cần ít nhất 2 ký tự.';
  if (!values.phone) errors.phone = 'Vui lòng nhập số điện thoại.';
  else if (!phonePattern.test(values.phone)) errors.phone = 'Số điện thoại gồm 10 chữ số và bắt đầu bằng 0.';
  const email = values.email.trim();
  if (!email) errors.email = 'Vui lòng nhập email.';
  else if (!emailPattern.test(email)) errors.email = 'Email không đúng định dạng.';
  if (!values.password) errors.password = 'Vui lòng nhập mật khẩu.';
  else if (values.password.length < 6) errors.password = 'Mật khẩu cần ít nhất 6 ký tự.';
  if (!values.confirm) errors.confirm = 'Vui lòng nhập lại mật khẩu.';
  else if (values.confirm !== values.password) errors.confirm = 'Mật khẩu xác nhận không khớp.';
  return errors;
}

export default function RegisterScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const wide = width >= 720;
  const phoneInput = useRef<TextInputInstance>(null);
  const emailInput = useRef<TextInputInstance>(null);
  const passwordInput = useRef<TextInputInstance>(null);
  const confirmInput = useRef<TextInputInstance>(null);
  const inFlight = useRef(false);
  const signUp = useAuthStore(state => state.signUp);

  const [role, setRole] = useState<RegisterRole>('player');
  const [values, setValues] = useState<Values>({ name: '', phone: '', email: '', password: '', confirm: '' });
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const [takenErrors, setTakenErrors] = useState<{ email?: string; phone?: string }>({});

  const errors = submitted ? validate(values) : {};
  const errorFor = (field: Field): string | undefined =>
    errors[field] ?? (field === 'email' || field === 'phone' ? takenErrors[field] : undefined);

  function update(field: Field, value: string) {
    // Phone keeps digits only, so pasted "091 234 5678" still works.
    const next = field === 'phone' ? value.replace(/\D/g, '').slice(0, 10) : value;
    setValues(current => ({ ...current, [field]: next }));
    setServerError(null);
    if (field === 'email' || field === 'phone') setTakenErrors(current => ({ ...current, [field]: undefined }));
  }

  function goToLogin() {
    if (navigation.canGoBack()) navigation.goBack();
    else navigation.navigate('Login');
  }

  async function handleRegister() {
    if (inFlight.current) return;
    setSubmitted(true);
    setServerError(null);
    if (Object.keys(validate(values)).length > 0) return;
    Keyboard.dismiss();
    inFlight.current = true;
    setSubmitting(true);
    try {
      // On success the navigator switches straight to the new account's space.
      await signUp({ name: values.name, phone: values.phone, email: values.email, password: values.password, role });
    } catch (error) {
      if (error instanceof ApiError && error.code === 'EMAIL_TAKEN') setTakenErrors(current => ({ ...current, email: error.message }));
      else if (error instanceof ApiError && error.code === 'PHONE_TAKEN') setTakenErrors(current => ({ ...current, phone: error.message }));
      else setServerError(error instanceof Error ? error.message : 'Đăng ký chưa thành công. Vui lòng thử lại.');
    } finally {
      inFlight.current = false;
      setSubmitting(false);
    }
  }

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView style={styles.safe} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.scroll}>
          <View style={[styles.page, !wide && styles.pageMobile]}>
            <View style={styles.header}>
              <Brand />
              <Pressable accessibilityRole="button" accessibilityLabel="Quay lại đăng nhập" onPress={goToLogin} hitSlop={8} style={styles.headerLink}>
                <Ionicons name="arrow-back" size={16} color={colors.green} />
                <Text style={styles.headerLinkText}>Đăng nhập</Text>
              </Pressable>
            </View>

            <View style={[styles.card, !wide && styles.cardMobile]}>
              <View style={styles.welcomeIcon}><Ionicons name="person-add-outline" size={24} color={colors.green} /></View>
              <Text style={styles.kicker}>THAM GIA SUN SPORT</Text>
              <Text accessibilityRole="header" style={styles.title}>Tạo tài khoản mới.</Text>
              <Text style={styles.subtitle}>Chỉ mất một phút để bắt đầu đặt sân, hoặc đưa sân của bạn lên Sun Sport.</Text>

              <Text style={styles.groupLabel}>Bạn tham gia với vai trò</Text>
              <View accessibilityRole="radiogroup" style={[styles.roles, !wide && styles.rolesMobile]}>
                {roleOptions.map(option => {
                  const selected = option.value === role;
                  return (
                    <Pressable
                      key={option.value}
                      accessibilityRole="radio"
                      accessibilityLabel={option.title}
                      accessibilityState={{ checked: selected, disabled: submitting }}
                      disabled={submitting}
                      onPress={() => setRole(option.value)}
                      style={[styles.role, wide && styles.roleWide, selected && styles.roleSelected]}
                    >
                      <View style={styles.roleTop}>
                        <View style={[styles.roleIcon, selected && styles.roleIconSelected]}>
                          <Ionicons name={option.icon} size={18} color={selected ? colors.white : colors.green} />
                        </View>
                        <Ionicons name={selected ? 'radio-button-on' : 'radio-button-off'} size={20} color={selected ? colors.green : '#B5BAB0'} />
                      </View>
                      <Text style={styles.roleTitle}>{option.title}</Text>
                      <Text style={styles.roleDescription}>{option.description}</Text>
                    </Pressable>
                  );
                })}
              </View>

              <AuthField label="Họ và tên" icon="person-outline" placeholder="Ví dụ: Nguyễn Văn An" autoCapitalize="words" autoComplete="name" textContentType="name" value={values.name} maxLength={100} editable={!submitting} onChangeText={value => update('name', value)} returnKeyType="next" onSubmitEditing={() => phoneInput.current?.focus()} error={errorFor('name')} />
              <AuthField ref={phoneInput} label="Số điện thoại" icon="call-outline" placeholder="10 chữ số, ví dụ 0912345678" keyboardType="phone-pad" autoComplete="tel" textContentType="telephoneNumber" value={values.phone} maxLength={14} editable={!submitting} onChangeText={value => update('phone', value)} returnKeyType="next" onSubmitEditing={() => emailInput.current?.focus()} error={errorFor('phone')} />
              <AuthField ref={emailInput} label="Email" icon="mail-outline" placeholder="Nhập địa chỉ email của bạn" keyboardType="email-address" autoComplete="email" textContentType="emailAddress" value={values.email} maxLength={150} editable={!submitting} onChangeText={value => update('email', value)} returnKeyType="next" onSubmitEditing={() => passwordInput.current?.focus()} error={errorFor('email')} />
              <AuthField ref={passwordInput} label="Mật khẩu" icon="lock-closed-outline" placeholder="Tối thiểu 6 ký tự" autoComplete="new-password" textContentType="newPassword" value={values.password} maxLength={72} editable={!submitting} onChangeText={value => update('password', value)} returnKeyType="next" onSubmitEditing={() => confirmInput.current?.focus()} password error={errorFor('password')} />
              <AuthField ref={confirmInput} label="Xác nhận mật khẩu" icon="shield-checkmark-outline" placeholder="Nhập lại mật khẩu" autoComplete="new-password" textContentType="newPassword" value={values.confirm} maxLength={72} editable={!submitting} onChangeText={value => update('confirm', value)} returnKeyType="go" onSubmitEditing={() => void handleRegister()} password error={errorFor('confirm')} />

              {!!serverError && <View accessibilityRole="alert" style={styles.serverError}><Ionicons name="alert-circle-outline" size={19} color={colors.error} /><Text style={styles.serverErrorText}>{serverError}</Text></View>}

              <Pressable accessibilityRole="button" accessibilityLabel="Tạo tài khoản" accessibilityState={{ disabled: submitting, busy: submitting }} disabled={submitting} onPress={() => void handleRegister()} style={({ pressed }) => [styles.submit, pressed && styles.submitPressed, submitting && styles.submitLoading]}>
                {submitting ? <ActivityIndicator color={colors.white} /> : <><Text style={styles.submitText}>Tạo tài khoản</Text><Ionicons name="arrow-forward" size={19} color={colors.white} /></>}
              </Pressable>
              <Text style={styles.adminNote}>Tài khoản quản trị viên do hệ thống cấp, không đăng ký tại đây.</Text>

              <View style={styles.cardFooter}>
                <Text style={styles.cardFooterText}>Đã có tài khoản? </Text>
                <Pressable accessibilityRole="link" onPress={goToLogin} hitSlop={6}><Text style={styles.cardFooterLink}>Đăng nhập</Text></Pressable>
              </View>
            </View>

            <Text style={styles.footer}>SUN SPORT · CÙNG NHAU RA SÂN</Text>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { flexGrow: 1 },
  page: { flex: 1, width: '100%', maxWidth: 1420, alignSelf: 'center', paddingHorizontal: 64, paddingTop: 33, paddingBottom: 26 },
  pageMobile: { paddingHorizontal: 22, paddingTop: 24 },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  headerLink: { flexDirection: 'row', alignItems: 'center', gap: 6, padding: 6 },
  headerLinkText: { color: colors.green, fontSize: 13, fontWeight: '600' },
  card: { width: '100%', maxWidth: 540, alignSelf: 'center', marginVertical: 40, backgroundColor: colors.white, paddingHorizontal: 36, paddingTop: 32, borderRadius: 23, borderWidth: 1, borderColor: '#E8EAE1', boxShadow: '0px 16px 50px rgba(40, 64, 34, 0.045)' },
  cardMobile: { paddingHorizontal: 23, paddingTop: 27, borderRadius: 19, marginVertical: 28 },
  welcomeIcon: { backgroundColor: '#F0F3E9', width: 49, height: 49, borderRadius: 15, alignItems: 'center', justifyContent: 'center', marginBottom: 25 },
  kicker: { color: '#829171', fontSize: 9, fontWeight: '700', letterSpacing: 1.8, marginBottom: 10 },
  title: { fontSize: 29, fontWeight: '700', color: colors.ink, letterSpacing: -0.9 },
  subtitle: { color: colors.muted, fontSize: 13, marginTop: 10, lineHeight: 21 },
  groupLabel: { color: colors.ink, fontWeight: '600', fontSize: 13, marginTop: 26, marginBottom: 10 },
  roles: { flexDirection: 'row', gap: 12, marginBottom: 24 },
  rolesMobile: { flexDirection: 'column' },
  role: { borderWidth: 1, borderColor: colors.line, borderRadius: 13, padding: 15, backgroundColor: '#FCFCF8' },
  roleWide: { flex: 1 },
  roleSelected: { borderColor: colors.green, backgroundColor: colors.greenLight },
  roleTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 },
  roleIcon: { width: 34, height: 34, borderRadius: 10, alignItems: 'center', justifyContent: 'center', backgroundColor: '#F0F3E9' },
  roleIconSelected: { backgroundColor: colors.green },
  roleTitle: { color: colors.ink, fontSize: 14, fontWeight: '700' },
  roleDescription: { color: colors.muted, fontSize: 12, lineHeight: 18, marginTop: 4 },
  serverError: { backgroundColor: colors.errorBackground, borderRadius: 9, padding: 12, gap: 8, flexDirection: 'row', alignItems: 'center', marginBottom: 15 },
  serverErrorText: { color: colors.error, fontSize: 12, lineHeight: 19, flex: 1 },
  submit: { backgroundColor: colors.green, minHeight: 53, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 12, borderRadius: 11, marginTop: 4 },
  submitPressed: { backgroundColor: '#1B3F2A' },
  submitLoading: { opacity: 0.7 },
  submitText: { color: colors.white, fontWeight: '600', fontSize: 14 },
  adminNote: { color: colors.muted, fontSize: 10, lineHeight: 16, textAlign: 'center', marginVertical: 18 },
  cardFooter: { borderTopWidth: 1, borderColor: '#F0F0E9', paddingVertical: 18, flexDirection: 'row', justifyContent: 'center', alignItems: 'center' },
  cardFooterText: { color: '#92998B', fontSize: 12 },
  cardFooterLink: { color: colors.green, fontSize: 12, fontWeight: '700' },
  footer: { textAlign: 'center', color: colors.muted, fontSize: 10, letterSpacing: 1.5 },
});
