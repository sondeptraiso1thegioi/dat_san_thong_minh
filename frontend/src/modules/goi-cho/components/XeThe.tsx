import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useApp } from '@/app/AppContext';
import { kiemTraAnh } from '../utils/anhGiayDangKy';
import { ChonAnh } from './AnhGiayDangKy';
import type { XeDuocDung } from '../models';
import { dinhDangNgay, dinhDangTien, hienThiBienSo, tenLoaiXe } from '@/shared/utils/format';
import { NhanTrangThai } from './GoiChoUi';

export function XeThe({ xe, onDoi }: { xe: XeDuocDung; onDoi?: () => Promise<void> | void }) {
  const daDuyet = xe.trangThaiDuyet === 'DA_DUYET';
  const canAnh = xe.laChuXe && (xe.trangThaiDuyet === 'TU_CHOI' || (xe.trangThaiDuyet === 'CHO_DUYET' && !xe.coAnhGiayDangKy));
  return (
    <article className="the xe-the" data-dich={`xe-${xe.xeId}`}>
      <div className="xe-dau">
        <div>
          <div className="bien-so">{hienThiBienSo(xe.bienSo)}</div>
          <div className="mo">{tenLoaiXe(xe.loaiXe)}{!xe.laChuXe && ` · Xe của ${xe.tenChuXe} (chia sẻ trong nhóm)`}</div>
        </div>
        <NhanTrangThai tt={xe.trangThaiDuyet} />
      </div>

      <dl className="xe-tt">
        <div><dt>Gói hiện tại</dt><dd>{xe.goiHienTai ?? (xe.conHan ? 'Gói chưa bắt đầu' : 'Chưa có')}</dd></div>
        <div><dt>Hết hạn</dt><dd className={xe.hetHanLuc && !xe.conHan ? 'chu-loi' : ''}>{dinhDangNgay(xe.hetHanLuc)}{xe.hetHanLuc && !xe.conHan && ' (đã hết)'}</dd></div>
        <div><dt>Trong bãi</dt><dd>{xe.dangTrongBai ? 'Đang gửi' : 'Không'}</dd></div>
      </dl>

      {xe.soNgayNo > 0 && (
        <div className="thong-bao thong-bao-loi">
          Quá hạn {xe.soNgayNo} ngày, còn nợ <strong>{dinhDangTien(xe.soTienNo)}</strong>
        </div>
      )}
      {xe.trangThaiDuyet === 'CHO_DUYET' && xe.coAnhGiayDangKy && <p className="mo">Admin duyệt xong mới mua gói được.</p>}
      {xe.trangThaiDuyet === 'TU_CHOI' && (
        <div className="thong-bao thong-bao-loi">Admin từ chối duyệt. Lý do: {xe.lyDoTuChoi ?? 'không ghi'}. Tải ảnh mới để gửi lại.</div>
      )}
      {xe.trangThaiDuyet === 'CHO_DUYET' && !xe.coAnhGiayDangKy && (
        <div className="thong-bao thong-bao-canh-bao">Xe chưa có ảnh giấy đăng ký nên Admin chưa duyệt được.</div>
      )}
      {canAnh && <TaiAnhLai xeId={xe.xeId} guiLai={xe.trangThaiDuyet === 'TU_CHOI'} onXong={onDoi} />}
      {xe.goiBiHuyLyDo && xe.trangThaiDuyet !== 'BI_GO' && (
        <div className="thong-bao thong-bao-loi">
          Gói {xe.goiBiHuyTen} bị Admin huỷ ngày {dinhDangNgay(xe.goiBiHuyLuc)}. Lý do: {xe.goiBiHuyLyDo}
        </div>
      )}
      {xe.trangThaiDuyet === 'BI_GO' && (
        <div className="thong-bao thong-bao-loi">Xe bị Admin gỡ khỏi hệ thống. Lý do: {xe.lyDoGo ?? 'không ghi'}</div>
      )}

      <div className="hang-nut">
        {xe.soNgayNo > 0 && <Link className="nut nut-nguy" to={`/phi-qua-han/${xe.xeId}`}>Trả phí quá hạn</Link>}
        {daDuyet && xe.laChuXe && (
          <Link className="nut" to={`/mua-goi?xe=${xe.xeId}`}>{xe.conHan ? 'Gia hạn gói' : 'Mua gói'}</Link>
        )}
      </div>
    </article>
  );
}

/** Tải ảnh cho xe chưa có ảnh, hoặc gửi lại xe bị từ chối. */
function TaiAnhLai({ xeId, guiLai, onXong }: { xeId: number; guiLai: boolean; onXong?: () => Promise<void> | void }) {
  const { services } = useApp();
  const [anh, setAnh] = useState<File | null>(null);
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  async function gui() {
    const l = kiemTraAnh(anh); if (l) { setLoi(l); return; }
    setDangGui(true); setLoi(null);
    try { await services.xe.taiAnhGiayDangKy(xeId, anh!); await onXong?.(); }
    catch (e) { setLoi((e as Error).message); }
    finally { setDangGui(false); }
  }
  return (
    <div className="tai-anh-lai">
      <ChonAnh tep={anh} onChon={setAnh} />
      {loi && <div className="thong-bao thong-bao-loi">{loi}</div>}
      <button className="nut nut-nho" disabled={dangGui || !anh} onClick={gui}>{dangGui ? 'Đang gửi…' : guiLai ? 'Gửi lại để duyệt' : 'Tải ảnh lên'}</button>
    </div>
  );
}
