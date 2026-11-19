using Microsoft.EntityFrameworkCore;

namespace NhaXe.Modules.ThongBao.Infrastructure;

/// <summary>DbContext riêng của module ThongBao, chỉ đụng bảng thong_bao.</summary>
public class ThongBaoDbContext : DbContext
{
    public ThongBaoDbContext(DbContextOptions<ThongBaoDbContext> o) : base(o) { }
}
