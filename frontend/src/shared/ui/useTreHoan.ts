import { useEffect, useState } from 'react';

/** Trả về giá trị sau khi người dùng ngừng thay đổi trong `ms` mili giây (gõ đến đâu lọc đến đó nhưng không gọi API mỗi phím). */
export function useTreHoan<T>(giaTri: T, ms = 300): T {
  const [tre, setTre] = useState(giaTri);
  useEffect(() => {
    const id = window.setTimeout(() => setTre(giaTri), ms);
    return () => window.clearTimeout(id);
  }, [giaTri, ms]);
  return tre;
}
