using Microsoft.EntityFrameworkCore;
using WeProject.Models;

namespace WeProject.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new AppDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<AppDbContext>>()))
            {
                // Prüfen, ob schon Kurse existieren. Wenn ja, brich ab (Datenbank wurde schon befüllt)
                if (context.Courses.Any())
                {
                    return;
                }

                // 1. Einen Demo-Kurs anlegen
                var course = new Course
                {
                    Title = "Web Engineering",
                    LecturerName = "Prof. Dr. Code",
                    IsMasterCourse = false
                };
                context.Courses.Add(course);
                context.SaveChanges(); // Speichern, damit der Kurs eine ID bekommt

                // 2. Ein Demo-Kapitel anlegen
                var chapter = new Chapter
                {
                    Title = "Einführung in ASP.NET Core",
                    ChapterNumber = 1,
                    CourseId = course.Id
                };
                context.Chapters.Add(chapter);
                context.SaveChanges();

                // 3. Eine Demo-Frage anlegen
                var question = new Question
                {
                    Text = "Welches Architekturmuster nutzt ASP.NET Core typischerweise für Web-Apps?",
                    ChapterId = chapter.Id
                };
                context.Questions.Add(question);
                context.SaveChanges();

                // 4. Vier Antwortoptionen anlegen (genau eine ist korrekt)
                context.AnswerOptions.AddRange(
                    new AnswerOption { Text = "MVC (Model-View-Controller)", IsCorrect = true, QuestionId = question.Id },
                    new AnswerOption { Text = "MVP (Model-View-Presenter)", IsCorrect = false, QuestionId = question.Id },
                    new AnswerOption { Text = "MVVM (Model-View-ViewModel)", IsCorrect = false, QuestionId = question.Id },
                    new AnswerOption { Text = "Singleton", IsCorrect = false, QuestionId = question.Id }
                );
                context.SaveChanges();
            }
        }
    }
}