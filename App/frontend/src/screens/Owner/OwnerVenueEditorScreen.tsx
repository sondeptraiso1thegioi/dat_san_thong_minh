import React, { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import LeafletMap from '../../components/map/LeafletMap';
import { isInVietnam } from '../../components/map/types';
import SportIcon from '../../components/SportIcon';
import { Chip, Field, MoneyInput, PlainInput, Stepper, fromMinutes, toMinutes } from '../../components/owner/FormControls';
import { ALL_DAYS, PriceBand, WEEKDAYS, WEEKEND, bandsFromPrices, dayShort, newKey, pricesFromBands, templateBands, validateBands } from '../../components/owner/priceBands';
import { colors } from '../../constants/theme';
import { amenityLabels, environmentLabels, sportLabels, sports } from '../../constants/sports';
import { RootRoutes } from '../../navigation/types';
import { ownerService } from '../../services/ownerService';
import { useLocationStore } from '../../store/useLocationStore';
import { OwnerVenueDetail, OwnerVenueInput, OwnerVenueSaveResult } from '../../types/owner';
import { CourtEnvironment, Sport } from '../../types/player';
import { formatPrice } from '../../utils/format';
import { statusInfo } from './OwnerVenuesScreen';

type Props = NativeStackScreenProps<RootRoutes, 'OwnerVenueEditor'>;

interface CourtDraft { key: string; id: string | null; sport: Sport; name: string; environment: CourtEnvironment; isActive: boolean }
interface Form {
  name: string; address: string; lat: number | null; lng: number | null;
  openTime: string; closeTime: string; amenities: string[]; description: string;
  courts: CourtDraft[]; bands: PriceBand[];
}

const steps = ['Thông tin', 'Sân con', 'Bảng giá', 'Xem lại'] as const;
const emptyForm: Form = {
  name: '', address: '', lat: null, lng: null, openTime: '06:00', closeTime: '22:00', amenities: [], description: '',
  courts: [{ key: newKey(), id: null, sport: 'badminton', name: 'Cầu lông 1', environment: 'indoor', isActive: true }], bands: [],
};

function fromDetail(v: OwnerVenueDetail): Form {
  return {
    name: v.name, address: v.address, lat: v.lat, lng: v.lng, openTime: v.openTime, closeTime: v.closeTime,
    amenities: v.amenities, description: v.description ?? '',
    courts: v.courts.map(c => ({ key: newKey(), ...c })), bands: bandsFromPrices(v.prices),
  };
}

const activeSportsOf = (form: Form) => sports.filter(s => form.courts.some(c => c.isActive && c.sport === s));

// Kiểm tra từng bước giống backend (OwnerVenueRules) để báo lỗi sớm; backend vẫn kiểm tra lại.
function validateStep(step: number, form: Form): string | null {
  if (step === 0) {
    const name = form.name.trim().replace(/\s+/g, ' ');
    if (name.length < 3 || name.length > 150) return 'Tên cụm sân từ 3 đến 150 ký tự.';
    if (form.address.trim().length < 5) return 'Nhập địa chỉ cụ thể (ít nhất 5 ký tự).';
    if (form.lat === null || form.lng === null) return 'Chạm vào bản đồ để ghim vị trí cụm sân.';
    if (!isInVietnam(form.lat, form.lng)) return 'Vị trí đã ghim nằm ngoài khu vực Sun Sport hỗ trợ. Phóng to bản đồ tới khu vực cụm sân rồi chạm lại đúng vị trí.';
    if (toMinutes(form.closeTime) <= toMinutes(form.openTime)) return 'Giờ đóng cửa phải sau giờ mở cửa.';
    if (form.description.length > 2000) return 'Mô tả tối đa 2000 ký tự.';
  }
  if (step === 1) {
    if (!form.courts.some(c => c.isActive)) return 'Cần ít nhất một sân con đang hoạt động.';
    if (form.courts.some(c => !c.name.trim())) return 'Nhập tên cho tất cả sân con.';
    const names = form.courts.map(c => c.name.trim().toLocaleLowerCase('vi'));
    const duplicate = names.find((n, i) => names.indexOf(n) !== i);
    if (duplicate) return `Tên sân con "${duplicate}" bị trùng.`;
  }
  if (step === 2) return validateBands(form.bands, activeSportsOf(form));
  return null;
}

export default function OwnerVenueEditorScreen({ navigation, route }: Props) {
  const venueId = route.params?.venueId ?? null;
  const [original, setOriginal] = useState<OwnerVenueDetail | null>(null);
  const [form, setForm] = useState<Form>(emptyForm);
  const [loading, setLoading] = useState(!!venueId);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [step, setStep] = useState(0);
  const [stepError, setStepError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState<OwnerVenueSaveResult | null>(null);

  useEffect(() => {
    if (!venueId) return;
    ownerService.getVenue(venueId)
      .then(detail => { setOriginal(detail); setForm(fromDetail(detail)); setLoading(false); })
      .catch(reason => { setLoadError(reason instanceof Error ? reason.message : 'Chưa tải được cụm sân.'); setLoading(false); });
  }, [venueId]);

  const update = (patch: Partial<Form>) => { setForm(f => ({ ...f, ...patch })); setStepError(null); setSaveError(null); };
  const readOnly = original?.status === 'deleted';
  const identityChanged = !!original && (original.name !== form.name.trim().replace(/\s+/g, ' ')
    || original.address !== form.address.trim().replace(/\s+/g, ' ') || original.lat !== form.lat || original.lng !== form.lng);

  function goTo(target: number) {
    for (let s = 0; s < target; s++) {
      const error = validateStep(s, form);
      if (error) { setStep(s); setStepError(error); return; }
    }
    setStepError(null);
    setStep(target);
  }

  async function submit() {
    for (let s = 0; s < 3; s++) {
      const error = validateStep(s, form);
      if (error) { setStep(s); setStepError(error); return; }
    }
    const active = activeSportsOf(form);
    const input: OwnerVenueInput = {
      name: form.name, address: form.address, lat: form.lat ?? 0, lng: form.lng ?? 0,
      openTime: form.openTime, closeTime: form.closeTime, amenities: form.amenities,
      description: form.description.trim() || null,
      courts: form.courts.map(({ key: _key, ...court }) => court),
      prices: pricesFromBands(form.bands, active),
    };
    setSaving(true);
    setSaveError(null);
    try {
      const result = await ownerService.saveVenue(venueId, input);
      setSaved(result);
      setOriginal(result.venue);
      setForm(fromDetail(result.venue));
    } catch (reason) {
      setSaveError(reason instanceof Error ? reason.message : 'Chưa lưu được cụm sân. Vui lòng thử lại.');
    } finally {
      setSaving(false);
    }
  }

  const title = venueId ? 'Sửa cụm sân' : 'Đăng ký cụm sân';
  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.topBar}>
        <Pressable accessibilityRole="button" accessibilityLabel="Quay lại" onPress={() => navigation.goBack()} style={styles.back} hitSlop={8}>
          <Ionicons name="arrow-back" size={20} color={colors.green} />
        </Pressable>
        <Text accessibilityRole="header" style={styles.topTitle} numberOfLines={1}>{original ? original.name : title}</Text>
      </View>

      {loading && <View style={styles.center}><ActivityIndicator color={colors.green} /></View>}
      {loadError && <View style={styles.center}><Text style={styles.errorText}>{loadError}</Text></View>}

      {!loading && !loadError && saved && (
        <View style={styles.center}>
          <View style={styles.done}>
            <View style={styles.doneIcon}><Ionicons name={saved.reviewRequired || saved.venue.status === 'pending' ? 'time' : 'checkmark'} size={30} color={colors.green} /></View>
            <Text style={styles.doneTitle}>{saved.reviewRequired ? 'Đã gửi đăng ký' : 'Đã lưu thay đổi'}</Text>
            <Text style={styles.doneText}>
              {saved.venue.status === 'pending'
                ? 'Cụm sân đang chờ quản trị viên duyệt. Sau khi được duyệt, người chơi sẽ tìm thấy và đặt được sân của bạn.'
                : 'Thay đổi đã áp dụng ngay cho người chơi.'}
            </Text>
            <Pressable accessibilityRole="button" onPress={() => navigation.goBack()} style={styles.primary}><Text style={styles.primaryText}>Về danh sách cụm sân</Text></Pressable>
            <Pressable accessibilityRole="button" onPress={() => { setSaved(null); setStep(0); }} style={styles.secondary}><Text style={styles.secondaryText}>Tiếp tục sửa</Text></Pressable>
          </View>
        </View>
      )}

      {!loading && !loadError && !saved && (
        <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
          <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
            <View style={styles.page}>
              {original && <StatusBanner venue={original} />}

              <View style={styles.steps}>
                {steps.map((label, index) => (
                  <Pressable key={label} accessibilityRole="tab" accessibilityState={{ selected: index === step }} onPress={() => goTo(index)} style={styles.stepItem}>
                    <View style={[styles.stepDot, index < step && styles.stepDone, index === step && styles.stepCurrent]}>
                      {index < step ? <Ionicons name="checkmark" size={14} color={colors.white} /> : <Text style={[styles.stepNumber, index === step && styles.stepNumberCurrent]}>{index + 1}</Text>}
                    </View>
                    <Text style={[styles.stepLabel, index === step && styles.stepLabelCurrent]} numberOfLines={1}>{label}</Text>
                  </Pressable>
                ))}
              </View>

              <View style={styles.card} pointerEvents={readOnly ? 'none' : 'auto'}>
                {step === 0 && <InfoStep form={form} update={update} />}
                {step === 1 && <CourtsStep form={form} update={update} />}
                {step === 2 && <PricesStep form={form} update={update} />}
                {step === 3 && <ReviewStep form={form} original={original} identityChanged={identityChanged} />}
              </View>

              {(stepError || saveError) && (
                <View accessibilityRole="alert" style={styles.alert}>
                  <Ionicons name="alert-circle-outline" size={18} color={colors.error} />
                  <Text style={styles.alertText}>{stepError ?? saveError}</Text>
                </View>
              )}

              {!readOnly && (
                <View style={styles.actions}>
                  {step > 0 && <Pressable accessibilityRole="button" onPress={() => { setStep(step - 1); setStepError(null); }} style={styles.secondary}><Text style={styles.secondaryText}>Quay lại</Text></Pressable>}
                  {step < 3
                    ? <Pressable accessibilityRole="button" onPress={() => goTo(step + 1)} style={[styles.primary, styles.flex]}><Text style={styles.primaryText}>Tiếp tục</Text></Pressable>
                    : (
                      <Pressable accessibilityRole="button" disabled={saving} onPress={() => void submit()} style={[styles.primary, styles.flex, saving && styles.busy]}>
                        {saving ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>{venueId ? 'Lưu thay đổi' : 'Gửi đăng ký'}</Text>}
                      </Pressable>
                    )}
                </View>
              )}
            </View>
          </ScrollView>
        </KeyboardAvoidingView>
      )}
    </SafeAreaView>
  );
}

