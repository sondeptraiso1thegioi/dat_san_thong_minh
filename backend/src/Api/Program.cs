using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NhaXe.Api.Common;
using NhaXe.Modules.AiBienSo.Infrastructure;
using NhaXe.Modules.BaiXe.Infrastructure;
using NhaXe.Modules.CongRaVao.Infrastructure;
using NhaXe.Modules.NhomGiaDinh.Infrastructure;
using NhaXe.Modules.GoiCho.Infrastructure;
using NhaXe.Modules.ThongBao.Infrastructure;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.Modules.TaiKhoan.Infrastructure;
using NhaXe.SharedKernel;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Bí mật (chuỗi kết nối, khoá JWT) đọc từ biến môi trường / .env, KHÔNG viết trong mã nguồn (Buổi 11).
var connStr = cfg.GetConnectionString("NhaXe") ?? throw new InvalidOperationException("Thiếu ConnectionStrings__NhaXe");
var jwt = new JwtOptions(
    cfg["Jwt:Key"] ?? throw new InvalidOperationException("Thiếu Jwt__Key"),
    cfg["Jwt:Issuer"] ?? "nhaxe",
    int.TryParse(cfg["Jwt:SoGioHetHan"], out var h) ? h : 12);

// Modular Monolith (Buổi 12): mỗi module tự đăng ký phần của mình, Program.cs chỉ lắp ráp.
builder.Services.AddSingleton<IClock, DongHoHeThong>();
builder.Services.AddTaiKhoanModule(connStr, jwt, new SmtpOptions(
    cfg["Smtp:Host"] ?? "localhost", cfg.GetValue("Smtp:Port", 1025),
    cfg["Smtp:From"] ?? "noreply@nhaxe.local", cfg.GetValue("Smtp:EnableSsl", false), cfg["Smtp:User"], cfg["Smtp:Password"]),
    // Không còn tài khoản demo: admin đầu tiên tạo từ cấu hình khi CSDL chưa có admin nào (.env: ADMIN_*)
    new AdminDauTienOptions(cfg["AdminDauTien:HoTen"], cfg["AdminDauTien:Sdt"], cfg["AdminDauTien:Email"], cfg["AdminDauTien:MatKhau"]));
// AI biển số: Model A (YOLOv8n) + PaddleOCR chạy bằng ONNX Runtime ngay trong API .NET; file mô hình ở thư mục models
builder.Services.AddAiBienSoModule(cfg["AiBienSo:ThuMucMoHinh"] ?? Path.Combine(AppContext.BaseDirectory, "models"));
// Thư mục lưu ảnh giấy đăng ký: trong Docker là volume "uploads" dùng chung cho mọi container API
var thuMucTep = cfg["LuuTru:ThuMuc"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
// Sự kiện giữa các module (CongRaVao phát XeDaVao / XeDaRa, GoiCho nghe), chạy trong cùng tiến trình
builder.Services.AddScoped<IPhatSuKien, PhatSuKienTrongTienTrinh>();
// GoiCho: xe, gói; nghe sự kiện cổng để giữ bản sao xe_trong_bai; quét nhắc gia hạn 72h mỗi phút
builder.Services.AddGoiChoModule(connStr, thuMucTep, TimeSpan.FromMinutes(1));
builder.Services.AddNhomGiaDinhModule(connStr); // quản lý nhóm; GoiCho hỏi thành viên qua Contracts
// CongRaVao: xe vào / ra, xác nhận kép 60 giây; quét yêu cầu quá hạn mỗi 5 giây
builder.Services.AddCongRaVaoModule(connStr, TimeSpan.FromSeconds(5), thuMucTep);
builder.Services.AddThongBaoModule(connStr);    // hộp thư người dùng + Admin; module khác gửi qua IThongBaoModule
builder.Services.AddBaiXeModule(connStr);       // sơ đồ bãi, quản lý khu / ô / camera, chỗ trống, đường đi (Dijkstra)
// TimXe (xe máy): nghe XeDaVao để chỉ khu + giữ suất 5 phút; camera lối vào khu; tìm xe. Trả suất quá hạn mỗi 30 giây.
NhaXe.Modules.TimXe.Infrastructure.DependencyInjection.AddTimXeModule(builder.Services, connStr, TimeSpan.FromSeconds(30));
NhaXe.Modules.BaoCao.Application.DependencyInjection.AddBaoCaoModule(builder.Services); // chỉ hỏi module khác qua Contracts
// Thông báo thời gian thực (SignalR): ThongBao lưu hộp thư xong thì đẩy tới máy đang mở app
builder.Services.AddSignalR();
builder.Services.AddSingleton<NhaXe.Modules.ThongBao.Application.IDayThoiGianThuc, NhaXe.Api.Common.DaySignalR>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Issuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        o.Events = new JwtBearerEvents
        {
            // 1 tài khoản chỉ đăng nhập 1 máy: token của máy cũ mang phiên cũ thì bị từ chối.
            // SignalR qua WebSocket không gửi được header: token đi theo query ?access_token=... (chỉ nhận cho /hub/)
            OnMessageReceived = ctx =>
            {
                var t = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(t) && ctx.HttpContext.Request.Path.StartsWithSegments("/hub")) ctx.Token = t;
                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var tk = ctx.HttpContext.RequestServices.GetRequiredService<ITaiKhoanModule>();
                var id = int.Parse(ctx.Principal!.FindFirst("sub")!.Value);
                var phien = ctx.Principal.FindFirst(JwtTokenService.ClaimPhien)?.Value ?? "";
                if (!await tk.PhienConHieuLucAsync(id, phien))
                    ctx.Fail("PHIEN_HET_HIEU_LUC");
            },
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                var het = ctx.AuthenticateFailure?.Message == "PHIEN_HET_HIEU_LUC";
                await ctx.Response.WriteAsJsonAsync(het
                    ? new { ma = "PHIEN_HET_HIEU_LUC", thongDiep = "Phiên đăng nhập đã hết hiệu lực. Hãy đăng nhập lại." }
                    : new { ma = "CHUA_DANG_NHAP", thongDiep = "Bạn cần đăng nhập." });
            }
        };
    });
