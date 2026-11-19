/* =====================================================================
   CSDL: Ứng dụng quản lý nhà xe thông minh tích hợp AI
   Hệ quản trị: Microsoft SQL Server 2022 (chạy trong SSMS: mở file, F5)
   Theo đề cương v10. Sau file này chạy database/migrations/V002..V004 theo thứ tự
   (V004 = chia khu: khu_vuc.suc_chua, camera.vai_tro, giu_cho_khu, nguồn CAMERA_KHU).
   db-init.sh trong Docker tự làm việc này.

   Nội dung:
     1. Tạo database NhaXeThongMinh (XOÁ database cũ cùng tên nếu có)
     2. 21 bảng + khoá chính / khoá ngoại / CHECK / UNIQUE / index
     3. Trigger chặn gói chồng ngày trên cùng một xe
     4. Hàm quyền dùng xe, view chỗ còn bán, view vị trí xe
     5. Thủ tục mua gói có khoá (chống bán quá sức chứa)
     6. Dữ liệu khởi tạo: cấu hình, loại xe, gói giá, sơ đồ bãi. KHÔNG có tài khoản hay xe nào:
        người dùng tự đăng ký; admin đầu tiên do API tạo từ biến môi trường ADMIN_* (.env).

   Quy ước:
     - Thời gian lưu giờ Việt Nam (SYSDATETIME() của máy chủ), DATETIME2(0).
     - Tiền: DECIMAL(12,0), đơn vị đồng.
     - Toạ độ sơ đồ / vùng trên ảnh: chuỗi JSON (kiểm tra bằng ISJSON).
     - Gói chỉ giữ SUẤT trong sức chứa, KHÔNG gán ô cố định:
       không có cột nào nối dang_ky_cho với o_do.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- bắt buộc cho filtered index (SSMS mặc định đã ON, sqlcmd thì không)
GO

USE master;
GO

IF DB_ID(N'NhaXeThongMinh') IS NOT NULL
BEGIN
    ALTER DATABASE NhaXeThongMinh SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE NhaXeThongMinh;
END
GO

CREATE DATABASE NhaXeThongMinh COLLATE Vietnamese_CI_AS;
GO

USE NhaXeThongMinh;
GO

/* =====================================================================
   PHẦN 1. TÀI KHOẢN VÀ NHÓM
   ===================================================================== */

CREATE TABLE dbo.tai_khoan (
    id               INT IDENTITY(1,1) NOT NULL,
    ho_ten           NVARCHAR(100)  NOT NULL,
    sdt              VARCHAR(15)    NOT NULL,
    email            VARCHAR(255)   NULL,
    mat_khau_hash    VARCHAR(255)   NOT NULL,          -- bcrypt, không lưu mật khẩu gốc
    vai_tro          VARCHAR(10)    NOT NULL CONSTRAINT DF_tai_khoan_vai_tro DEFAULT 'USER',
    trang_thai       VARCHAR(10)    NOT NULL CONSTRAINT DF_tai_khoan_trang_thai DEFAULT 'HOAT_DONG',
    phien_hien_tai   VARCHAR(100)   NULL,              -- mã phiên duy nhất: đăng nhập máy mới thì ghi đè, máy cũ mất hiệu lực
    fcm_token        VARCHAR(500)   NULL,              -- token đẩy thông báo của máy đang đăng nhập
    dang_nhap_luc    DATETIME2(0)   NULL,
    ma_khoi_phuc_hash VARCHAR(64)   NULL,              -- Module 1: chỉ lưu SHA-256 của mã ngẫu nhiên
    ma_khoi_phuc_het_han_luc DATETIME2(0) NULL,
    gui_ma_khoi_phuc_luc DATETIME2(0) NULL,
    so_lan_sai       INT            NOT NULL CONSTRAINT DF_tai_khoan_so_lan_sai DEFAULT 0, -- sai mật khẩu liên tiếp
    khoa_dang_nhap_den DATETIME2(0) NULL,              -- 5 lần sai thì tạm khoá đến lúc này (15 phút, tăng dần)
    phien_ban        ROWVERSION     NOT NULL,          -- chặn dùng lại mã trong yêu cầu đồng thời
    tao_luc          DATETIME2(0)   NOT NULL CONSTRAINT DF_tai_khoan_tao_luc DEFAULT SYSDATETIME(),
    CONSTRAINT PK_tai_khoan PRIMARY KEY (id),
    CONSTRAINT UQ_tai_khoan_sdt UNIQUE (sdt),
    CONSTRAINT CK_tai_khoan_vai_tro CHECK (vai_tro IN ('ADMIN', 'USER')),
    CONSTRAINT CK_tai_khoan_trang_thai CHECK (trang_thai IN ('HOAT_DONG', 'KHOA'))
);
-- email không bắt buộc nhưng nếu có thì không trùng
CREATE UNIQUE INDEX UX_tai_khoan_email ON dbo.tai_khoan(email) WHERE email IS NOT NULL;
GO

CREATE TABLE dbo.nhom (
    id               INT IDENTITY(1,1) NOT NULL,
    chu_nhom_id      INT            NOT NULL,
    ten              NVARCHAR(100)  NOT NULL,
    ma_moi           VARCHAR(10)    NOT NULL,
    ma_het_han_luc   DATETIME2(0)   NULL,
    tao_luc          DATETIME2(0)   NOT NULL CONSTRAINT DF_nhom_tao_luc DEFAULT SYSDATETIME(),
    CONSTRAINT PK_nhom PRIMARY KEY (id),
    CONSTRAINT UQ_nhom_ma_moi UNIQUE (ma_moi),
    CONSTRAINT UQ_nhom_chu_nhom UNIQUE (chu_nhom_id),   -- mỗi người làm chủ tối đa 1 nhóm
    CONSTRAINT FK_nhom_chu_nhom FOREIGN KEY (chu_nhom_id) REFERENCES dbo.tai_khoan(id)
);
GO

CREATE TABLE dbo.thanh_vien_nhom (
    id               INT IDENTITY(1,1) NOT NULL,
    nhom_id          INT            NOT NULL,
    tai_khoan_id     INT            NOT NULL,
    vai_tro          VARCHAR(10)    NOT NULL CONSTRAINT DF_tvn_vai_tro DEFAULT 'THANH_VIEN',
    tham_gia_luc     DATETIME2(0)   NOT NULL CONSTRAINT DF_tvn_tham_gia_luc DEFAULT SYSDATETIME(),
    CONSTRAINT PK_thanh_vien_nhom PRIMARY KEY (id),
    CONSTRAINT UQ_tvn_tai_khoan UNIQUE (tai_khoan_id),  -- mỗi người chỉ ở 1 nhóm
    CONSTRAINT CK_tvn_vai_tro CHECK (vai_tro IN ('CHU', 'THANH_VIEN')),
    CONSTRAINT FK_tvn_nhom FOREIGN KEY (nhom_id) REFERENCES dbo.nhom(id) ON DELETE CASCADE,
    CONSTRAINT FK_tvn_tai_khoan FOREIGN KEY (tai_khoan_id) REFERENCES dbo.tai_khoan(id)
);
CREATE INDEX IX_tvn_nhom ON dbo.thanh_vien_nhom(nhom_id);
GO

CREATE TABLE dbo.yeu_cau_tham_gia (
    id               INT IDENTITY(1,1) NOT NULL,
    nhom_id          INT            NOT NULL,
    tai_khoan_id     INT            NOT NULL,
    trang_thai       VARCHAR(10)    NOT NULL CONSTRAINT DF_yctg_trang_thai DEFAULT 'CHO_DUYET',
    tao_luc          DATETIME2(0)   NOT NULL CONSTRAINT DF_yctg_tao_luc DEFAULT SYSDATETIME(),
    xu_ly_luc        DATETIME2(0)   NULL,
    CONSTRAINT PK_yeu_cau_tham_gia PRIMARY KEY (id),
    CONSTRAINT CK_yctg_trang_thai CHECK (trang_thai IN ('CHO_DUYET', 'DONG_Y', 'TU_CHOI')),
    CONSTRAINT CK_yctg_xu_ly CHECK (trang_thai = 'CHO_DUYET' OR xu_ly_luc IS NOT NULL),
    CONSTRAINT FK_yctg_nhom FOREIGN KEY (nhom_id) REFERENCES dbo.nhom(id) ON DELETE CASCADE,
    CONSTRAINT FK_yctg_tai_khoan FOREIGN KEY (tai_khoan_id) REFERENCES dbo.tai_khoan(id)
);
-- một người không gửi 2 yêu cầu đang chờ vào cùng một nhóm
CREATE UNIQUE INDEX UX_yctg_dang_cho ON dbo.yeu_cau_tham_gia(nhom_id, tai_khoan_id) WHERE trang_thai = 'CHO_DUYET';
GO

/* =====================================================================
   PHẦN 2. LOẠI XE, XE, GÓI CHỖ
   ===================================================================== */

CREATE TABLE dbo.loai_xe (
    id                 INT IDENTITY(1,1) NOT NULL,
    ten                VARCHAR(10)    NOT NULL,
    suc_chua           INT            NOT NULL,
    gia_qua_han_ngay   DECIMAL(12,0)  NOT NULL,
    CONSTRAINT PK_loai_xe PRIMARY KEY (id),
    CONSTRAINT UQ_loai_xe_ten UNIQUE (ten),
    CONSTRAINT CK_loai_xe_ten CHECK (ten IN ('O_TO', 'XE_MAY')),
    CONSTRAINT CK_loai_xe_suc_chua CHECK (suc_chua >= 0),
    CONSTRAINT CK_loai_xe_gia CHECK (gia_qua_han_ngay >= 0)
);
GO