function StatusBanner({ venue }: { venue: OwnerVenueDetail }) {
  const info = statusInfo[venue.status];
  return (
    <View style={[styles.banner, { backgroundColor: info.background }]}>
      <Ionicons name={info.icon} size={18} color={info.color} />
      <View style={styles.flex}>
        <Text style={[styles.bannerTitle, { color: info.color }]}>{info.label}</Text>
        <Text style={[styles.bannerText, { color: info.color }]}>
          {venue.status === 'rejected' && venue.rejectReason ? `Lý do: ${venue.rejectReason}. Sửa rồi bấm "Lưu thay đổi" để gửi lại.`
            : venue.status === 'deleted' && venue.rejectReason ? `${info.note} Lý do: ${venue.rejectReason}.` : info.note}
        </Text>
      </View>
    </View>
  );
}

type StepProps = { form: Form; update: (patch: Partial<Form>) => void };

function InfoStep({ form, update }: StepProps) {
  const { locate, locating, error: gpsError } = useLocationStore();
  const markers = useMemo(() => (form.lat !== null && form.lng !== null
    ? [{ id: 'pin', lat: form.lat, lng: form.lng, label: 'Vị trí sân', vip: false, selected: true }] : []), [form.lat, form.lng]);
  const open = toMinutes(form.openTime);
  const close = toMinutes(form.closeTime);
  return (
    <>
      <Text style={styles.stepTitle}>Thông tin cụm sân</Text>
      <Field label="Tên cụm sân" value={form.name} onChangeText={name => update({ name })} placeholder="Ví dụ: Sân cầu lông Xuân Thủy" maxLength={150} />
      <Field label="Địa chỉ" value={form.address} onChangeText={address => update({ address })} placeholder="Số nhà, đường, phường/quận, thành phố" maxLength={255} />

      <View style={styles.rowBetween}>
        <Text style={styles.label}>Vị trí trên bản đồ</Text>
        <Pressable accessibilityRole="button" disabled={locating} onPress={async () => { const p = await locate(); if (p) update({ lat: p.lat, lng: p.lng }); }} style={styles.linkButton}>
          {locating ? <ActivityIndicator size="small" color={colors.green} /> : <Ionicons name="navigate-outline" size={14} color={colors.green} />}
          <Text style={styles.linkText}>Dùng vị trí hiện tại</Text>
        </Pressable>
      </View>
      <LeafletMap markers={markers} onSelect={() => undefined} onMapPress={point => update({ lat: point.lat, lng: point.lng })} style={styles.map} />
      <Text style={[styles.hint, form.lat === null && styles.hintStrong, form.lat !== null && form.lng !== null && !isInVietnam(form.lat, form.lng) && styles.hintError]}>
        {gpsError ?? (form.lat !== null && form.lng !== null
          ? isInVietnam(form.lat, form.lng)
            ? `Đã ghim: ${form.lat.toFixed(5)}, ${form.lng.toFixed(5)} · chạm chỗ khác để đổi`
            : `Điểm ${form.lat.toFixed(3)}, ${form.lng.toFixed(3)} nằm ngoài khu vực hỗ trợ · phóng to và chạm lại đúng vị trí sân`
          : 'Chạm vào bản đồ đúng vị trí cổng cụm sân để người chơi chỉ đường chính xác.')}
      </Text>

      <View style={styles.twoCols}>
        <Stepper label="Giờ mở cửa" value={form.openTime} style={styles.flex}
          canDecrease={open > 0} canIncrease={open + 30 < close}
          onDecrease={() => update({ openTime: fromMinutes(open - 30) })} onIncrease={() => update({ openTime: fromMinutes(open + 30) })} />
        <Stepper label="Giờ đóng cửa" value={form.closeTime} style={styles.flex}
          canDecrease={close - 30 > open} canIncrease={close < 24 * 60}
          onDecrease={() => update({ closeTime: fromMinutes(close - 30) })} onIncrease={() => update({ closeTime: fromMinutes(close + 30) })} />
      </View>

      <Text style={[styles.label, styles.spaced]}>Tiện ích</Text>
      <View style={styles.chips}>
        {Object.entries(amenityLabels).map(([code, label]) => (
          <Chip key={code} label={label} active={form.amenities.includes(code)}
            onPress={() => update({ amenities: form.amenities.includes(code) ? form.amenities.filter(a => a !== code) : [...form.amenities, code] })} />
        ))}
      </View>

      <View style={styles.spaced} />
      <Field label="Mô tả (không bắt buộc)" value={form.description} onChangeText={description => update({ description })} multiline maxLength={2000}
        placeholder="Mặt sân, chỗ gửi xe, lưu ý khi đến sân…" hint={`${form.description.length}/2000 ký tự`} />
    </>
  );
}

