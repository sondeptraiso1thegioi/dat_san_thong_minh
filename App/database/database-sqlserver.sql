-- =====================================================================
-- HỆ THỐNG ĐẶT SÂN THỂ THAO THÔNG MINH - SQL SERVER 2022+ / SSMS
-- 1. Trong SSMS, tạo database trống DatSanThongMinh (hoặc tên bạn chọn).
-- 2. Chọn đúng database đó ở thanh công cụ, mở file này và nhấn F5.
-- Script không tạo/xóa database, không xóa dữ liệu đã có, không cần GO.
-- Tiền: BIGINT VNĐ. Tiếng Việt: NVARCHAR / N'...'. Thời điểm: DATETIMEOFFSET(3).
-- Đây là schema chính của dự án; database.sql là bản PostgreSQL tham khảo.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51000, N'Hãy tạo và chọn database dự án trống trước khi chạy file.', 1;
IF CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) < 16
    THROW 51000, N'Script yêu cầu SQL Server 2022 (16.x) trở lên.', 1;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
    THROW 51000, N'Database đã có bảng. Hãy chọn database trống; script không ghi đè dữ liệu.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Mỗi tài khoản một vai trò. Bcrypt do backend tạo khi đăng ký.
    CREATE TABLE dbo.users (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_users PRIMARY KEY,
        name NVARCHAR(100) NOT NULL,
        email NVARCHAR(150) NOT NULL CONSTRAINT uq_users_email UNIQUE,
        phone VARCHAR(20) NULL,
        password_hash VARCHAR(100) NOT NULL,
        role VARCHAR(10) NOT NULL CONSTRAINT ck_users_role CHECK (role IN ('admin','owner','player')),
        status VARCHAR(10) NOT NULL CONSTRAINT df_users_status DEFAULT 'active'
            CONSTRAINT ck_users_status CHECK (status IN ('active','locked')),
        address NVARCHAR(255) NULL,
        lat FLOAT NULL CONSTRAINT ck_users_lat CHECK (lat BETWEEN -90 AND 90),
        lng FLOAT NULL CONSTRAINT ck_users_lng CHECK (lng BETWEEN -180 AND 180),
        created_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_users_created DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
    );
    -- SQL Server UNIQUE thông thường chỉ cho một NULL; index lọc cho phép nhiều người chưa có SĐT.
    CREATE UNIQUE INDEX uq_users_phone ON dbo.users(phone) WHERE phone IS NOT NULL;

    -- 2. Cụm sân, ảnh và tiện ích là JSON dạng mảng.
    CREATE TABLE dbo.venues (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_venues PRIMARY KEY,
        owner_id BIGINT NOT NULL CONSTRAINT fk_venues_owner REFERENCES dbo.users(id),
        name NVARCHAR(150) NOT NULL,
        address NVARCHAR(255) NOT NULL,
        lat FLOAT NOT NULL CONSTRAINT ck_venues_lat CHECK (lat BETWEEN -90 AND 90),
        lng FLOAT NOT NULL CONSTRAINT ck_venues_lng CHECK (lng BETWEEN -180 AND 180),
        open_time TIME(0) NOT NULL,
        close_time TIME(0) NOT NULL,
        amenities NVARCHAR(MAX) NOT NULL CONSTRAINT df_venues_amenities DEFAULT N'[]',
        photos NVARCHAR(MAX) NOT NULL CONSTRAINT df_venues_photos DEFAULT N'[]',
        description NVARCHAR(MAX) NULL,
        status VARCHAR(10) NOT NULL CONSTRAINT df_venues_status DEFAULT 'pending'
            CONSTRAINT ck_venues_status CHECK (status IN ('pending','approved','rejected','deleted')),
        reject_reason NVARCHAR(MAX) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_venues_created DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        CONSTRAINT ck_venues_hours CHECK (close_time > open_time),
        CONSTRAINT ck_venues_amenities CHECK (ISJSON(amenities, ARRAY) = 1),
        CONSTRAINT ck_venues_photos CHECK (ISJSON(photos, ARRAY) = 1)
    );
    CREATE INDEX idx_venues_status ON dbo.venues(status);

    -- 3. Sân con.
    CREATE TABLE dbo.courts (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_courts PRIMARY KEY,
        venue_id BIGINT NOT NULL CONSTRAINT fk_courts_venue REFERENCES dbo.venues(id),
        sport VARCHAR(15) NOT NULL
            CONSTRAINT ck_courts_sport CHECK (sport IN ('tennis','pickleball','badminton','table_tennis')),
        name NVARCHAR(50) NOT NULL,
        environment VARCHAR(10) NOT NULL
            CONSTRAINT ck_courts_environment CHECK (environment IN ('indoor','covered','outdoor')),
        is_active BIT NOT NULL CONSTRAINT df_courts_active DEFAULT 1,
        CONSTRAINT uq_courts_venue_name UNIQUE (venue_id, name)
    );
    CREATE INDEX idx_courts_venue_sport ON dbo.courts(venue_id, sport);

    -- 4. Giá theo thứ (0 = Chủ nhật), môn và giờ. Trigger bên dưới chặn chồng giá.
    CREATE TABLE dbo.price_rules (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_price_rules PRIMARY KEY,
        venue_id BIGINT NOT NULL CONSTRAINT fk_prices_venue REFERENCES dbo.venues(id),
        sport VARCHAR(15) NOT NULL
            CONSTRAINT ck_prices_sport CHECK (sport IN ('tennis','pickleball','badminton','table_tennis')),
        weekday SMALLINT NOT NULL CONSTRAINT ck_prices_weekday CHECK (weekday BETWEEN 0 AND 6),
        start_hour SMALLINT NOT NULL CONSTRAINT ck_prices_start CHECK (start_hour BETWEEN 0 AND 23),
        end_hour SMALLINT NOT NULL CONSTRAINT ck_prices_end CHECK (end_hour BETWEEN 1 AND 24),
        price_per_hour BIGINT NOT NULL CONSTRAINT ck_prices_amount CHECK (price_per_hour >= 0),
        CONSTRAINT ck_prices_hours CHECK (end_hour > start_hour)
    );
    CREATE INDEX idx_price_rules_lookup ON dbo.price_rules(venue_id, sport, weekday, start_hour);

    -- 5. Đặt sân. Pending giữ chỗ; BackgroundService chuyển booking quá hạn sang cancelled.
    CREATE TABLE dbo.bookings (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_bookings PRIMARY KEY,
        user_id BIGINT NOT NULL CONSTRAINT fk_bookings_user REFERENCES dbo.users(id),
        court_id BIGINT NOT NULL CONSTRAINT fk_bookings_court REFERENCES dbo.courts(id),
        start_at DATETIMEOFFSET(3) NOT NULL,
        end_at DATETIMEOFFSET(3) NOT NULL,
        status VARCHAR(10) NOT NULL CONSTRAINT df_bookings_status DEFAULT 'pending'
            CONSTRAINT ck_bookings_status CHECK (status IN ('pending','confirmed','cancelled','completed')),
        price BIGINT NOT NULL CONSTRAINT ck_bookings_price CHECK (price >= 0),
        source VARCHAR(10) NOT NULL CONSTRAINT df_bookings_source DEFAULT 'manual'
            CONSTRAINT ck_bookings_source CHECK (source IN ('manual','ai')),
        hold_expires_at DATETIMEOFFSET(3) NULL,
        cancel_reason NVARCHAR(MAX) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_bookings_created DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        CONSTRAINT ck_bookings_period CHECK (end_at > start_at),
        CONSTRAINT ck_bookings_pending_hold CHECK (
            status <> 'pending' OR (hold_expires_at IS NOT NULL AND hold_expires_at > created_at)
        )
    );
    CREATE INDEX idx_bookings_user ON dbo.bookings(user_id, start_at DESC);
    CREATE INDEX idx_bookings_court_time ON dbo.bookings(court_id, start_at) INCLUDE (end_at, status);

    -- 6. Gói VIP.
    CREATE TABLE dbo.vip_plans (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_vip_plans PRIMARY KEY,
        name NVARCHAR(50) NOT NULL,
        duration_days INT NOT NULL CONSTRAINT ck_vip_plans_duration CHECK (duration_days > 0),
        price BIGINT NOT NULL CONSTRAINT ck_vip_plans_price CHECK (price >= 0),
        is_active BIT NOT NULL CONSTRAINT df_vip_plans_active DEFAULT 1
    );

    -- 7. Lượt mua VIP.
    CREATE TABLE dbo.vip_subscriptions (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_vip_subscriptions PRIMARY KEY,
        venue_id BIGINT NOT NULL CONSTRAINT fk_vip_subs_venue REFERENCES dbo.venues(id),
        plan_id BIGINT NOT NULL CONSTRAINT fk_vip_subs_plan REFERENCES dbo.vip_plans(id),
        start_at DATETIMEOFFSET(3) NULL,
        end_at DATETIMEOFFSET(3) NULL,
        status VARCHAR(10) NOT NULL CONSTRAINT df_vip_subs_status DEFAULT 'pending'
            CONSTRAINT ck_vip_subs_status CHECK (status IN ('pending','active','expired','cancelled')),
        created_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_vip_subs_created DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        CONSTRAINT ck_vip_subs_period CHECK (
            (start_at IS NULL AND end_at IS NULL)
            OR (start_at IS NOT NULL AND end_at IS NOT NULL AND end_at > start_at)
        ),
        CONSTRAINT ck_vip_subs_active_period CHECK (
            status NOT IN ('active','expired') OR (start_at IS NOT NULL AND end_at IS NOT NULL)
        )
    );
    CREATE INDEX idx_vip_active ON dbo.vip_subscriptions(venue_id, end_at) WHERE status = 'active';

    -- 8. Thanh toán. refund_pending = đã nhận tiền, cần hoàn; refunded = đã hoàn.
    CREATE TABLE dbo.payments (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_payments PRIMARY KEY,
        type VARCHAR(10) NOT NULL CONSTRAINT ck_payments_type CHECK (type IN ('booking','vip')),
        booking_id BIGINT NULL CONSTRAINT fk_payments_booking REFERENCES dbo.bookings(id),
        vip_subscription_id BIGINT NULL CONSTRAINT fk_payments_vip REFERENCES dbo.vip_subscriptions(id),
        amount BIGINT NOT NULL CONSTRAINT ck_payments_amount CHECK (amount > 0),
        commission_rate DECIMAL(5,4) NULL
            CONSTRAINT ck_payments_rate CHECK (commission_rate BETWEEN 0 AND 1),
        commission_amount BIGINT NOT NULL CONSTRAINT df_payments_commission DEFAULT 0
            CONSTRAINT ck_payments_commission CHECK (commission_amount >= 0),
        owner_amount BIGINT NOT NULL CONSTRAINT df_payments_owner DEFAULT 0
            CONSTRAINT ck_payments_owner CHECK (owner_amount >= 0),
        status VARCHAR(20) NOT NULL CONSTRAINT df_payments_status DEFAULT 'pending'
            CONSTRAINT ck_payments_status CHECK (status IN ('pending','paid','failed','refund_pending','refunded')),
        payout_status VARCHAR(10) NOT NULL CONSTRAINT df_payments_payout DEFAULT 'none'
            CONSTRAINT ck_payments_payout CHECK (payout_status IN ('none','pending','paid')),
        -- Tên cột cũ giữ tương thích DB; gateway giả lập lưu orderId, billId, mã kết quả.
        vnp_txn_ref VARCHAR(100) NOT NULL CONSTRAINT uq_payments_txn_ref UNIQUE,
        vnp_transaction_no VARCHAR(100) NULL,
        vnp_response_code VARCHAR(10) NULL,
        paid_at DATETIMEOFFSET(3) NULL,
        created_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_payments_created DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        CONSTRAINT ck_payments_target CHECK (
            (type = 'booking' AND booking_id IS NOT NULL AND vip_subscription_id IS NULL)
            OR (type = 'vip' AND vip_subscription_id IS NOT NULL AND booking_id IS NULL)
        ),
        CONSTRAINT ck_payments_received CHECK (
            status NOT IN ('paid','refund_pending','refunded')
            OR (paid_at IS NOT NULL AND commission_amount + owner_amount = amount)
        ),
        CONSTRAINT ck_payments_booking_commission CHECK (
            type <> 'booking' OR status NOT IN ('paid','refund_pending','refunded')
            OR (commission_rate IS NOT NULL
                AND commission_amount = CONVERT(BIGINT, ROUND(amount * commission_rate, 0)))
        ),
        CONSTRAINT ck_payments_vip_revenue CHECK (
            type <> 'vip' OR (owner_amount = 0 AND payout_status = 'none')
        ),
        CONSTRAINT ck_payments_payout_state CHECK (
            type = 'vip'
            OR (status = 'paid' AND payout_status IN ('pending','paid'))
            OR (status IN ('pending','failed') AND payout_status = 'none')
            OR (status IN ('refund_pending','refunded') AND payout_status IN ('none','paid'))
        )
    );
    CREATE INDEX idx_payments_payout ON dbo.payments(payout_status) WHERE payout_status = 'pending';
    CREATE UNIQUE INDEX payments_one_paid_booking ON dbo.payments(booking_id)
        WHERE type = 'booking' AND status = 'paid' AND booking_id IS NOT NULL;
    CREATE UNIQUE INDEX payments_one_paid_vip ON dbo.payments(vip_subscription_id)
        WHERE type = 'vip' AND status = 'paid' AND vip_subscription_id IS NOT NULL;

    -- 9. Cache thời tiết theo giờ.
    CREATE TABLE dbo.weather_cache (
        venue_id BIGINT NOT NULL CONSTRAINT fk_weather_venue REFERENCES dbo.venues(id),
        forecast_hour DATETIMEOFFSET(3) NOT NULL,
        rain_prob SMALLINT NULL CONSTRAINT ck_weather_rain CHECK (rain_prob BETWEEN 0 AND 100),
        temp_c DECIMAL(4,1) NULL,
        wind_kmh DECIMAL(5,1) NULL,
        uv_index DECIMAL(4,1) NULL,
        fetched_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_weather_fetched DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        CONSTRAINT pk_weather_cache PRIMARY KEY (venue_id, forecast_hour)
    );

    -- 10. Nhật ký gợi ý. EF Core đọc JSON dưới dạng string; backend dùng System.Text.Json.
    CREATE TABLE dbo.suggestion_logs (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_suggestion_logs PRIMARY KEY,
        user_id BIGINT NOT NULL CONSTRAINT fk_suggestions_user REFERENCES dbo.users(id),
        [query] NVARCHAR(MAX) NOT NULL CONSTRAINT ck_suggestions_query CHECK (ISJSON([query], OBJECT) = 1),
        shown_items NVARCHAR(MAX) NOT NULL CONSTRAINT ck_suggestions_items CHECK (ISJSON(shown_items, ARRAY) = 1),
        chosen_item NVARCHAR(MAX) NULL
            CONSTRAINT ck_suggestions_chosen CHECK (chosen_item IS NULL OR ISJSON(chosen_item, OBJECT) = 1),
        created_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_suggestions_created DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
    );
    CREATE INDEX idx_suggestion_logs_user ON dbo.suggestion_logs(user_id, created_at DESC);

    -- 11. Cấu hình. VALUE chấp nhận cả object, array và scalar như 5 hoặc 0.08.
    CREATE TABLE dbo.ai_settings (
        [key] VARCHAR(50) NOT NULL CONSTRAINT pk_ai_settings PRIMARY KEY,
        [value] NVARCHAR(MAX) NOT NULL CONSTRAINT ck_ai_settings_json CHECK (ISJSON([value], VALUE) = 1),
        description NVARCHAR(MAX) NULL,
        updated_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_settings_updated DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
    );

    -- 12. Báo cáo vi phạm.
    CREATE TABLE dbo.reports (
        id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT pk_reports PRIMARY KEY,
        reporter_id BIGINT NOT NULL CONSTRAINT fk_reports_reporter REFERENCES dbo.users(id),
        target_type VARCHAR(10) NOT NULL CONSTRAINT ck_reports_type CHECK (target_type IN ('user','venue','booking')),
        target_id BIGINT NOT NULL,
        reason NVARCHAR(MAX) NOT NULL,
        status VARCHAR(10) NOT NULL CONSTRAINT df_reports_status DEFAULT 'open'
            CONSTRAINT ck_reports_status CHECK (status IN ('open','resolved','rejected')),
        created_at DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT df_reports_created DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
    );

    -- Dynamic batch giữ CREATE FUNCTION/TRIGGER là câu đầu batch, vẫn cùng transaction.
    EXEC sys.sp_executesql N'
    CREATE FUNCTION dbo.distance_km (@lat1 FLOAT, @lng1 FLOAT, @lat2 FLOAT, @lng2 FLOAT)
    RETURNS FLOAT WITH SCHEMABINDING AS
    BEGIN
        DECLARE @a FLOAT = POWER(SIN(RADIANS(@lat2 - @lat1) / 2.0), 2)
            + COS(RADIANS(@lat1)) * COS(RADIANS(@lat2))
            * POWER(SIN(RADIANS(@lng2 - @lng1) / 2.0), 2);
        RETURN 2.0 * 6371.0 * ASIN(SQRT(CASE WHEN @a < 0 THEN 0 WHEN @a > 1 THEN 1 ELSE @a END));
    END;';

    -- INSERT/UPDATE nhiều dòng đều được kiểm tra; khoảng giờ là [start, end).
    -- UPDLOCK/HOLDLOCK bảo vệ cả khoảng khóa khi có ghi đồng thời.
    -- Backend khóa court trước khi ghi trong transaction để giảm deadlock; retry lỗi 1205.
    EXEC sys.sp_executesql N'
    CREATE TRIGGER dbo.trg_bookings_no_overlap ON dbo.bookings AFTER INSERT, UPDATE AS
    BEGIN
        IF ROWCOUNT_BIG() = 0 RETURN;
        SET NOCOUNT ON;
        IF EXISTS (
            SELECT 1 FROM inserted AS i
            JOIN dbo.bookings AS b WITH (UPDLOCK, HOLDLOCK)
                ON b.court_id = i.court_id AND b.id <> i.id
                AND b.start_at < i.end_at AND i.start_at < b.end_at
            WHERE i.status IN (''pending'',''confirmed'') AND b.status IN (''pending'',''confirmed'')
        )
            THROW 51001, N''BOOKING_OVERLAP: Sân đã có người đặt trong khoảng giờ này.'', 1;
    END;';

    EXEC sys.sp_executesql N'
    CREATE TRIGGER dbo.trg_price_rules_no_overlap ON dbo.price_rules AFTER INSERT, UPDATE AS
    BEGIN
        IF ROWCOUNT_BIG() = 0 RETURN;
        SET NOCOUNT ON;
        IF EXISTS (
            SELECT 1 FROM inserted AS i
            JOIN dbo.price_rules AS p WITH (UPDLOCK, HOLDLOCK)
                ON p.venue_id = i.venue_id AND p.sport = i.sport AND p.weekday = i.weekday
                AND p.id <> i.id AND p.start_hour < i.end_hour AND i.start_hour < p.end_hour
        )
            THROW 51002, N''PRICE_RULE_OVERLAP: Khung giá bị chồng giờ cho cùng cụm sân, môn và thứ.'', 1;
    END;';

    -- DỮ LIỆU DEMO. Mật khẩu 123456 đã băm bcrypt (cost 10), dùng cho cả 5 tài khoản.
    -- Tài khoản đăng ký mới: backend dùng bcrypt; không dùng HASHBYTES để thay bcrypt.
    DECLARE @demo_hash VARCHAR(100) = '$2a$10$mF8rVAuOAbWtmHtuaOWvjuC3NFqZHhhu2O0oMxw3s/jOg2tS5QMg2';
    DECLARE @now DATETIMEOFFSET(3) = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');
    DECLARE @today_vn DATETIMEOFFSET(3) = TODATETIMEOFFSET(
        CONVERT(DATETIME2(3), CONVERT(DATE, SWITCHOFFSET(@now, '+07:00'))), '+07:00');

    INSERT INTO dbo.users (name, email, phone, password_hash, role, address, lat, lng) VALUES
        (N'Quản trị viên', N'admin@datsan.vn', '0900000001', @demo_hash, 'admin', NULL, NULL, NULL),
        (N'Chủ sân A', N'owner1@datsan.vn', '0900000002', @demo_hash, 'owner', NULL, NULL, NULL),
        (N'Chủ sân B', N'owner2@datsan.vn', '0900000003', @demo_hash, 'owner', NULL, NULL, NULL),
        (N'Người chơi An', N'an@datsan.vn', '0900000004', @demo_hash, 'player', N'Cầu Giấy, Hà Nội', 21.0362, 105.7906),
        (N'Người chơi Bình', N'binh@datsan.vn', '0900000005', @demo_hash, 'player', N'Đống Đa, Hà Nội', 21.0136, 105.8240);

    INSERT INTO dbo.venues (owner_id, name, address, lat, lng, open_time, close_time, amenities, status) VALUES
        (2, N'Cụm sân A', N'Cầu Giấy, Hà Nội', 21.0450, 105.7980, '06:00', '22:00', N'["gui_xe","nuoc_uong"]', 'approved'),
        (2, N'Cụm sân B', N'Ba Đình, Hà Nội', 21.0340, 105.8150, '06:00', '23:00', N'["gui_xe","phong_thay_do","cho_thue_vot"]', 'approved'),
        (3, N'Cụm sân C', N'Tây Hồ, Hà Nội', 21.0700, 105.8200, '05:30', '22:00', N'["gui_xe","phong_thay_do","nha_tam"]', 'approved'),
        (3, N'Cụm sân D', N'Thanh Xuân, Hà Nội', 20.9950, 105.8100, '06:00', '22:00', N'[]', 'pending');

    INSERT INTO dbo.courts (venue_id, sport, name, environment) VALUES
        (1, 'tennis', N'Tennis 1', 'outdoor'), (1, 'tennis', N'Tennis 2', 'outdoor'),
        (2, 'tennis', N'Tennis mái 1', 'covered'), (2, 'pickleball', N'Pickleball 1', 'covered'),
        (2, 'badminton', N'Cầu lông 1', 'indoor'), (3, 'tennis', N'Tennis trong nhà', 'indoor'),
        (3, 'badminton', N'Cầu lông 1', 'indoor'), (3, 'badminton', N'Cầu lông 2', 'indoor'),
        (3, 'table_tennis', N'Bóng bàn 1', 'indoor'), (4, 'pickleball', N'Pickleball 1', 'outdoor');

    INSERT INTO dbo.price_rules (venue_id, sport, weekday, start_hour, end_hour, price_per_hour)
    SELECT v.venue_id, v.sport, d.weekday, r.start_hour, r.end_hour,
        CASE WHEN r.is_peak = 1 THEN v.peak_price ELSE v.normal_price END
    FROM (VALUES (1,'tennis',120000,150000), (2,'tennis',160000,200000),
        (2,'pickleball',100000,130000), (2,'badminton',80000,120000),
        (3,'tennis',250000,300000), (3,'badminton',90000,120000),
        (3,'table_tennis',50000,70000), (4,'pickleball',90000,110000))
        AS v(venue_id, sport, normal_price, peak_price)
    CROSS JOIN (VALUES (0),(1),(2),(3),(4),(5),(6)) AS d(weekday)
    CROSS JOIN (VALUES (5,17,0),(17,21,1),(21,24,0)) AS r(start_hour,end_hour,is_peak);

    INSERT INTO dbo.vip_plans (name, duration_days, price) VALUES
        (N'VIP 7 ngày', 7, 99000), (N'VIP 30 ngày', 30, 299000);
    INSERT INTO dbo.vip_subscriptions (venue_id, plan_id, start_at, end_at, status, created_at) VALUES
        (3, 2, DATEADD(day,-5,@now), DATEADD(day,25,@now), 'active', DATEADD(day,-5,@now));
    INSERT INTO dbo.payments (type, vip_subscription_id, amount, commission_amount, owner_amount,
        status, vnp_txn_ref, vnp_transaction_no, vnp_response_code, paid_at, created_at) VALUES
        ('vip', 1, 299000, 299000, 0, 'paid', 'VIP-0001', '14000001', '00', DATEADD(day,-5,@now), DATEADD(day,-5,@now));

    INSERT INTO dbo.bookings (user_id, court_id, start_at, end_at, status, price, source, created_at) VALUES
        (4,1,DATEADD(hour,19,DATEADD(day,-21,@today_vn)),DATEADD(hour,20,DATEADD(day,-21,@today_vn)),
            'completed',150000,'manual',DATEADD(hour,17,DATEADD(day,-21,@today_vn))),
        (4,1,DATEADD(hour,19,DATEADD(day,-14,@today_vn)),DATEADD(hour,20,DATEADD(day,-14,@today_vn)),
            'completed',150000,'manual',DATEADD(hour,17,DATEADD(day,-14,@today_vn))),
        (4,1,DATEADD(hour,19,DATEADD(day,-7,@today_vn)),DATEADD(hour,20,DATEADD(day,-7,@today_vn)),
            'completed',150000,'ai',DATEADD(hour,17,DATEADD(day,-7,@today_vn))),
        (5,7,DATEADD(hour,18,DATEADD(day,1,@today_vn)),DATEADD(hour,19,DATEADD(day,1,@today_vn)),
            'confirmed',120000,'manual',@now);
    INSERT INTO dbo.payments (type, booking_id, amount, commission_rate, commission_amount, owner_amount,
        status, payout_status, vnp_txn_ref, vnp_transaction_no, vnp_response_code, paid_at, created_at)
    SELECT 'booking', b.id, b.price, 0.08, CONVERT(BIGINT,ROUND(b.price * 0.08,0)),
        b.price - CONVERT(BIGINT,ROUND(b.price * 0.08,0)), 'paid', 'pending',
        'BK-' + RIGHT('0000' + CONVERT(VARCHAR(20),b.id),4), '1500000' + CONVERT(VARCHAR(20),b.id),
        '00', b.created_at, b.created_at
    FROM dbo.bookings AS b;

    INSERT INTO dbo.ai_settings ([key], [value], description) VALUES
        ('weights',N'{"distance":0.30,"time":0.25,"weather":0.25,"habit":0.10,"price":0.10}',N'Trọng số chấm điểm gợi ý'),
        ('default_radius_km',N'5',N'Bán kính mặc định (km)'),
        ('time_window_hours',N'2',N'Khoảng giờ tìm quanh giờ muốn chơi'),
        ('top_k',N'5',N'Số gợi ý trả về'),
        ('weather_rules',N'{"rain_prob_threshold":50,"hot_temp_c":35,"uv_index_threshold":8,"wind_kmh_threshold":30,
            "nice_temp_min":20,"nice_temp_max":32,"score":{"rain":{"sheltered":1.0,"outdoor":0.0},
            "hot":{"sheltered":0.8,"outdoor":0.3},"windy":{"sheltered":0.8,"outdoor":0.3},
            "nice":{"sheltered":0.6,"outdoor":1.0}}}',N'Ngưỡng và điểm thời tiết'),
        ('commission_rate',N'0.08',N'Tỷ lệ hoa hồng'),
        ('booking_hold_minutes',N'10',N'Số phút giữ chỗ');

    COMMIT TRANSACTION;
    SELECT DB_NAME() AS database_name, N'Khởi tạo thành công' AS result,
        (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) AS table_count;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- TRUY VẤN THAM KHẢO (chạy riêng sau khi khởi tạo):
-- SELECT TOP (10) * FROM dbo.venues;
-- SELECT dbo.distance_km(21.0362,105.7906,21.0450,105.7980) AS distance_km;
-- SELECT [key], [value] FROM dbo.ai_settings;
-- SELECT v.owner_id, SUM(p.owner_amount) AS owner_payable
-- FROM dbo.payments p JOIN dbo.bookings b ON b.id = p.booking_id
-- JOIN dbo.courts c ON c.id = b.court_id JOIN dbo.venues v ON v.id = c.venue_id
-- WHERE p.status = 'paid' AND p.payout_status = 'pending' GROUP BY v.owner_id;
