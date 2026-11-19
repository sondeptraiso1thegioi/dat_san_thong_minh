import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { PhienDangNhap } from '@/modules/tai-khoan';
import { taoServices, type Services } from './container';

interface AppState {
  services: Services;
  phien: PhienDangNhap | null;
  thongBaoPhien: string | null;
  dangNhap: (sdt: string, matKhau: string) => Promise<PhienDangNhap>;
  dangXuat: () => void;
}

const Ctx = createContext<AppState | null>(null);

export function AppProvider({ children }: { children: ReactNode }) {
  const services = useMemo(taoServices, []);
  const [phien, setPhien] = useState<PhienDangNhap | null>(() => services.phien.doc());
  const [thongBaoPhien, setThongBaoPhien] = useState<string | null>(null);

  useEffect(() => {
    services.phien.dangKyNghe((p, tb) => { setPhien(p); setThongBaoPhien(tb ?? null); });
  }, [services]);

  // Có phiên thì nối kênh thời gian thực, đăng xuất / hết phiên thì ngắt
  const token = phien?.token ?? null;
  useEffect(() => {
    if (!token) return;
    services.thoiGianThuc.ketNoi(() => services.phien.layToken());
    return () => services.thoiGianThuc.ngat();
  }, [services, token]);

  const value: AppState = {
    services,
    phien,
    thongBaoPhien,
    dangNhap: async (sdt, matKhau) => {
      const p = await services.auth.dangNhap(sdt, matKhau);
      services.phien.luu(p);
      return p;
    },
    dangXuat: () => services.phien.luu(null),
  };
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useApp() {
  const v = useContext(Ctx);
  if (!v) throw new Error('useApp phải nằm trong AppProvider');
  return v;
}