function CourtsStep({ form, update }: StepProps) {
  const setCourt = (key: string, patch: Partial<CourtDraft>) => update({ courts: form.courts.map(c => (c.key === key ? { ...c, ...patch } : c)) });
  function addCourt() {
    const last = form.courts[form.courts.length - 1];
    const sport = last?.sport ?? 'badminton';
    const count = form.courts.filter(c => c.sport === sport).length + 1;
    update({ courts: [...form.courts, { key: newKey(), id: null, sport, name: `${sportLabels[sport]} ${count}`, environment: last?.environment ?? 'indoor', isActive: true }] });
  }
  return (
    <>
      <Text style={styles.stepTitle}>Sân con</Text>
      <Text style={styles.hint}>Mỗi sân con là một sân người chơi có thể đặt riêng. Sân đã lưu không đổi được môn và không xóa được (để giữ lịch sử đặt sân); muốn bỏ thì tắt "Đang hoạt động".</Text>
      {form.courts.map((court, index) => (
        <View key={court.key} style={[styles.court, !court.isActive && styles.courtOff]}>
          <View style={styles.rowBetween}>
            <Text style={styles.courtIndex}>Sân {index + 1}{court.id ? '' : ' · mới'}</Text>
            {court.id
              ? (
                <View style={styles.row}>
                  <Text style={styles.small}>Đang hoạt động</Text>
                  <Switch value={court.isActive} onValueChange={isActive => setCourt(court.key, { isActive })} trackColor={{ true: colors.green, false: '#D7DBD1' }} thumbColor={colors.white} />
                </View>
              )
              : (
                <Pressable accessibilityRole="button" accessibilityLabel={`Xóa sân ${index + 1}`} onPress={() => update({ courts: form.courts.filter(c => c.key !== court.key) })} hitSlop={8}>
                  <Ionicons name="trash-outline" size={18} color={colors.error} />
                </Pressable>
              )}
          </View>
          <PlainInput value={court.name} onChangeText={name => setCourt(court.key, { name })} maxLength={50} placeholder="Tên sân, ví dụ Cầu lông 1"
            accessibilityLabel={`Tên sân ${index + 1}`} invalid={!court.name.trim()} />
          <View style={styles.chips}>
            {sports.map(sport => (
              <Chip key={sport} label={sportLabels[sport]} active={court.sport === sport} disabled={!!court.id && court.sport !== sport}
                icon={<SportIcon sport={sport} size={14} color={court.sport === sport ? colors.white : colors.green} />}
                onPress={() => setCourt(court.key, { sport })} />
            ))}
          </View>
          <View style={[styles.chips, styles.chipsGap]}>
            {(Object.keys(environmentLabels) as CourtEnvironment[]).map(env => (
              <Chip key={env} label={environmentLabels[env]} active={court.environment === env} onPress={() => setCourt(court.key, { environment: env })} />
            ))}
          </View>
        </View>
      ))}
      <Pressable accessibilityRole="button" onPress={addCourt} style={styles.addRow}>
        <Ionicons name="add-circle-outline" size={18} color={colors.green} /><Text style={styles.linkText}>Thêm sân con</Text>
      </Pressable>
    </>
  );
}

