using monthly_claiming_system.Models;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets for your tables
   
    

    public DbSet<User> Users { get; set; }
    public DbSet<Lecturer> Lecturers { get; set; }
    public DbSet<Claim> Claims { get; set; }
    public DbSet<SupportingDocuments> SupportingDocuments { get; set; }
}

