using Microsoft.EntityFrameworkCore;
using WeProject.Models; 

namespace WeProject.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Course> Courses { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<Exam> Exams { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfiguriert die Many-to-Many-Beziehung zwischen Exam und Question
            modelBuilder.Entity<Exam>()
                .HasMany(e => e.Questions)
                .WithMany(q => q.Exams)
                .UsingEntity<Dictionary<string, object>>(
                    "ExamQuestion",
                    j => j
                        .HasOne<Question>()
                        .WithMany()
                        .HasForeignKey("QuestionsId")
                        .OnDelete(DeleteBehavior.NoAction), // Verhindert die zweite Kaskade beim SQL Server
                    j => j
                        .HasOne<Exam>()
                        .WithMany()
                        .HasForeignKey("ExamsId")
                        .OnDelete(DeleteBehavior.Cascade)); // Behält die primäre Kaskade bei
        }
    }
}