function PricesStep({ form, update }: StepProps) {
  const active = activeSportsOf(form);
  const setBand = (key: string, patch: Partial<PriceBand>) => update({ bands: form.bands.map(b => (b.key === key ? { ...b, ...patch } : b)) });
  const open = toMinutes(form.openTime);
  const close = toMinutes(form.closeTime);
  const orphan = form.bands.filter(b => !active.includes(b.sport));
  return (
    <>
      <Text style={styles.stepTitle}>Bảng giá theo khung giờ</Text>
      <Text style={styles.hint}>Giá tính theo giờ cho từng môn. Mỗi khung giờ chọn các ngày áp dụng; cùng một ngày thì các khung không được chồng giờ. Giờ mở cửa: {form.openTime}–{form.closeTime}.</Text>
      {active.map(sport => {
        const bands = form.bands.filter(b => b.sport === sport).sort((a, b) => a.startHour - b.startHour);
        return (
          <View key={sport} style={styles.sportBlock}>
            <View style={styles.rowBetween}>
              <View style={styles.row}><SportIcon sport={sport} size={18} color={colors.green} /><Text style={styles.sportTitle}>{sportLabels[sport]}</Text></View>
              <Pressable accessibilityRole="button" onPress={() => update({ bands: [...form.bands.filter(b => b.sport !== sport), ...templateBands(sport, open, close)] })} style={styles.linkButton}>
                <Ionicons name="flash-outline" size={14} color={colors.green} /><Text style={styles.linkText}>Tạo nhanh: giờ thường + cao điểm</Text>
              </Pressable>
            </View>
            {bands.length === 0 && <Text style={styles.small}>Chưa có khung giờ nào.</Text>}
            {bands.map(band => (
              <View key={band.key} style={styles.band}>
                <View style={styles.dayRow}>
                  {[{ label: 'Cả tuần', days: ALL_DAYS }, { label: 'T2–T6', days: WEEKDAYS }, { label: 'T7–CN', days: WEEKEND }].map(preset => (
                    <Chip key={preset.label} label={preset.label} active={sameDays(band.days, preset.days)} onPress={() => setBand(band.key, { days: [...preset.days] })} />
                  ))}
                </View>
                <View style={styles.dayRow}>
                  {ALL_DAYS.map(day => (
                    <Pressable key={day} accessibilityRole="button" accessibilityState={{ selected: band.days.includes(day) }}
                      onPress={() => setBand(band.key, { days: band.days.includes(day) ? band.days.filter(d => d !== day) : [...band.days, day] })}
                      style={[styles.day, band.days.includes(day) && styles.dayOn]}>
                      <Text style={[styles.dayText, band.days.includes(day) && styles.dayTextOn]}>{dayShort[day]}</Text>
                    </Pressable>
                  ))}
                </View>
                <View style={styles.bandRow}>
                  <Stepper value={`${String(band.startHour).padStart(2, '0')}:00`} style={styles.hourStep}
                    canDecrease={band.startHour > 0} canIncrease={band.startHour + 1 < band.endHour}
                    onDecrease={() => setBand(band.key, { startHour: band.startHour - 1 })} onIncrease={() => setBand(band.key, { startHour: band.startHour + 1 })} />
                  <Text style={styles.dash}>–</Text>
                  <Stepper value={`${String(band.endHour).padStart(2, '0')}:00`} style={styles.hourStep}
                    canDecrease={band.endHour - 1 > band.startHour} canIncrease={band.endHour < 24}
                    onDecrease={() => setBand(band.key, { endHour: band.endHour - 1 })} onIncrease={() => setBand(band.key, { endHour: band.endHour + 1 })} />
                </View>
                <View style={styles.bandRow}>
                  <View style={styles.flex}>
                    <MoneyInput value={band.price} onChange={price => setBand(band.key, { price })} placeholder="Giá mỗi giờ"
                      accessibilityLabel={`Giá ${sportLabels[sport]} ${band.startHour}:00–${band.endHour}:00`} invalid={band.price === null} />
                  </View>
                  <Pressable accessibilityRole="button" accessibilityLabel="Xóa khung giờ" onPress={() => update({ bands: form.bands.filter(b => b.key !== band.key) })} style={styles.iconButton} hitSlop={6}>
                    <Ionicons name="trash-outline" size={18} color={colors.error} />
                  </Pressable>
                </View>
              </View>
            ))}
            <Pressable accessibilityRole="button" style={styles.addRow}
              onPress={() => {
                const lastEnd = Math.max(Math.ceil(open / 60), ...bands.map(b => b.endHour));
                const start = Math.min(lastEnd, 23);
                update({ bands: [...form.bands, { key: newKey(), sport, days: [...ALL_DAYS], startHour: start, endHour: Math.min(start + 2, 24), price: null }] });
              }}>
              <Ionicons name="add-circle-outline" size={18} color={colors.green} /><Text style={styles.linkText}>Thêm khung giờ</Text>
            </Pressable>
          </View>
        );
      })}
      {orphan.length > 0 && <Text style={styles.small}>Có {orphan.length} khung giờ của môn không còn sân hoạt động; chúng sẽ không được lưu.</Text>}
    </>
  );
}

