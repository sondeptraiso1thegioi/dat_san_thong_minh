#!/bin/bash
# 1) Database chưa có (hoặc RESET_DB=1): chạy nha-xe.sql (script này DROP DATABASE nên chỉ chạy khi cần).
# 2) Luôn chạy mọi migration trong database/migrations theo thứ tự tên (V002, V003, V004...).
#    Mỗi migration viết để chạy lại nhiều lần vẫn an toàn, nên không cần bảng ghi phiên bản.
set -e
SQLCMD() { /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$MSSQL_SA_PASSWORD" -C -I -b "$@"; }
co_db() { SQLCMD -h -1 -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID('NhaXeThongMinh') IS NOT NULL THEN 1 ELSE 0 END" 2>/dev/null | tr -d '[:space:]' | grep -qx 1; }

if [ "$RESET_DB" = "1" ] || ! co_db; then
  echo "Tạo database mới từ nha-xe.sql (không có tài khoản; admin đầu tiên do API tạo từ ADMIN_* trong .env)..."
  SQLCMD -i /scripts/nha-xe.sql > /tmp/init.log
else
  echo "Database NhaXeThongMinh đã có, giữ nguyên dữ liệu."
fi

for f in $(ls /scripts/migrations/V*.sql | sort); do
  echo "Chạy migration $(basename "$f")"
  SQLCMD -i "$f" > /dev/null
done
echo "Xong."
