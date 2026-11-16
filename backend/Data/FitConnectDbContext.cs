using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Models;

namespace FitConnectAPI.Data;

public class FitConnectDbContext : DbContext
{
    public FitConnectDbContext(DbContextOptions<FitConnectDbContext> options) : base(options) { }

    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Membresia> Membresias => Set<Membresia>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<CorteCaja> CortesCaja => Set<CorteCaja>();
    public DbSet<NfcLectura> NfcLecturas => Set<NfcLectura>();
    public DbSet<LecturaSensor> LecturasSensor => Set<LecturaSensor>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<StockSucursal> StockSucursal => Set<StockSucursal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sucursal>().ToTable("Sucursales");
        modelBuilder.Entity<Usuario>().ToTable("Usuarios");
        modelBuilder.Entity<Cliente>().ToTable("Clientes");
        modelBuilder.Entity<Membresia>().ToTable("Membresias");
        modelBuilder.Entity<Pago>().ToTable("Pagos");
        modelBuilder.Entity<Asistencia>().ToTable("Asistencias");
        modelBuilder.Entity<Producto>().ToTable("Productos");
        modelBuilder.Entity<Venta>().ToTable("Ventas");
        modelBuilder.Entity<CorteCaja>().ToTable("CortesCaja");
        modelBuilder.Entity<NfcLectura>().ToTable("NfcLecturas");
        modelBuilder.Entity<LecturaSensor>().ToTable("LecturasSensor");
        modelBuilder.Entity<MovimientoInventario>().ToTable("MovimientosInventario");
        modelBuilder.Entity<StockSucursal>().ToTable("StockSucursal");

        modelBuilder.Entity<Pago>()
            .HasOne(p => p.Recepcionista)
            .WithMany()
            .HasForeignKey(p => p.IdRecepcionista)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Pago>()
            .HasOne(p => p.Sucursal)
            .WithMany()
            .HasForeignKey(p => p.IdSucursal)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Asistencia>()
            .HasOne(a => a.Sucursal)
            .WithMany()
            .HasForeignKey(a => a.IdSucursal)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Venta>()
            .HasOne(v => v.Recepcionista)
            .WithMany()
            .HasForeignKey(v => v.IdRecepcionista)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Venta>()
            .HasOne(v => v.Sucursal)
            .WithMany()
            .HasForeignKey(v => v.IdSucursal)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<CorteCaja>()
            .HasOne(c => c.Sucursal)
            .WithMany()
            .HasForeignKey(c => c.IdSucursal)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<CorteCaja>()
            .HasOne(c => c.Recepcionista)
            .WithMany()
            .HasForeignKey(c => c.IdRecepcionista)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<StockSucursal>()
            .HasOne(s => s.Producto)
            .WithMany()
            .HasForeignKey(s => s.IdProducto)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<StockSucursal>()
            .HasOne(s => s.Sucursal)
            .WithMany()
            .HasForeignKey(s => s.IdSucursal)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<StockSucursal>()
            .HasIndex(s => new { s.IdProducto, s.IdSucursal }).IsUnique();

        modelBuilder.Entity<MovimientoInventario>()
            .HasOne(m => m.Producto)
            .WithMany()
            .HasForeignKey(m => m.IdProducto)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<MovimientoInventario>()
            .HasOne(m => m.Sucursal)
            .WithMany()
            .HasForeignKey(m => m.IdSucursal)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