function ReviewStep({ form, original, identityChanged }: { form: Form; original: OwnerVenueDetail | null; identityChanged: boolean }) {
  const active = form.courts.filter(c => c.isActive);
  const prices = pricesFromBands(form.bands, activeSportsOf(form));
  return (
    <>
      <Text style={styles.stepTitle}>Kiểm tra và gửi</Text>
      {!original && <Note icon="time-outline" text="Sau khi gửi, cụm sân ở trạng thái Chờ duyệt. Quản trị viên kiểm tra vị trí và thông tin rồi duyệt; khi đó người chơi mới tìm thấy sân." />}
      {original?.status === 'approved' && identityChanged && <Note warn icon="alert-circle-outline" text="Bạn đã đổi tên, địa chỉ hoặc vị trí. Cụm sân sẽ chuyển về Chờ duyệt và tạm ẩn với người chơi đến khi được duyệt lại." />}
      {original?.status === 'approved' && !identityChanged && <Note icon="checkmark-circle-outline" text="Thay đổi giờ mở cửa, tiện ích, sân con và bảng giá được áp dụng ngay, không cần duyệt lại. Giá mới chỉ áp dụng cho booking tạo sau khi lưu." />}
      {original?.status === 'rejected' && <Note icon="refresh-outline" text="Lưu thay đổi sẽ gửi lại cụm sân để quản trị viên duyệt." />}

      <Summary label="Tên" value={form.name.trim()} />
      <Summary label="Địa chỉ" value={form.address.trim()} />
      <Summary label="Giờ mở cửa" value={`${form.openTime}–${form.closeTime}`} />
      <Summary label="Tiện ích" value={form.amenities.map(a => amenityLabels[a] ?? a).join(', ') || 'Không có'} />
      <Summary label="Sân con hoạt động" value={active.map(c => `${c.name} (${sportLabels[c.sport]}, ${environmentLabels[c.environment].toLowerCase()})`).join('; ')} />
      {activeSportsOf(form).map(sport => {
        const rows = prices.filter(p => p.sport === sport);
        const min = Math.min(...rows.map(p => p.pricePerHour));
        const max = Math.max(...rows.map(p => p.pricePerHour));
        return <Summary key={sport} label={`Giá ${sportLabels[sport]}`} value={`${min === max ? formatPrice(min) : `${formatPrice(min)} – ${formatPrice(max)}`}/giờ · ${rows.length} dòng giá`} />;
      })}
    </>
  );
}