CREATE TABLE dbo.xe (
    id                 INT IDENTITY(1,1) NOT NULL,
    tai_khoan_id       INT            NOT NULL,          -- chủ xe
    loai_xe_id         INT            NOT NULL,
    bien_so            VARCHAR(15)    NOT NULL,          -- chuẩn hoá: viết hoa, bỏ dấu chấm/gạch/khoảng trắng, ví dụ 29A12345
    anh_giay_dang_ky   VARCHAR(500)   NULL,              -- khoá tệp ảnh giấy đăng ký trong kho lưu trữ, ví dụ giay-dk/5.png
    trang_thai_duyet   VARCHAR(10)    NOT NULL CONSTRAINT DF_xe_duyet DEFAULT 'CHO_DUYET',
    chia_se_nhom       BIT            NOT NULL CONSTRAINT DF_xe_chia_se DEFAULT 0,
    tao_luc            DATETIME2(0)   NOT NULL CONSTRAINT DF_xe_tao_luc DEFAULT SYSDATETIME(),
    ly_do_go           NVARCHAR(255)  NULL,              -- Admin gỡ xe phạm quy (gỡ mềm, không xoá dòng)
    go_luc             DATETIME2(0)   NULL,
    go_boi_id          INT            NULL,              -- Admin thực hiện
    ly_do_tu_choi      NVARCHAR(255)  NULL,              -- Admin từ chối duyệt: lý do hiện cho chủ xe; gửi lại ảnh thì xoá
    CONSTRAINT PK_xe PRIMARY KEY (id),
    CONSTRAINT UQ_xe_bien_so UNIQUE (bien_so),
    CONSTRAINT CK_xe_duyet CHECK (trang_thai_duyet IN ('CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'BI_GO')),
    CONSTRAINT CK_xe_go CHECK ((trang_thai_duyet = 'BI_GO' AND ly_do_go IS NOT NULL AND go_luc IS NOT NULL)
                            OR (trang_thai_duyet <> 'BI_GO' AND ly_do_go IS NULL AND go_luc IS NULL)),
    CONSTRAINT CK_xe_tu_choi CHECK ((trang_thai_duyet = 'TU_CHOI' AND ly_do_tu_choi IS NOT NULL)
                                OR (trang_thai_duyet <> 'TU_CHOI' AND ly_do_tu_choi IS NULL)),
    CONSTRAINT FK_xe_go_boi FOREIGN KEY (go_boi_id) REFERENCES dbo.tai_khoan(id),
    CONSTRAINT CK_xe_bien_so CHECK (bien_so NOT LIKE '%[^A-Z0-9]%' AND LEN(bien_so) >= 7),
    CONSTRAINT FK_xe_tai_khoan FOREIGN KEY (tai_khoan_id) REFERENCES dbo.tai_khoan(id),
    CONSTRAINT FK_xe_loai_xe FOREIGN KEY (loai_xe_id) REFERENCES dbo.loai_xe(id)
);
CREATE INDEX IX_xe_tai_khoan ON dbo.xe(tai_khoan_id);
GO

CREATE TABLE dbo.goi_cho (
    id                 INT IDENTITY(1,1) NOT NULL,
    loai_xe_id         INT            NOT NULL,
    ten                NVARCHAR(50)   NOT NULL,          -- 1 tuần / 1 tháng / 1 năm
    so_ngay            INT            NOT NULL,
    gia                DECIMAL(12,0)  NOT NULL,
    dang_ban           BIT            NOT NULL CONSTRAINT DF_goi_cho_dang_ban DEFAULT 1,
    CONSTRAINT PK_goi_cho PRIMARY KEY (id),
    CONSTRAINT UQ_goi_cho UNIQUE (loai_xe_id, so_ngay),
    CONSTRAINT CK_goi_cho_so_ngay CHECK (so_ngay > 0),
    CONSTRAINT CK_goi_cho_gia CHECK (gia >= 0),
    CONSTRAINT FK_goi_cho_loai_xe FOREIGN KEY (loai_xe_id) REFERENCES dbo.loai_xe(id)
);
GO

CREATE TABLE dbo.dang_ky_cho (
    id                 INT IDENTITY(1,1) NOT NULL,
    xe_id              INT            NOT NULL,
    goi_cho_id         INT            NOT NULL,
    ngay_bat_dau       DATETIME2(0)   NOT NULL,          -- giữ giờ để nhắc trước đúng 72h
    ngay_ket_thuc      DATETIME2(0)   NOT NULL,
    so_tien            DECIMAL(12,0)  NOT NULL,          -- giá tại lúc mua (giá gói đổi sau không ảnh hưởng)
    trang_thai         VARCHAR(10)    NOT NULL CONSTRAINT DF_dkc_trang_thai DEFAULT 'CON_HAN',
    mua_luc            DATETIME2(0)   NOT NULL CONSTRAINT DF_dkc_mua_luc DEFAULT SYSDATETIME(),
    da_nhac_72h        BIT            NOT NULL CONSTRAINT DF_dkc_nhac DEFAULT 0,
    ly_do_huy          NVARCHAR(255)  NULL,              -- Admin huỷ: lý do hiện cho khách; khách tự huỷ: lý do khách chọn
    gop_y              NVARCHAR(500)  NULL,              -- góp ý tự do của khách khi tự huỷ gói (gửi cho Admin)
    huy_luc            DATETIME2(0)   NULL,
    huy_boi_id         INT            NULL,              -- chủ xe tự huỷ, hoặc Admin
    CONSTRAINT PK_dang_ky_cho PRIMARY KEY (id),
    CONSTRAINT CK_dkc_trang_thai CHECK (trang_thai IN ('CON_HAN', 'HET_HAN', 'HUY')),
    CONSTRAINT CK_dkc_ngay CHECK (ngay_ket_thuc > ngay_bat_dau),
    CONSTRAINT CK_dkc_so_tien CHECK (so_tien >= 0),
    CONSTRAINT FK_dkc_xe FOREIGN KEY (xe_id) REFERENCES dbo.xe(id),
    CONSTRAINT FK_dkc_goi_cho FOREIGN KEY (goi_cho_id) REFERENCES dbo.goi_cho(id),
    CONSTRAINT FK_dkc_huy_boi FOREIGN KEY (huy_boi_id) REFERENCES dbo.tai_khoan(id)
    -- KHÔNG có cột o_do_id: gói chỉ giữ suất, không giữ ô (v6)
);
CREATE INDEX IX_dkc_xe_hieu_luc ON dbo.dang_ky_cho(xe_id, ngay_ket_thuc) INCLUDE (ngay_bat_dau, trang_thai);
-- tác vụ nhắc hạn 72h quét theo ngày kết thúc
CREATE INDEX IX_dkc_nhac_han ON dbo.dang_ky_cho(ngay_ket_thuc) WHERE trang_thai = 'CON_HAN' AND da_nhac_72h = 0;
GO

/* =====================================================================
   PHẦN 3. SƠ ĐỒ BÃI, CAMERA, ĐƯỜNG ĐI (Find my car)
   ===================================================================== */

CREATE TABLE dbo.khu_vuc (
    id                 INT IDENTITY(1,1) NOT NULL,
    ten                NVARCHAR(50)   NOT NULL,          -- ví dụ "Khu A"
    loai_xe_id         INT            NOT NULL,
    toa_do_so_do       NVARCHAR(1000) NULL,              -- JSON, ví dụ {"x":10,"y":20,"w":200,"h":80}
    CONSTRAINT PK_khu_vuc PRIMARY KEY (id),
    CONSTRAINT UQ_khu_vuc_ten UNIQUE (ten),
    CONSTRAINT CK_khu_vuc_toa_do CHECK (toa_do_so_do IS NULL OR ISJSON(toa_do_so_do) = 1),
    CONSTRAINT FK_khu_vuc_loai_xe FOREIGN KEY (loai_xe_id) REFERENCES dbo.loai_xe(id)
);
GO

CREATE TABLE dbo.camera (
    id                 INT IDENTITY(1,1) NOT NULL,
    khu_vuc_id         INT            NOT NULL,
    ten                NVARCHAR(50)   NOT NULL,
    nguon_anh          VARCHAR(500)   NOT NULL,          -- URL luồng RTSP hoặc đường dẫn ảnh mẫu (demo)
    dang_bat           BIT            NOT NULL CONSTRAINT DF_camera_dang_bat DEFAULT 1,
    CONSTRAINT PK_camera PRIMARY KEY (id),
    CONSTRAINT UQ_camera_ten UNIQUE (ten),
    CONSTRAINT FK_camera_khu_vuc FOREIGN KEY (khu_vuc_id) REFERENCES dbo.khu_vuc(id)
);
CREATE INDEX IX_camera_khu_vuc ON dbo.camera(khu_vuc_id);
GO

CREATE TABLE dbo.o_do (
    id                 INT IDENTITY(1,1) NOT NULL,
    khu_vuc_id         INT            NOT NULL,
    camera_id          INT            NULL,              -- camera nhìn thấy ô này (nếu có vẽ vùng)
    ma_o               VARCHAR(10)    NOT NULL,          -- ví dụ A12
    vung_tren_anh      NVARCHAR(2000) NULL,              -- JSON đa giác trên ảnh camera, ví dụ [[x1,y1],[x2,y2],...]
    toa_do_so_do       NVARCHAR(1000) NULL,              -- JSON vị trí ô trên sơ đồ app
    ma_qr              VARCHAR(50)    NOT NULL,          -- mã dán tại ô, dùng cho "Báo vị trí"
    CONSTRAINT PK_o_do PRIMARY KEY (id),
    CONSTRAINT UQ_o_do_ma_o UNIQUE (ma_o),
    CONSTRAINT UQ_o_do_ma_qr UNIQUE (ma_qr),
    CONSTRAINT UQ_o_do_id_khu UNIQUE (id, khu_vuc_id),    -- để khoá ngoại ghép (ô phải thuộc đúng khu)
    CONSTRAINT CK_o_do_vung CHECK (vung_tren_anh IS NULL OR ISJSON(vung_tren_anh) = 1),
    CONSTRAINT CK_o_do_toa_do CHECK (toa_do_so_do IS NULL OR ISJSON(toa_do_so_do) = 1),
    CONSTRAINT CK_o_do_vung_can_camera CHECK (vung_tren_anh IS NULL OR camera_id IS NOT NULL),
    CONSTRAINT FK_o_do_khu_vuc FOREIGN KEY (khu_vuc_id) REFERENCES dbo.khu_vuc(id),
    CONSTRAINT FK_o_do_camera FOREIGN KEY (camera_id) REFERENCES dbo.camera(id)
);
CREATE INDEX IX_o_do_camera ON dbo.o_do(camera_id) WHERE camera_id IS NOT NULL;
GO

-- Trạng thái mới nhất của từng ô (module BaiXe). Hôm nay Admin đặt tay; khi có AI ô trống thì AI ghi qua IBaiXeModule.
CREATE TABLE dbo.trang_thai_o (
    o_do_id            INT            NOT NULL,
    trang_thai         VARCHAR(10)    NOT NULL,
    nguon              VARCHAR(5)     NOT NULL,
    cap_nhat_luc       DATETIME2(0)   NOT NULL,
    CONSTRAINT PK_trang_thai_o PRIMARY KEY (o_do_id),
    CONSTRAINT CK_tto_trang_thai CHECK (trang_thai IN ('TRONG', 'CO_XE', 'KHONG_RO')),
    CONSTRAINT CK_tto_nguon CHECK (nguon IN ('ADMIN', 'AI')),
    CONSTRAINT FK_tto_o_do FOREIGN KEY (o_do_id) REFERENCES dbo.o_do(id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.nut_duong_di (
    id                 INT IDENTITY(1,1) NOT NULL,
    ten                NVARCHAR(50)   NOT NULL,          -- "Lối vào", "Thang máy", "Đầu khu A"...
    toa_do             NVARCHAR(200)  NOT NULL,          -- JSON {"x":..,"y":..}
    khu_vuc_id         INT            NULL,              -- nút đầu khu thì gắn khu
    CONSTRAINT PK_nut_duong_di PRIMARY KEY (id),
    CONSTRAINT CK_nut_toa_do CHECK (ISJSON(toa_do) = 1),
    CONSTRAINT FK_nut_khu_vuc FOREIGN KEY (khu_vuc_id) REFERENCES dbo.khu_vuc(id)
);
GO

CREATE TABLE dbo.canh_duong_di (
    id                 INT IDENTITY(1,1) NOT NULL,
    nut_a_id           INT            NOT NULL,
    nut_b_id           INT            NOT NULL,
    do_dai             DECIMAL(8,2)   NOT NULL,          -- mét, trọng số cho Dijkstra; cạnh vô hướng
    CONSTRAINT PK_canh_duong_di PRIMARY KEY (id),
    CONSTRAINT CK_canh_khac_nut CHECK (nut_a_id < nut_b_id), -- vô hướng: luôn lưu id nhỏ trước, tránh trùng A-B / B-A
    CONSTRAINT CK_canh_do_dai CHECK (do_dai > 0),
    CONSTRAINT UQ_canh UNIQUE (nut_a_id, nut_b_id),
    CONSTRAINT FK_canh_nut_a FOREIGN KEY (nut_a_id) REFERENCES dbo.nut_duong_di(id),
    CONSTRAINT FK_canh_nut_b FOREIGN KEY (nut_b_id) REFERENCES dbo.nut_duong_di(id)
);
CREATE INDEX IX_canh_nut_b ON dbo.canh_duong_di(nut_b_id);
GO

/* =====================================================================
   PHẦN 4. LƯỢT GỬI, XÁC NHẬN XE RA, CỔNG
   ===================================================================== */

CREATE TABLE dbo.luot_gui (
    id                       INT IDENTITY(1,1) NOT NULL,
    xe_id                    INT            NOT NULL,
    thoi_gian_vao            DATETIME2(0)   NOT NULL,
    thoi_gian_ra             DATETIME2(0)   NULL,        -- NULL = lượt đang mở
    anh_vao                  VARCHAR(500)   NULL,
    anh_ra                   VARCHAR(500)   NULL,
    do_tin_cay_vao           DECIMAL(5,4)   NULL,        -- 0..1
    do_tin_cay_ra            DECIMAL(5,4)   NULL,
    trang_thai               VARCHAR(15)    NOT NULL CONSTRAINT DF_lg_trang_thai DEFAULT 'DANG_GUI',
    -- vị trí mới nhất (Find my car), bản sao nhanh của dòng mới nhất trong vi_tri_xe
    vi_tri_khu_id            INT            NULL,
    vi_tri_o_id              INT            NULL,
    vi_tri_luc               DATETIME2(0)   NULL,
    vi_tri_nguon             VARCHAR(10)    NULL,
    so_lan_quet_khong_thay   INT            NOT NULL CONSTRAINT DF_lg_khong_thay DEFAULT 0,
    CONSTRAINT PK_luot_gui PRIMARY KEY (id),
    CONSTRAINT CK_lg_trang_thai CHECK (trang_thai IN ('DANG_GUI', 'CHO_XAC_NHAN', 'CHO_THANH_TOAN', 'DA_RA', 'NGHI_MAT')),
    -- DA_RA thì phải có giờ ra; các trạng thái khác (xe còn trong bãi) thì chưa có giờ ra
    CONSTRAINT CK_lg_da_ra CHECK ((trang_thai = 'DA_RA' AND thoi_gian_ra IS NOT NULL)
                               OR (trang_thai <> 'DA_RA' AND thoi_gian_ra IS NULL)),
    CONSTRAINT CK_lg_thoi_gian CHECK (thoi_gian_ra IS NULL OR thoi_gian_ra >= thoi_gian_vao),
    CONSTRAINT CK_lg_tin_cay CHECK ((do_tin_cay_vao IS NULL OR do_tin_cay_vao BETWEEN 0 AND 1)
                                AND (do_tin_cay_ra  IS NULL OR do_tin_cay_ra  BETWEEN 0 AND 1)),
    CONSTRAINT CK_lg_vi_tri_nguon CHECK (vi_tri_nguon IS NULL OR vi_tri_nguon IN ('QUET', 'NGUOI_DUNG', 'ADMIN')),
    CONSTRAINT CK_lg_vi_tri_du CHECK (
        (vi_tri_khu_id IS NULL AND vi_tri_o_id IS NULL AND vi_tri_luc IS NULL AND vi_tri_nguon IS NULL)
     OR (vi_tri_khu_id IS NOT NULL AND vi_tri_luc IS NOT NULL AND vi_tri_nguon IS NOT NULL)),
    CONSTRAINT CK_lg_khong_thay CHECK (so_lan_quet_khong_thay >= 0),
    CONSTRAINT FK_lg_xe FOREIGN KEY (xe_id) REFERENCES dbo.xe(id),
    CONSTRAINT FK_lg_vi_tri_khu FOREIGN KEY (vi_tri_khu_id) REFERENCES dbo.khu_vuc(id),
    CONSTRAINT FK_lg_vi_tri_o FOREIGN KEY (vi_tri_o_id, vi_tri_khu_id) REFERENCES dbo.o_do(id, khu_vuc_id)
);
GO
-- mỗi xe tối đa MỘT lượt gửi đang mở
CREATE UNIQUE INDEX UX_lg_mot_luot_mo ON dbo.luot_gui(xe_id) WHERE thoi_gian_ra IS NULL;
-- bộ quét bãi chỉ so khớp với các xe đang trong bãi
CREATE INDEX IX_lg_dang_mo ON dbo.luot_gui(trang_thai) INCLUDE (xe_id, vi_tri_luc, so_lan_quet_khong_thay) WHERE thoi_gian_ra IS NULL;
CREATE INDEX IX_lg_xe_thoi_gian ON dbo.luot_gui(xe_id, thoi_gian_vao DESC);
GO

-- Xe ra cần CẢ người dùng xe (chủ xe hoặc thành viên nhóm được chia sẻ) VÀ quản lý bãi xác nhận trong 60 giây (chốt 06/11/2026).
-- Thuộc module CongRaVao.
CREATE TABLE dbo.yeu_cau_xac_nhan (
    id                     INT IDENTITY(1,1) NOT NULL,
    luot_gui_id            INT            NOT NULL,
    anh_cong_ra            VARCHAR(500)   NULL,
    tao_luc                DATETIME2(0)   NOT NULL,
    het_han_luc            DATETIME2(0)   NOT NULL,      -- tao_luc + 60 giây
    ket_qua                VARCHAR(10)    NOT NULL CONSTRAINT DF_ycxn_ket_qua DEFAULT 'CHO',
    chu_xe_dong_y          BIT            NULL,          -- NULL = người dùng xe chưa trả lời
    chu_xe_tra_loi_id      INT            NULL,          -- chủ xe hoặc thành viên nhóm bấm trước
    chu_xe_tra_loi_luc     DATETIME2(0)   NULL,
    quan_ly_dong_y         BIT            NULL,          -- NULL = quản lý chưa trả lời
    quan_ly_id             INT            NULL,
    quan_ly_tra_loi_luc    DATETIME2(0)   NULL,
    ly_do                  NVARCHAR(255)  NULL,          -- lý do kết thúc (từ chối, hết giờ)
    ket_thuc_luc           DATETIME2(0)   NULL,
    phien_ban              ROWVERSION     NOT NULL,      -- khoá lạc quan: hai người bấm cùng lúc thì một người phải thử lại
    CONSTRAINT PK_yeu_cau_xac_nhan PRIMARY KEY (id),
    CONSTRAINT CK_ycxn_ket_qua CHECK (ket_qua IN ('CHO', 'DONG_Y', 'TU_CHOI', 'HET_HAN')),
    CONSTRAINT CK_ycxn_han CHECK (het_han_luc > tao_luc),
    CONSTRAINT CK_ycxn_chu_xe CHECK ((chu_xe_dong_y IS NULL AND chu_xe_tra_loi_id IS NULL AND chu_xe_tra_loi_luc IS NULL)
                                  OR (chu_xe_dong_y IS NOT NULL AND chu_xe_tra_loi_id IS NOT NULL AND chu_xe_tra_loi_luc IS NOT NULL)),
    CONSTRAINT CK_ycxn_quan_ly CHECK ((quan_ly_dong_y IS NULL AND quan_ly_id IS NULL AND quan_ly_tra_loi_luc IS NULL)
                                   OR (quan_ly_dong_y IS NOT NULL AND quan_ly_id IS NOT NULL AND quan_ly_tra_loi_luc IS NOT NULL)),
    -- DONG_Y chỉ khi CẢ HAI đã đồng ý; CHO thì chưa kết thúc
    CONSTRAINT CK_ycxn_dong_y CHECK (ket_qua <> 'DONG_Y' OR (chu_xe_dong_y = 1 AND quan_ly_dong_y = 1)),
    CONSTRAINT CK_ycxn_ket_thuc CHECK ((ket_qua = 'CHO' AND ket_thuc_luc IS NULL) OR (ket_qua <> 'CHO' AND ket_thuc_luc IS NOT NULL)),
    CONSTRAINT FK_ycxn_luot_gui FOREIGN KEY (luot_gui_id) REFERENCES dbo.luot_gui(id),
    CONSTRAINT FK_ycxn_chu_xe FOREIGN KEY (chu_xe_tra_loi_id) REFERENCES dbo.tai_khoan(id),
    CONSTRAINT FK_ycxn_quan_ly FOREIGN KEY (quan_ly_id) REFERENCES dbo.tai_khoan(id)
);
-- mỗi lượt gửi chỉ có một yêu cầu đang chờ trả lời
CREATE UNIQUE INDEX UX_ycxn_dang_cho ON dbo.yeu_cau_xac_nhan(luot_gui_id) WHERE ket_qua = 'CHO';
-- tác vụ nền tìm yêu cầu quá 60 giây
CREATE INDEX IX_ycxn_het_han ON dbo.yeu_cau_xac_nhan(het_han_luc) WHERE ket_qua = 'CHO';
GO

-- Bản sao "xe nào đang trong bãi" của module GoiCho. CongRaVao phát sự kiện XeDaVao / XeDaRa, GoiCho nghe và ghi vào đây,
-- nhờ vậy GoiCho không phải hỏi ngược CongRaVao (tránh vòng phụ thuộc, xem docs/kien-truc-module.md).
CREATE TABLE dbo.xe_trong_bai (
    xe_id              INT            NOT NULL,
    luot_gui_id        INT            NOT NULL,
    vao_luc            DATETIME2(0)   NOT NULL,
    CONSTRAINT PK_xe_trong_bai PRIMARY KEY (xe_id),
    CONSTRAINT UQ_xtb_luot_gui UNIQUE (luot_gui_id),
    CONSTRAINT FK_xtb_xe FOREIGN KEY (xe_id) REFERENCES dbo.xe(id),
    CONSTRAINT FK_xtb_luot_gui FOREIGN KEY (luot_gui_id) REFERENCES dbo.luot_gui(id)
);
GO

CREATE TABLE dbo.su_kien_cong (
    id                 INT IDENTITY(1,1) NOT NULL,
    loai               VARCHAR(5)     NOT NULL,
    bien_so_doc_duoc   VARCHAR(15)    NULL,              -- NULL khi AI không đọc được
    do_tin_cay         DECIMAL(5,4)   NULL,              -- BỔ SUNG so với ERD
    luot_gui_id        INT            NULL,              -- BỔ SUNG so với ERD: nối sự kiện với lượt gửi (nếu khớp)
    ket_qua            VARCHAR(10)    NOT NULL,
    ly_do              NVARCHAR(255)  NULL,              -- bắt buộc khi từ chối hoặc Admin mở tay
    admin_xu_ly_id     INT            NULL,
    thoi_gian          DATETIME2(0)   NOT NULL CONSTRAINT DF_skc_thoi_gian DEFAULT SYSDATETIME(),
    CONSTRAINT PK_su_kien_cong PRIMARY KEY (id),
    CONSTRAINT CK_skc_loai CHECK (loai IN ('VAO', 'RA')),
    CONSTRAINT CK_skc_ket_qua CHECK (ket_qua IN ('CHO_QUA', 'TU_CHOI')),
    CONSTRAINT CK_skc_ly_do CHECK (ket_qua = 'CHO_QUA' AND admin_xu_ly_id IS NULL OR ly_do IS NOT NULL),
    CONSTRAINT CK_skc_tin_cay CHECK (do_tin_cay IS NULL OR do_tin_cay BETWEEN 0 AND 1),
    CONSTRAINT FK_skc_admin FOREIGN KEY (admin_xu_ly_id) REFERENCES dbo.tai_khoan(id),
    CONSTRAINT FK_skc_luot_gui FOREIGN KEY (luot_gui_id) REFERENCES dbo.luot_gui(id)
);
CREATE INDEX IX_skc_thoi_gian ON dbo.su_kien_cong(thoi_gian DESC);
CREATE INDEX IX_skc_bien_so ON dbo.su_kien_cong(bien_so_doc_duoc);
GO

/* =====================================================================
   PHẦN 5. THANH TOÁN, THÔNG BÁO
   ===================================================================== */

CREATE TABLE dbo.thanh_toan (
    id                 INT IDENTITY(1,1) NOT NULL,
    tai_khoan_id       INT            NOT NULL,          -- người trả tiền
    loai               VARCHAR(12)    NOT NULL,
    dang_ky_cho_id     INT            NULL,
    luot_gui_id        INT            NULL,
    so_ngay_qua_han    INT            NULL,
    so_tien            DECIMAL(12,0)  NOT NULL,
    trang_thai         VARCHAR(12)    NOT NULL CONSTRAINT DF_tt_trang_thai DEFAULT 'CHO',
    thoi_gian          DATETIME2(0)   NOT NULL CONSTRAINT DF_tt_thoi_gian DEFAULT SYSDATETIME(),
    CONSTRAINT PK_thanh_toan PRIMARY KEY (id),
    CONSTRAINT CK_tt_loai CHECK (loai IN ('MUA_GOI', 'PHI_QUA_HAN', 'HOAN_TIEN')),
    CONSTRAINT CK_tt_trang_thai CHECK (trang_thai IN ('CHO', 'THANH_CONG', 'THAT_BAI')),
    CONSTRAINT CK_tt_so_tien CHECK (so_tien >= 0),
    -- mua gói thì phải trỏ tới đăng ký; phí quá hạn thì trỏ tới lượt gửi và có số ngày
    CONSTRAINT CK_tt_doi_tuong CHECK (
        (loai = 'MUA_GOI'     AND dang_ky_cho_id IS NOT NULL AND luot_gui_id IS NULL AND so_ngay_qua_han IS NULL)
     OR (loai = 'PHI_QUA_HAN' AND luot_gui_id IS NOT NULL AND dang_ky_cho_id IS NULL AND so_ngay_qua_han > 0)
     OR (loai = 'HOAN_TIEN'   AND dang_ky_cho_id IS NOT NULL AND luot_gui_id IS NULL AND so_ngay_qua_han IS NULL)),
    CONSTRAINT FK_tt_tai_khoan FOREIGN KEY (tai_khoan_id) REFERENCES dbo.tai_khoan(id),
    CONSTRAINT FK_tt_dang_ky_cho FOREIGN KEY (dang_ky_cho_id) REFERENCES dbo.dang_ky_cho(id),
    CONSTRAINT FK_tt_luot_gui FOREIGN KEY (luot_gui_id) REFERENCES dbo.luot_gui(id)
);
CREATE INDEX IX_tt_tai_khoan ON dbo.thanh_toan(tai_khoan_id, thoi_gian DESC);
GO

CREATE TABLE dbo.thong_bao (
    id                 INT IDENTITY(1,1) NOT NULL,
    tai_khoan_id       INT            NOT NULL,
    loai               VARCHAR(12)    NOT NULL,
    noi_dung           NVARCHAR(500)  NOT NULL,
    gui_luc            DATETIME2(0)   NOT NULL CONSTRAINT DF_tb_gui_luc DEFAULT SYSDATETIME(),
    da_doc             BIT            NOT NULL CONSTRAINT DF_tb_da_doc DEFAULT 0,
    CONSTRAINT PK_thong_bao PRIMARY KEY (id),
    CONSTRAINT CK_tb_loai CHECK (loai IN ('VAO', 'RA', 'SAP_HET_HAN', 'HET_HAN', 'QUA_HAN', 'NGHI_MAT', 'HUY_GOI', 'XE_BI_GO', 'KHACH_HUY')),
    CONSTRAINT FK_tb_tai_khoan FOREIGN KEY (tai_khoan_id) REFERENCES dbo.tai_khoan(id) ON DELETE CASCADE
);
CREATE INDEX IX_tb_tai_khoan ON dbo.thong_bao(tai_khoan_id, gui_luc DESC) INCLUDE (da_doc);
GO

/* =====================================================================
   PHẦN 6. QUÉT BÃI VÀ VỊ TRÍ XE (Find my car)
   ===================================================================== */

CREATE TABLE dbo.lan_quet (
    id                 INT IDENTITY(1,1) NOT NULL,
    loai               VARCHAR(12)    NOT NULL,
    bat_dau_luc        DATETIME2(0)   NOT NULL CONSTRAINT DF_lq_bat_dau DEFAULT SYSDATETIME(),
    ket_thuc_luc       DATETIME2(0)   NULL,
    so_camera          INT            NOT NULL CONSTRAINT DF_lq_so_camera DEFAULT 0,
    so_bien_doc_duoc   INT            NOT NULL CONSTRAINT DF_lq_so_bien DEFAULT 0,
    so_xe_dinh_vi      INT            NOT NULL CONSTRAINT DF_lq_so_xe DEFAULT 0,
    trang_thai         VARCHAR(12)    NOT NULL CONSTRAINT DF_lq_trang_thai DEFAULT 'DANG_CHAY',
    CONSTRAINT PK_lan_quet PRIMARY KEY (id),
    CONSTRAINT CK_lq_loai CHECK (loai IN ('DINH_KY', 'SAU_XE_VAO', 'THU_CONG')),
    CONSTRAINT CK_lq_trang_thai CHECK (trang_thai IN ('DANG_CHAY', 'HOAN_THANH', 'LOI')),
    CONSTRAINT CK_lq_ket_thuc CHECK ((trang_thai = 'DANG_CHAY' AND ket_thuc_luc IS NULL)
                                  OR (trang_thai <> 'DANG_CHAY' AND ket_thuc_luc >= bat_dau_luc)),
    CONSTRAINT CK_lq_so_lieu CHECK (so_camera >= 0 AND so_bien_doc_duoc >= 0 AND so_xe_dinh_vi >= 0)
);
CREATE INDEX IX_lq_bat_dau ON dbo.lan_quet(bat_dau_luc DESC);
GO

CREATE TABLE dbo.vi_tri_xe (
    id                 BIGINT IDENTITY(1,1) NOT NULL,   -- bảng lịch sử, tăng nhanh nhất
    luot_gui_id        INT            NOT NULL,
    lan_quet_id        INT            NULL,
    camera_id          INT            NULL,
    khu_vuc_id         INT            NOT NULL,
    o_do_id            INT            NULL,              -- NULL = chỉ biết mức khu (xe máy hoặc camera không có vùng ô)
    bien_so_doc_duoc   VARCHAR(15)    NULL,
    do_tin_cay         DECIMAL(5,4)   NULL,
    anh_cat            VARCHAR(500)   NULL,              -- ảnh cắt quanh xe, chỉ trả cho người có quyền dùng xe
    nguon              VARCHAR(10)    NOT NULL,
    thay_luc           DATETIME2(0)   NOT NULL CONSTRAINT DF_vtx_thay_luc DEFAULT SYSDATETIME(),
    CONSTRAINT PK_vi_tri_xe PRIMARY KEY (id),
    CONSTRAINT CK_vtx_nguon CHECK (nguon IN ('QUET', 'NGUOI_DUNG', 'ADMIN')),
    -- kết quả quét phải biết thuộc lần quét nào, camera nào
    CONSTRAINT CK_vtx_quet CHECK (nguon <> 'QUET' OR (lan_quet_id IS NOT NULL AND camera_id IS NOT NULL AND bien_so_doc_duoc IS NOT NULL)),
    CONSTRAINT CK_vtx_tin_cay CHECK (do_tin_cay IS NULL OR do_tin_cay BETWEEN 0 AND 1),
    CONSTRAINT FK_vtx_luot_gui FOREIGN KEY (luot_gui_id) REFERENCES dbo.luot_gui(id),
    CONSTRAINT FK_vtx_lan_quet FOREIGN KEY (lan_quet_id) REFERENCES dbo.lan_quet(id),
    CONSTRAINT FK_vtx_camera FOREIGN KEY (camera_id) REFERENCES dbo.camera(id),
    CONSTRAINT FK_vtx_khu_vuc FOREIGN KEY (khu_vuc_id) REFERENCES dbo.khu_vuc(id),
    CONSTRAINT FK_vtx_o_do FOREIGN KEY (o_do_id, khu_vuc_id) REFERENCES dbo.o_do(id, khu_vuc_id)  -- ô phải thuộc đúng khu
);
CREATE INDEX IX_vtx_luot_gui ON dbo.vi_tri_xe(luot_gui_id, thay_luc DESC);
CREATE INDEX IX_vtx_lan_quet ON dbo.vi_tri_xe(lan_quet_id) WHERE lan_quet_id IS NOT NULL;
GO

CREATE TABLE dbo.cau_hinh (
    khoa               VARCHAR(50)    NOT NULL,
    gia_tri            NVARCHAR(200)  NOT NULL,
    mo_ta              NVARCHAR(255)  NULL,
    CONSTRAINT PK_cau_hinh PRIMARY KEY (khoa)
);
GO

/* =====================================================================
   PHẦN 7. TRIGGER, HÀM, VIEW, THỦ TỤC
   ===================================================================== */

-- Mỗi xe tối đa một gói CON_HAN tại một thời điểm: các gói gia hạn phải nối tiếp, không chồng ngày.
-- (UNIQUE không diễn tả được "khoảng thời gian không giao nhau" nên dùng trigger.)
CREATE TRIGGER dbo.trg_dang_ky_cho_khong_chong_ngay
ON dbo.dang_ky_cho
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.dang_ky_cho d
          ON d.xe_id = i.xe_id
         AND d.id <> i.id
         AND d.trang_thai = 'CON_HAN'
         AND i.trang_thai = 'CON_HAN'
         AND d.ngay_bat_dau < i.ngay_ket_thuc
         AND i.ngay_bat_dau < d.ngay_ket_thuc
    )
    BEGIN
        THROW 50010, N'Xe đã có gói còn hạn trùng khoảng thời gian này.', 1;
    END
END
GO

-- Danh sách xe một tài khoản được DÙNG: xe của mình + xe bật chia_se_nhom của người cùng nhóm.
-- Mọi API xem xe, tìm xe, xác nhận xe ra phải lọc qua hàm này (rủi ro 3 trong v6: quyền riêng tư).
CREATE FUNCTION dbo.fn_xe_duoc_dung (@tai_khoan_id INT)
RETURNS TABLE
AS
RETURN
(
    SELECT x.id AS xe_id, CAST(1 AS BIT) AS la_chu_xe
    FROM dbo.xe x
    WHERE x.tai_khoan_id = @tai_khoan_id
    UNION ALL
    SELECT x.id, CAST(0 AS BIT)
    FROM dbo.thanh_vien_nhom toi
    JOIN dbo.thanh_vien_nhom nguoi_khac
      ON nguoi_khac.nhom_id = toi.nhom_id
     AND nguoi_khac.tai_khoan_id <> toi.tai_khoan_id
    JOIN dbo.xe x
      ON x.tai_khoan_id = nguoi_khac.tai_khoan_id
     AND x.chia_se_nhom = 1
    WHERE toi.tai_khoan_id = @tai_khoan_id
);
GO

-- Chỗ còn bán theo loại xe (v6 mục 3):
--   còn bán = sức chứa − số XE đang giữ suất (có gói còn hạn hoặc chưa tới ngày bắt đầu)
--                      − số xe hết gói nhưng vẫn còn trong bãi
-- Đếm DISTINCT xe: xe đã mua gia hạn nối tiếp (2 dòng dang_ky_cho) chỉ chiếm 1 suất.
CREATE VIEW dbo.v_cho_con_ban
AS
WITH giu_suat AS (
    SELECT DISTINCT d.xe_id
    FROM dbo.dang_ky_cho d
    WHERE d.trang_thai = 'CON_HAN'
      AND d.ngay_ket_thuc > SYSDATETIME()
),
qua_han_trong_bai AS (
    SELECT lg.xe_id
    FROM dbo.xe_trong_bai lg          -- bản sao của GoiCho, không đọc luot_gui của CongRaVao
    WHERE lg.xe_id NOT IN (SELECT xe_id FROM giu_suat)
)
SELECT
    lx.id                               AS loai_xe_id,
    lx.ten                              AS loai_xe,
    lx.suc_chua,
    COUNT(DISTINCT gs.xe_id)            AS so_xe_giu_suat,
    COUNT(DISTINCT qh.xe_id)            AS so_xe_qua_han_trong_bai,
    lx.suc_chua - COUNT(DISTINCT gs.xe_id) - COUNT(DISTINCT qh.xe_id) AS so_cho_con_ban
FROM dbo.loai_xe lx
LEFT JOIN dbo.xe x            ON x.loai_xe_id = lx.id
LEFT JOIN giu_suat gs         ON gs.xe_id = x.id
LEFT JOIN qua_han_trong_bai qh ON qh.xe_id = x.id
GROUP BY lx.id, lx.ten, lx.suc_chua;
GO

-- Vị trí mới nhất của các xe đang trong bãi (màn hình "Tìm xe" và màn hình đối soát của Admin).
CREATE VIEW dbo.v_vi_tri_xe_hien_tai
AS
SELECT
    lg.id                      AS luot_gui_id,
    x.id                       AS xe_id,
    x.bien_so,
    lx.ten                     AS loai_xe,
    lg.thoi_gian_vao,
    lg.trang_thai,
    kv.ten                     AS khu,
    od.ma_o                    AS o,
    lg.vi_tri_luc              AS thay_luc,
    DATEDIFF(MINUTE, lg.vi_tri_luc, SYSDATETIME()) AS so_phut_truoc,
    lg.vi_tri_nguon,
    lg.so_lan_quet_khong_thay,
    CASE WHEN lg.vi_tri_khu_id IS NULL THEN N'Chưa định vị' ELSE N'Đã định vị' END AS tinh_trang
FROM dbo.luot_gui lg
JOIN dbo.xe x          ON x.id = lg.xe_id
JOIN dbo.loai_xe lx    ON lx.id = x.loai_xe_id
LEFT JOIN dbo.khu_vuc kv ON kv.id = lg.vi_tri_khu_id
LEFT JOIN dbo.o_do od    ON od.id = lg.vi_tri_o_id
WHERE lg.thoi_gian_ra IS NULL;
GO

-- Phí quá hạn còn nợ của một lượt gửi đang mở.
--   het_han_luc   = ngày kết thúc muộn nhất của các gói (không tính gói HUY) của xe
--   so_ngay       = số ngày tính từ lúc hết hạn tới giờ, làm tròn lên (quá 1 phút cũng tính 1 ngày)
--   so_ngay_da_tra= tổng số ngày đã trả PHI_QUA_HAN thành công cho lượt này, sau mốc hết hạn đó
--   Dùng chung cho: cổng ra (phải trả hết mới mở) và mua gói mới khi xe đang quá hạn trong bãi.
CREATE FUNCTION dbo.fn_phi_qua_han (@luot_gui_id INT)
RETURNS TABLE
AS
RETURN
(
    WITH lg AS (
        SELECT l.luot_gui_id AS id, l.xe_id, l.vao_luc AS thoi_gian_vao, x.loai_xe_id,
               COALESCE((SELECT MAX(d.ngay_ket_thuc) FROM dbo.dang_ky_cho d
                         WHERE d.xe_id = l.xe_id AND d.trang_thai <> 'HUY'), l.vao_luc) AS het_han_luc
        FROM dbo.xe_trong_bai l           -- bản sao của GoiCho (xe đang trong bãi)
        JOIN dbo.xe x ON x.id = l.xe_id
        WHERE l.luot_gui_id = @luot_gui_id
    ),
    tinh AS (
        SELECT lg.*, lx.gia_qua_han_ngay,
               CASE WHEN lg.het_han_luc >= SYSDATETIME() THEN 0
                    ELSE CAST(CEILING(DATEDIFF(MINUTE, lg.het_han_luc, SYSDATETIME()) / 1440.0) AS INT) END AS so_ngay,
               COALESCE((SELECT SUM(t.so_ngay_qua_han) FROM dbo.thanh_toan t
                         WHERE t.luot_gui_id = lg.id AND t.loai = 'PHI_QUA_HAN'
                           AND t.trang_thai = 'THANH_CONG' AND t.thoi_gian >= lg.het_han_luc), 0) AS so_ngay_da_tra
        FROM lg JOIN dbo.loai_xe lx ON lx.id = lg.loai_xe_id
    )
    SELECT id AS luot_gui_id, xe_id, het_han_luc, so_ngay, so_ngay_da_tra,
           CASE WHEN so_ngay > so_ngay_da_tra THEN so_ngay - so_ngay_da_tra ELSE 0 END AS so_ngay_con_no,
           CASE WHEN so_ngay > so_ngay_da_tra THEN so_ngay - so_ngay_da_tra ELSE 0 END * gia_qua_han_ngay AS so_tien_con_no
    FROM tinh
);
GO

-- Thanh toán phí quá hạn (giả lập: ghi THANH_CONG luôn). Người trả phải có quyền dùng xe.
CREATE PROCEDURE dbo.sp_thanh_toan_phi_qua_han
    @tai_khoan_id  INT,
    @luot_gui_id   INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @xe_id INT, @so_ngay INT, @so_tien DECIMAL(12,0);

    BEGIN TRY
    BEGIN TRAN;
        SELECT @xe_id = xe_id FROM dbo.xe_trong_bai WITH (UPDLOCK) WHERE luot_gui_id = @luot_gui_id;
        IF @xe_id IS NULL
            THROW 50020, N'Lượt gửi không tồn tại hoặc xe đã ra.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.fn_xe_duoc_dung(@tai_khoan_id) WHERE xe_id = @xe_id)
            THROW 50021, N'Tài khoản không có quyền dùng xe này.', 1;

        SELECT @so_ngay = so_ngay_con_no, @so_tien = so_tien_con_no FROM dbo.fn_phi_qua_han(@luot_gui_id);
        IF ISNULL(@so_ngay, 0) = 0
            THROW 50022, N'Xe không còn nợ phí quá hạn.', 1;

        INSERT dbo.thanh_toan (tai_khoan_id, loai, luot_gui_id, so_ngay_qua_han, so_tien, trang_thai)
        VALUES (@tai_khoan_id, 'PHI_QUA_HAN', @luot_gui_id, @so_ngay, @so_tien, 'THANH_CONG');
    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

-- Mua / gia hạn gói. Khoá dòng loai_xe để hai người mua cùng lúc không vượt sức chứa.
--   - Xe đang giữ suất (gói còn hạn) hoặc đang trong bãi: đây là gia hạn, không tốn thêm suất,
--     gói mới bắt đầu ngay khi gói cũ kết thúc.
--   - Ngược lại: cần còn ít nhất 1 chỗ, nếu không báo lỗi "Hết chỗ".
--   - Xe đang quá hạn trong bãi: PHẢI trả hết phí quá hạn (sp_thanh_toan_phi_qua_han) rồi mới được mua.
--   - Thanh toán giả lập: ghi thanh_toan THANH_CONG luôn.
CREATE PROCEDURE dbo.sp_mua_goi
    @tai_khoan_id    INT,
    @xe_id           INT,
    @goi_cho_id      INT,
    @dang_ky_cho_id  INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @loai_xe_id INT, @so_ngay INT, @gia DECIMAL(12,0), @bat_dau DATETIME2(0),
            @dang_giu_suat BIT = 0, @con_ban INT, @bay_gio DATETIME2(0) = SYSDATETIME();

    BEGIN TRY
    BEGIN TRAN;

    -- 1. Kiểm tra xe: đúng chủ, đã duyệt
    IF NOT EXISTS (SELECT 1 FROM dbo.xe WHERE id = @xe_id AND tai_khoan_id = @tai_khoan_id)
        THROW 50001, N'Chỉ chủ xe được mua gói cho xe này.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.xe WHERE id = @xe_id AND trang_thai_duyet = 'DA_DUYET')
        THROW 50002, N'Xe chưa được Admin duyệt.', 1;

    -- 2. Gói phải đang bán và đúng loại xe
    SELECT @loai_xe_id = g.loai_xe_id, @so_ngay = g.so_ngay, @gia = g.gia
    FROM dbo.goi_cho g
    JOIN dbo.xe x ON x.loai_xe_id = g.loai_xe_id AND x.id = @xe_id
    WHERE g.id = @goi_cho_id AND g.dang_ban = 1;
    IF @loai_xe_id IS NULL
        THROW 50003, N'Gói không tồn tại, ngừng bán hoặc không đúng loại xe.', 1;

    -- 3. Khoá theo loại xe (tuần tự hoá các lượt mua cùng loại)
    SELECT @con_ban = suc_chua FROM dbo.loai_xe WITH (UPDLOCK, HOLDLOCK) WHERE id = @loai_xe_id;

    -- 4. Xe đang giữ suất? (gói còn hạn, hoặc đang trong bãi)
    SELECT @bat_dau = MAX(ngay_ket_thuc)
    FROM dbo.dang_ky_cho
    WHERE xe_id = @xe_id AND trang_thai = 'CON_HAN' AND ngay_ket_thuc > @bay_gio;

    IF @bat_dau IS NOT NULL
       OR EXISTS (SELECT 1 FROM dbo.xe_trong_bai WHERE xe_id = @xe_id)
        SET @dang_giu_suat = 1;

    IF @bat_dau IS NULL SET @bat_dau = @bay_gio;

    -- 4b. Xe quá hạn còn trong bãi: chặn nếu chưa trả hết phí quá hạn
    DECLARE @luot_mo INT, @no_ngay INT, @no_tien DECIMAL(12,0), @thong_bao NVARCHAR(300);
    SELECT @luot_mo = luot_gui_id FROM dbo.xe_trong_bai WHERE xe_id = @xe_id;
    IF @luot_mo IS NOT NULL
    BEGIN
        SELECT @no_ngay = so_ngay_con_no, @no_tien = so_tien_con_no FROM dbo.fn_phi_qua_han(@luot_mo);
        IF @no_ngay > 0
        BEGIN
            SET @thong_bao = CONCAT(N'Xe còn nợ phí quá hạn ', @no_ngay, N' ngày (', FORMAT(@no_tien, 'N0', 'vi-VN'),
                                    N' đ). Thanh toán phí quá hạn trước khi mua gói mới.');
            THROW 50005, @thong_bao, 1;
        END
    END

    -- 5. Mua mới thì phải còn chỗ
    IF @dang_giu_suat = 0
    BEGIN
        SELECT @con_ban = so_cho_con_ban FROM dbo.v_cho_con_ban WHERE loai_xe_id = @loai_xe_id;
        IF @con_ban <= 0
            THROW 50004, N'Hết chỗ cho loại xe này.', 1;
    END

    -- 6. Ghi đăng ký và thanh toán
    INSERT dbo.dang_ky_cho (xe_id, goi_cho_id, ngay_bat_dau, ngay_ket_thuc, so_tien, trang_thai, mua_luc)
    VALUES (@xe_id, @goi_cho_id, @bat_dau, DATEADD(DAY, @so_ngay, @bat_dau), @gia, 'CON_HAN', @bay_gio);
    SET @dang_ky_cho_id = SCOPE_IDENTITY();

    INSERT dbo.thanh_toan (tai_khoan_id, loai, dang_ky_cho_id, so_tien, trang_thai, thoi_gian)
    VALUES (@tai_khoan_id, 'MUA_GOI', @dang_ky_cho_id, @gia, 'THANH_CONG', @bay_gio);

    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;   -- lỗi nghiệp vụ hay lỗi hệ thống đều huỷ toàn bộ, không để treo giao dịch
        THROW;
    END CATCH
END
GO

-- Huỷ gói trước hạn (chủ xe). Số tiền hoàn do tầng nghiệp vụ C# tính theo chính sách hoàn tiền
-- và truyền vào; thủ tục kiểm tra lại các quy tắc, khoá theo loại xe rồi ghi trong MỘT giao dịch.
--   - chỉ chủ xe; gói phải còn hiệu lực (CON_HAN, chưa hết ngày)
--   - chỉ huỷ được gói CUỐI CÙNG của xe (không để lịch gói bị hở giữa chừng)
--   - gói đang dùng mà xe đang trong bãi thì chặn (huỷ xong xe sẽ thành quá hạn ngay)
CREATE PROCEDURE dbo.sp_huy_goi
    @tai_khoan_id    INT,
    @dang_ky_cho_id  INT,
    @so_tien_hoan    DECIMAL(12,0),
    @ly_do           NVARCHAR(255) = NULL,   -- lý do khách chọn (không bắt buộc)
    @gop_y           NVARCHAR(MAX) = NULL    -- MAX để bắt được chuỗi quá dài thay vì bị cắt im lặng
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF LEN(@gop_y) > 500
        THROW 50035, N'Góp ý tối đa 500 ký tự.', 1;
    SET @ly_do = NULLIF(LTRIM(RTRIM(@ly_do)), N'');
    SET @gop_y = NULLIF(LTRIM(RTRIM(@gop_y)), N'');
    DECLARE @xe_id INT, @chu INT, @loai_xe_id INT, @bat_dau DATETIME2(0), @ket_thuc DATETIME2(0),
            @so_tien DECIMAL(12,0), @bien_so VARCHAR(15), @ten_goi NVARCHAR(50), @bay_gio DATETIME2(0) = SYSDATETIME();

    BEGIN TRY
    BEGIN TRAN;
        SELECT @xe_id = d.xe_id, @chu = x.tai_khoan_id, @loai_xe_id = x.loai_xe_id, @bat_dau = d.ngay_bat_dau,
               @ket_thuc = d.ngay_ket_thuc, @so_tien = d.so_tien, @bien_so = x.bien_so, @ten_goi = g.ten
        FROM dbo.dang_ky_cho d WITH (UPDLOCK)
        JOIN dbo.xe x ON x.id = d.xe_id
        JOIN dbo.goi_cho g ON g.id = d.goi_cho_id
        WHERE d.id = @dang_ky_cho_id AND d.trang_thai = 'CON_HAN' AND d.ngay_ket_thuc > @bay_gio;
        IF @xe_id IS NULL
            THROW 50030, N'Gói không tồn tại hoặc đã hết hiệu lực.', 1;
        IF @chu <> @tai_khoan_id
            THROW 50001, N'Chỉ chủ xe được huỷ gói của xe này.', 1;

        -- cùng khoá với sp_mua_goi để số chỗ còn bán luôn nhất quán
        DECLARE @khoa INT;
        SELECT @khoa = suc_chua FROM dbo.loai_xe WITH (UPDLOCK, HOLDLOCK) WHERE id = @loai_xe_id;

        IF EXISTS (SELECT 1 FROM dbo.dang_ky_cho
                   WHERE xe_id = @xe_id AND id <> @dang_ky_cho_id AND trang_thai = 'CON_HAN' AND ngay_bat_dau > @bat_dau)
            THROW 50032, N'Chỉ huỷ được gói cuối cùng. Hãy huỷ gói gia hạn phía sau trước.', 1;
        IF @bat_dau <= @bay_gio AND EXISTS (SELECT 1 FROM dbo.xe_trong_bai WHERE xe_id = @xe_id)
            THROW 50031, N'Xe đang trong bãi. Đưa xe ra trước khi huỷ gói đang dùng.', 1;
        IF @so_tien_hoan < 0 OR @so_tien_hoan > @so_tien
            THROW 50034, N'Số tiền hoàn không hợp lệ.', 1;

        UPDATE dbo.dang_ky_cho SET trang_thai = 'HUY', huy_luc = @bay_gio, huy_boi_id = @tai_khoan_id,
                                   ly_do_huy = @ly_do, gop_y = @gop_y
        WHERE id = @dang_ky_cho_id;

        INSERT dbo.thanh_toan (tai_khoan_id, loai, dang_ky_cho_id, so_tien, trang_thai, thoi_gian)
        VALUES (@tai_khoan_id, 'HOAN_TIEN', @dang_ky_cho_id, @so_tien_hoan, 'THANH_CONG', @bay_gio);

        -- Thông báo cho khách và cho mọi Admin KHÔNG ghi ở đây nữa: bảng thong_bao thuộc module ThongBao.
        -- C# (HuyGoiUseCase) gửi qua IThongBaoModule sau khi thủ tục này thành công.
    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

-- Admin gỡ xe phạm quy: gỡ MỀM (đổi trạng thái, giữ lịch sử gói/lượt gửi/thanh toán để đối soát).
--   - chỉ gỡ xe đang ở trạng thái DA_DUYET; xe đang trong bãi thì chặn
--   - gói còn hiệu lực bị huỷ, KHÔNG hoàn tiền (lỗi do người vi phạm), suất trả lại cho bãi
CREATE PROCEDURE dbo.sp_go_xe
    @admin_id   INT,
    @xe_id      INT,
    @ly_do      NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @chu INT, @loai_xe_id INT, @bien_so VARCHAR(15), @bay_gio DATETIME2(0) = SYSDATETIME();

    BEGIN TRY
    BEGIN TRAN;
        -- Quyền Admin kiểm tra ở API ([Authorize(Roles = "ADMIN")]); thủ tục không đọc bảng tai_khoan của module TaiKhoan.
        SELECT @chu = tai_khoan_id, @loai_xe_id = loai_xe_id, @bien_so = bien_so
        FROM dbo.xe WITH (UPDLOCK) WHERE id = @xe_id AND trang_thai_duyet = 'DA_DUYET';
        IF @chu IS NULL
            THROW 50040, N'Chỉ gỡ được xe đang ở trạng thái đã duyệt.', 1;

        DECLARE @khoa INT;
        SELECT @khoa = suc_chua FROM dbo.loai_xe WITH (UPDLOCK, HOLDLOCK) WHERE id = @loai_xe_id;

        IF EXISTS (SELECT 1 FROM dbo.xe_trong_bai WHERE xe_id = @xe_id)
            THROW 50041, N'Xe đang trong bãi. Cho xe ra trước khi gỡ.', 1;
        IF LEN(LTRIM(RTRIM(ISNULL(@ly_do, N'')))) < 5
            THROW 50043, N'Lý do gỡ xe phải có ít nhất 5 ký tự.', 1;

        UPDATE dbo.xe SET trang_thai_duyet = 'BI_GO', ly_do_go = @ly_do, go_luc = @bay_gio, go_boi_id = @admin_id
        WHERE id = @xe_id;

        UPDATE dbo.dang_ky_cho SET trang_thai = 'HUY', huy_luc = @bay_gio, huy_boi_id = @admin_id,
               ly_do_huy = CONCAT(N'Xe bị gỡ: ', @ly_do)
        WHERE xe_id = @xe_id AND trang_thai = 'CON_HAN' AND ngay_ket_thuc > @bay_gio;

        -- Thông báo cho chủ xe: C# (GoXeUseCase) gửi qua IThongBaoModule sau khi thủ tục thành công.
    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

-- Admin huỷ gói (có lý do, hiện trong tài khoản khách hàng).
--   - chỉ gói CUỐI còn hiệu lực; KHÔNG chặn khi xe đang trong bãi (xe tính quá hạn từ lúc huỷ)
--   - số tiền hoàn do C# tính theo chính sách Admin chọn (hoàn phần chưa dùng / không hoàn); 0 thì không ghi hoàn tiền
CREATE PROCEDURE dbo.sp_admin_huy_goi
    @admin_id        INT,
    @dang_ky_cho_id  INT,
    @ly_do           NVARCHAR(255),
    @so_tien_hoan    DECIMAL(12,0)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @xe_id INT, @chu INT, @loai_xe_id INT, @bat_dau DATETIME2(0), @so_tien DECIMAL(12,0),
            @bien_so VARCHAR(15), @ten_goi NVARCHAR(50), @bay_gio DATETIME2(0) = SYSDATETIME();

    BEGIN TRY
    BEGIN TRAN;
        -- Quyền Admin kiểm tra ở API ([Authorize(Roles = "ADMIN")]); thủ tục không đọc bảng tai_khoan của module TaiKhoan.
        IF LEN(LTRIM(RTRIM(ISNULL(@ly_do, N'')))) < 5
            THROW 50043, N'Lý do phải có ít nhất 5 ký tự.', 1;

        SELECT @xe_id = d.xe_id, @chu = x.tai_khoan_id, @loai_xe_id = x.loai_xe_id, @bat_dau = d.ngay_bat_dau,
               @so_tien = d.so_tien, @bien_so = x.bien_so, @ten_goi = g.ten
        FROM dbo.dang_ky_cho d WITH (UPDLOCK)
        JOIN dbo.xe x ON x.id = d.xe_id
        JOIN dbo.goi_cho g ON g.id = d.goi_cho_id
        WHERE d.id = @dang_ky_cho_id AND d.trang_thai = 'CON_HAN' AND d.ngay_ket_thuc > @bay_gio;
        IF @xe_id IS NULL
            THROW 50030, N'Gói không tồn tại hoặc đã hết hiệu lực.', 1;

        DECLARE @khoa INT;
        SELECT @khoa = suc_chua FROM dbo.loai_xe WITH (UPDLOCK, HOLDLOCK) WHERE id = @loai_xe_id;

        IF EXISTS (SELECT 1 FROM dbo.dang_ky_cho
                   WHERE xe_id = @xe_id AND id <> @dang_ky_cho_id AND trang_thai = 'CON_HAN' AND ngay_bat_dau > @bat_dau)
            THROW 50032, N'Chỉ huỷ được gói cuối cùng. Hãy huỷ gói gia hạn phía sau trước.', 1;
        IF @so_tien_hoan < 0 OR @so_tien_hoan > @so_tien
            THROW 50034, N'Số tiền hoàn không hợp lệ.', 1;

        UPDATE dbo.dang_ky_cho SET trang_thai = 'HUY', ly_do_huy = @ly_do, huy_luc = @bay_gio, huy_boi_id = @admin_id
        WHERE id = @dang_ky_cho_id;

        IF @so_tien_hoan > 0   -- tiền hoàn về chủ xe
            INSERT dbo.thanh_toan (tai_khoan_id, loai, dang_ky_cho_id, so_tien, trang_thai, thoi_gian)
            VALUES (@chu, 'HOAN_TIEN', @dang_ky_cho_id, @so_tien_hoan, 'THANH_CONG', @bay_gio);

        -- Thông báo cho chủ xe: C# (AdminHuyGoiUseCase) gửi qua IThongBaoModule sau khi thủ tục thành công.
    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

-- Admin đổi sức chứa theo loại xe. Không được hạ dưới số suất đang bị chiếm
-- (xe giữ suất + xe hết gói còn trong bãi), nếu không sẽ bán vượt chỗ thật.
CREATE PROCEDURE dbo.sp_doi_suc_chua
    @admin_id    INT,
    @loai_xe_id  INT,
    @suc_chua    INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @cu INT, @dang_chiem INT, @thong_bao NVARCHAR(300);
    BEGIN TRY
    BEGIN TRAN;
        -- Quyền Admin kiểm tra ở API ([Authorize(Roles = "ADMIN")]); thủ tục không đọc bảng tai_khoan của module TaiKhoan.
        SELECT @cu = suc_chua FROM dbo.loai_xe WITH (UPDLOCK, HOLDLOCK) WHERE id = @loai_xe_id;   -- cùng khoá với sp_mua_goi
        IF @cu IS NULL
            THROW 50051, N'Loại xe không tồn tại.', 1;
        SELECT @dang_chiem = so_xe_giu_suat + so_xe_qua_han_trong_bai FROM dbo.v_cho_con_ban WHERE loai_xe_id = @loai_xe_id;
        IF @suc_chua < @dang_chiem
        BEGIN
            SET @thong_bao = CONCAT(N'Đang có ', @dang_chiem, N' suất bị chiếm, không hạ sức chứa xuống ', @suc_chua, N' được.');
            THROW 50050, @thong_bao, 1;
        END
        UPDATE dbo.loai_xe SET suc_chua = @suc_chua WHERE id = @loai_xe_id;
    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

/* =====================================================================
   PHẦN 8. DỮ LIỆU KHỞI TẠO
   Chỉ có dữ liệu để bãi xe chạy được: cấu hình, loại xe + sức chứa, gói giá, khu / ô / camera, lối đi.
   KHÔNG có tài khoản, nhóm, xe, gói đã mua, lượt gửi hay thông báo mẫu: tất cả do người dùng thật tạo ra.
   Admin đầu tiên: API tự tạo lúc khởi động nếu bảng tai_khoan chưa có ADMIN (biến AdminDauTien__*, file .env: ADMIN_*).
   Giá gói là SỐ MẪU, chưa phải bảng giá đã chốt (Admin đổi được ở trang Gói và giá).
   ===================================================================== */

DECLARE @now DATETIME2(0) = SYSDATETIME();

-- Cấu hình
INSERT dbo.cau_hinh (khoa, gia_tri, mo_ta) VALUES
 ('chu_ky_quet_phut',              N'5',    N'Chu kỳ quét toàn bộ camera (demo có thể đặt 1)'),
 ('tre_quet_sau_xe_vao_phut',      N'3',    N'Quét bổ sung sau khi có xe vào cổng'),
 ('so_lan_khong_thay_canh_bao',    N'3',    N'K lần quét liên tiếp không thấy thì vào danh sách chưa định vị'),
 ('thoi_gian_cho_xac_nhan_giay',   N'60',   N'Hết thời gian thì không mở cổng, chuyển Admin'),
 ('nhac_truoc_het_han_gio',        N'72',   N'Nhắc gia hạn trước khi gói hết hạn'),
 ('nguong_tin_cay_bien_so',        N'0.85', N'Dưới ngưỡng thì chuyển Admin xác nhận biển số'),
 ('so_xe_cho_duyet_toi_da',        N'5',    N'Mỗi tài khoản tối đa bấy nhiêu xe CHỜ DUYỆT cùng lúc (xe đã duyệt/từ chối không tính)');

-- Loại xe: sức chứa ô tô nhỏ để demo "hết chỗ"
SET IDENTITY_INSERT dbo.loai_xe ON;
INSERT dbo.loai_xe (id, ten, suc_chua, gia_qua_han_ngay) VALUES
 (1, 'O_TO',   4,  50000),
 (2, 'XE_MAY', 20, 10000);
SET IDENTITY_INSERT dbo.loai_xe OFF;

-- Gói chỗ (giá mẫu)
SET IDENTITY_INSERT dbo.goi_cho ON;
INSERT dbo.goi_cho (id, loai_xe_id, ten, so_ngay, gia) VALUES
 (1, 1, N'1 tuần',  7,   300000),
 (2, 1, N'1 tháng', 30,  1200000),
 (3, 1, N'1 năm',   365, 12000000),
 (4, 2, N'1 tuần',  7,   30000),
 (5, 2, N'1 tháng', 30,  100000),
 (6, 2, N'1 năm',   365, 1000000);
SET IDENTITY_INSERT dbo.goi_cho OFF;

-- Sơ đồ bãi: khu A, B cho ô tô (định vị tới ô), khu C cho xe máy (chỉ tới khu)
SET IDENTITY_INSERT dbo.khu_vuc ON;
INSERT dbo.khu_vuc (id, ten, loai_xe_id, toa_do_so_do) VALUES
 (1, N'Khu A', 1, N'{"x":40,"y":40,"w":240,"h":100}'),
 (2, N'Khu B', 1, N'{"x":40,"y":180,"w":240,"h":100}'),
 (3, N'Khu C', 2, N'{"x":320,"y":40,"w":160,"h":240}');
SET IDENTITY_INSERT dbo.khu_vuc OFF;

SET IDENTITY_INSERT dbo.camera ON;
INSERT dbo.camera (id, khu_vuc_id, ten, nguon_anh, dang_bat) VALUES
 (1, 1, N'CAM-A1',        '/demo/camera/cam-a1.jpg', 1),
 (2, 2, N'CAM-B1',        '/demo/camera/cam-b1.jpg', 1),
 (3, 3, N'CAM-C1',        '/demo/camera/cam-c1.jpg', 1),
 (4, 3, N'CAM-C-LOI-VAO', '/demo/camera/cam-c-loi-vao.jpg', 1);  -- camera lối vào khu xe máy
SET IDENTITY_INSERT dbo.camera OFF;

SET IDENTITY_INSERT dbo.o_do ON;
INSERT dbo.o_do (id, khu_vuc_id, camera_id, ma_o, vung_tren_anh, toa_do_so_do, ma_qr) VALUES
 (1, 1, 1, 'A01', N'[[0,300],[320,300],[320,720],[0,720]]',     N'{"x":40,"y":40,"w":60,"h":100}',  'QR-A01'),
 (2, 1, 1, 'A02', N'[[320,300],[640,300],[640,720],[320,720]]', N'{"x":100,"y":40,"w":60,"h":100}', 'QR-A02'),
 (3, 1, 1, 'A03', N'[[640,300],[960,300],[960,720],[640,720]]', N'{"x":160,"y":40,"w":60,"h":100}', 'QR-A03'),
 (4, 1, 1, 'A04', N'[[960,300],[1280,300],[1280,720],[960,720]]',N'{"x":220,"y":40,"w":60,"h":100}', 'QR-A04'),
 (5, 2, 2, 'B01', N'[[0,300],[320,300],[320,720],[0,720]]',     N'{"x":40,"y":180,"w":60,"h":100}', 'QR-B01'),
 (6, 2, 2, 'B02', N'[[320,300],[640,300],[640,720],[320,720]]', N'{"x":100,"y":180,"w":60,"h":100}','QR-B02'),
 (7, 2, 2, 'B03', N'[[640,300],[960,300],[960,720],[640,720]]', N'{"x":160,"y":180,"w":60,"h":100}','QR-B03'),
 (8, 2, 2, 'B04', N'[[960,300],[1280,300],[1280,720],[960,720]]',N'{"x":220,"y":180,"w":60,"h":100}','QR-B04');
SET IDENTITY_INSERT dbo.o_do OFF;

-- Đồ thị lối đi để dẫn đường (Dijkstra/BFS ở backend)
SET IDENTITY_INSERT dbo.nut_duong_di ON;
INSERT dbo.nut_duong_di (id, ten, toa_do, khu_vuc_id) VALUES
 (1, N'Lối vào',     N'{"x":20,"y":160}',  NULL),
 (2, N'Ngã ba giữa', N'{"x":300,"y":160}', NULL),
 (3, N'Đầu khu A',   N'{"x":160,"y":150}', 1),
 (4, N'Đầu khu B',   N'{"x":160,"y":170}', 2),
 (5, N'Đầu khu C',   N'{"x":320,"y":160}', 3),
 (6, N'Thang máy',   N'{"x":300,"y":300}', NULL);
SET IDENTITY_INSERT dbo.nut_duong_di OFF;

INSERT dbo.canh_duong_di (nut_a_id, nut_b_id, do_dai) VALUES
 (1, 3, 15.0), (1, 4, 15.0), (2, 3, 15.0), (2, 4, 15.0), (2, 5, 5.0), (2, 6, 14.0);

-- BaiXe: bãi mới, mọi ô đều trống
INSERT dbo.trang_thai_o (o_do_id, trang_thai, nguon, cap_nhat_luc)
SELECT id, 'TRONG', 'ADMIN', @now FROM dbo.o_do;
GO

/* =====================================================================
   PHẦN 9. TRUY VẤN KIỂM TRA (bôi đen từng câu rồi F5)
   CSDL mới không có tài khoản / xe nào: đăng ký và thêm xe trên giao diện trước, rồi thay <id> bên dưới.
   ===================================================================== */
/*
-- Tài khoản đã đăng ký (mật khẩu chỉ lưu bcrypt)
SELECT id, ho_ten, sdt, email, vai_tro, trang_thai, tao_luc, dang_nhap_luc FROM dbo.tai_khoan ORDER BY id;

-- Chỗ còn bán theo loại xe
SELECT * FROM dbo.v_cho_con_ban;

-- Xe một tài khoản được dùng (của mình + xe được chia sẻ trong nhóm)
SELECT q.*, x.bien_so FROM dbo.fn_xe_duoc_dung(<id tài khoản>) q JOIN dbo.xe x ON x.id = q.xe_id;

-- Đối soát: xe đang gửi nhưng chưa định vị được
SELECT * FROM dbo.v_vi_tri_xe_hien_tai WHERE tinh_trang = N'Chưa định vị';

-- Mua gói bằng thủ tục (giống API làm)
DECLARE @id INT; EXEC dbo.sp_mua_goi @tai_khoan_id = <id tài khoản>, @xe_id = <id xe>, @goi_cho_id = 4, @dang_ky_cho_id = @id OUTPUT;
SELECT * FROM dbo.dang_ky_cho WHERE id = @id;
*/