builder.Services.AddAuthorization();

// Chống dò mật khẩu, lớp 2/3 (lớp 1: NGINX limit_req; lớp 3: khoá theo tài khoản trong TaiKhoan):
// mỗi IP tối đa N lần/phút vào /api/auth/*. Bộ đếm nằm trong RAM của từng instance (scale 2 thì mỗi instance đếm riêng),
// nên không dùng thay được lớp 3.
var soLanAuthMoiPhut = cfg.GetValue("ChongDoMatKhau:SoLanMoiPhutMoiIp", 30);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (ctx, ct) => new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(
        new { ma = "QUA_NHIEU_YEU_CAU", thongDiep = "Bạn thao tác quá nhanh. Hãy thử lại sau 1 phút." }, ct));
    o.AddPolicy("auth", http => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "khong-ro",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        { PermitLimit = soLanAuthMoiPhut, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
// Sau NGINX, RemoteIpAddress là IP của NGINX: đọc IP thật từ X-Forwarded-For, nhưng CHỈ tin header khi nó đến từ
// mạng nội bộ Docker (người ngoài tự gửi X-Forwarded-For giả thì bị bỏ qua).
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var mang in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" })
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(mang));
});
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new NhaXe.Api.Common.GioDiaPhuongJson()))
    .ConfigureApiBehaviorOptions(o =>
    o.InvalidModelStateResponseFactory = _ => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
        new { ma = "DU_LIEU_KHONG_HOP_LE", thongDiep = "Thiếu trường bắt buộc hoặc dữ liệu không đúng định dạng." }));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    // Nút "Authorize" trên Swagger: dán token lấy từ /api/auth/dang-nhap để gọi các API cần đăng nhập
    // .NET 10 + Swashbuckle 10 dùng Microsoft.OpenApi 2.x: model nằm ở namespace Microsoft.OpenApi, tham chiếu bằng OpenApiSecuritySchemeReference
    var scheme = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Dán token (không cần chữ Bearer)",
    };
    o.AddSecurityDefinition("Bearer", scheme);
    // Hai module có DTO trùng tên (CongRaVao.YeuCauDto, NhomGiaDinh.YeuCauDto): đặt tên schema kèm module để Swagger không lỗi
    o.CustomSchemaIds(t => (t.FullName ?? t.Name).Replace("NhaXe.Modules.", "").Replace(".Application.", ".").Replace("+", "."));
    // Nhóm API theo module ([Tags] trên controller), sắp theo số thứ tự module trong file kiến trúc
    o.OrderActionsBy(a => $"{a.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>().FirstOrDefault()?.Tags.FirstOrDefault()}_{a.RelativePath}");
    o.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

var app = builder.Build();
app.UseForwardedHeaders(); // đứng đầu: mọi middleware sau thấy IP thật của người dùng

// Gắn tên instance vào header để thấy NGINX chia tải giữa các container API (Buổi 7, 8).
var instance = Environment.MachineName;
app.Use(async (ctx, next) => { ctx.Response.Headers["X-Api-Instance"] = instance; await next(); });

app.UseMiddleware<LoiMiddleware>();
if (app.Environment.IsDevelopment() || cfg["Swagger"] == "true")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NhaXe.Api.Common.ThongBaoHub>("/hub/thong-bao");
app.MapGet("/api/health", () => Results.Ok(new { trangThai = "OK", instance })).WithTags("0. Hệ thống");

app.Run();