function Note({ icon, text, warn }: { icon: React.ComponentProps<typeof Ionicons>['name']; text: string; warn?: boolean }) {
  return (
    <View style={[styles.note, warn && styles.noteWarn]}>
      <Ionicons name={icon} size={18} color={warn ? '#7A5A10' : colors.green} />
      <Text style={[styles.noteText, warn && styles.noteWarnText]}>{text}</Text>
    </View>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return <View style={styles.summary}><Text style={styles.summaryLabel}>{label}</Text><Text style={styles.summaryValue}>{value}</Text></View>;
}

const sameDays = (a: number[], b: number[]) => a.length === b.length && b.every(d => a.includes(d));

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  flex: { flex: 1, minWidth: 0 },
  topBar: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  back: { padding: 4 },
  topTitle: { flex: 1, color: colors.ink, fontSize: 17, fontWeight: '800' },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24 },
  errorText: { color: colors.error, textAlign: 'center' },
  scroll: { padding: 18, paddingBottom: 40 },
  page: { width: '100%', maxWidth: 760, alignSelf: 'center' },
  banner: { flexDirection: 'row', gap: 10, padding: 14, borderRadius: 14, marginBottom: 16 },
  bannerTitle: { fontSize: 13, fontWeight: '800' },
  bannerText: { fontSize: 12, lineHeight: 18, marginTop: 2 },
  steps: { flexDirection: 'row', marginBottom: 14, gap: 6 },
  stepItem: { flex: 1, alignItems: 'center', gap: 6 },
  stepDot: { width: 28, height: 28, borderRadius: 14, borderWidth: 1, borderColor: '#D5DCCF', alignItems: 'center', justifyContent: 'center', backgroundColor: colors.white },
  stepDone: { backgroundColor: colors.green, borderColor: colors.green },
  stepCurrent: { borderColor: colors.green, borderWidth: 2 },
  stepNumber: { color: colors.muted, fontSize: 12, fontWeight: '700' },
  stepNumberCurrent: { color: colors.green },
  stepLabel: { color: colors.muted, fontSize: 11, fontWeight: '600' },
  stepLabelCurrent: { color: colors.green },
  card: { backgroundColor: colors.white, borderRadius: 18, borderWidth: 1, borderColor: '#E8EAE1', padding: 18 },
  stepTitle: { color: colors.ink, fontSize: 18, fontWeight: '800', marginBottom: 14 },
  label: { color: colors.ink, fontSize: 13, fontWeight: '600', marginBottom: 7 },
  spaced: { marginTop: 18 },
  hint: { color: colors.muted, fontSize: 12, lineHeight: 18, marginTop: 6, marginBottom: 12 },
  hintStrong: { color: colors.green, fontWeight: '600' },
  hintError: { color: colors.error, fontWeight: '600' },
  small: { color: colors.muted, fontSize: 12 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  rowBetween: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10, flexWrap: 'wrap' },
  linkButton: { flexDirection: 'row', alignItems: 'center', gap: 5, paddingVertical: 4 },
  linkText: { color: colors.green, fontSize: 13, fontWeight: '700' },
  map: { height: 260, borderRadius: 14, overflow: 'hidden', borderWidth: 1, borderColor: colors.line },
  twoCols: { flexDirection: 'row', gap: 12, marginTop: 6 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 7 },
  chipsGap: { marginTop: 8 },
  court: { borderWidth: 1, borderColor: colors.line, borderRadius: 14, padding: 14, marginTop: 12, gap: 10 },
  courtOff: { opacity: 0.6, backgroundColor: '#F7F7F3' },
  courtIndex: { color: colors.ink, fontWeight: '700' },
  addRow: { flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 14, paddingVertical: 6 },
  sportBlock: { borderTopWidth: 1, borderColor: '#F0F1EA', paddingTop: 16, marginTop: 8 },
  sportTitle: { color: colors.ink, fontSize: 15, fontWeight: '800' },
  band: { borderWidth: 1, borderColor: colors.line, borderRadius: 14, padding: 12, marginTop: 12, gap: 10 },
  dayRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 5 },
  day: { width: 34, height: 34, borderRadius: 8, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center', backgroundColor: '#FCFCF8' },
  dayOn: { backgroundColor: colors.greenLight, borderColor: colors.green },
  dayText: { color: colors.muted, fontSize: 12, fontWeight: '600' },
  dayTextOn: { color: colors.green },
  bandRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  hourStep: { flex: 1 },
  dash: { color: colors.muted },
  iconButton: { width: 42, height: 46, alignItems: 'center', justifyContent: 'center' },
  note: { flexDirection: 'row', gap: 10, padding: 12, borderRadius: 12, backgroundColor: colors.greenLight, marginBottom: 14 },
  noteWarn: { backgroundColor: '#FFF4D6' },
  noteText: { flex: 1, color: colors.green, fontSize: 12, lineHeight: 18 },
  noteWarnText: { color: '#7A5A10' },
  summary: { paddingVertical: 10, borderBottomWidth: 1, borderColor: '#F0F1EA' },
  summaryLabel: { color: colors.muted, fontSize: 11, fontWeight: '700', letterSpacing: 0.4 },
  summaryValue: { color: colors.ink, fontSize: 14, marginTop: 3, lineHeight: 20 },
  alert: { flexDirection: 'row', gap: 8, alignItems: 'center', backgroundColor: colors.errorBackground, borderRadius: 12, padding: 12, marginTop: 14 },
  alertText: { flex: 1, color: colors.error, fontSize: 13, lineHeight: 19 },
  actions: { flexDirection: 'row', gap: 10, marginTop: 16 },
  primary: { backgroundColor: colors.green, borderRadius: 12, paddingVertical: 15, paddingHorizontal: 20, alignItems: 'center', justifyContent: 'center' },
  primaryText: { color: colors.white, fontWeight: '700', fontSize: 14 },
  busy: { opacity: 0.7 },
  secondary: { borderRadius: 12, paddingVertical: 15, paddingHorizontal: 20, alignItems: 'center', borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  secondaryText: { color: colors.ink, fontWeight: '600', fontSize: 14 },
  done: { maxWidth: 440, width: '100%', alignItems: 'center', backgroundColor: colors.white, borderRadius: 20, borderWidth: 1, borderColor: '#E8EAE1', padding: 28, gap: 10 },
  doneIcon: { width: 64, height: 64, borderRadius: 32, backgroundColor: colors.greenLight, alignItems: 'center', justifyContent: 'center' },
  doneTitle: { color: colors.ink, fontSize: 20, fontWeight: '800' },
  doneText: { color: colors.muted, fontSize: 13, lineHeight: 20, textAlign: 'center', marginBottom: 6 },
});
