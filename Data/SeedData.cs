using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeProject.Models;

namespace WeProject.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var context = new AppDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<AppDbContext>>());

            // Prüfen, ob die Datenbank bereits befüllt ist
            if (context.Courses.Any())
            {
                return; 
            }

            // ==========================================
            // 1. LEHRVERANSTALTUNGEN (COURSES) ANLEGEN
            // ==========================================
            var course1 = new Course { Title = "Modellbasierte Systementwicklung", LecturerName = "Prof. Dr. Schmidt", IsMasterCourse = true };
            var course2 = new Course { Title = "Grundlagen der Programmierung", LecturerName = "Dr. Müller", IsMasterCourse = false };
            var course3 = new Course { Title = "Algorithmen und Datenstrukturen", LecturerName = "Prof. Dr. Weber", IsMasterCourse = false };
            var course4 = new Course { Title = "Künstliche Intelligenz", LecturerName = "Prof. Dr. Turing", IsMasterCourse = true };
            var course5 = new Course { Title = "Datenbanken", LecturerName = "Dr. Codd", IsMasterCourse = false };
            var course6 = new Course { Title = "Webentwicklung mit ASP.NET Core", LecturerName = "Prof. Dr. Berners-Lee", IsMasterCourse = false };

            context.Courses.AddRange(course1, course2, course3, course4, course5, course6);
            context.SaveChanges(); 

            // ==========================================
            // 2. KAPITEL (CHAPTERS) ZUWEISEN
            // ==========================================
            var chapters = new[]
            {
                // Kapitel für Kurs 1 (Modellbasierte Systementwicklung)
                new Chapter { Title = "Einführung in UML", ChapterNumber = 1, CourseId = course1.Id },
                new Chapter { Title = "Sequenzdiagramme", ChapterNumber = 2, CourseId = course1.Id },
                new Chapter { Title = "Zustandsautomaten", ChapterNumber = 3, CourseId = course1.Id },

                // Kapitel für Kurs 2 (Grundlagen der Programmierung)
                new Chapter { Title = "Variablen und Datentypen", ChapterNumber = 1, CourseId = course2.Id },
                new Chapter { Title = "Schleifen und Verzweigungen", ChapterNumber = 2, CourseId = course2.Id },
                new Chapter { Title = "Objektorientierung", ChapterNumber = 3, CourseId = course2.Id },

                // Kapitel für Kurs 3 (Algorithmen)
                new Chapter { Title = "O-Notation und Komplexität", ChapterNumber = 1, CourseId = course3.Id },
                new Chapter { Title = "Sortierverfahren", ChapterNumber = 2, CourseId = course3.Id },
                new Chapter { Title = "Graphen und Bäume", ChapterNumber = 3, CourseId = course3.Id },

                // Kapitel für Kurs 4 (KI)
                new Chapter { Title = "Machine Learning Grundlagen", ChapterNumber = 1, CourseId = course4.Id },
                new Chapter { Title = "Neuronale Netze", ChapterNumber = 2, CourseId = course4.Id },

                // Kapitel für Kurs 5 (Datenbanken)
                new Chapter { Title = "Relationales Datenmodell", ChapterNumber = 1, CourseId = course5.Id },
                new Chapter { Title = "SQL-Abfragen", ChapterNumber = 2, CourseId = course5.Id },
                new Chapter { Title = "Normalisierung", ChapterNumber = 3, CourseId = course5.Id },

                // Kapitel für Kurs 6 (Webentwicklung)
                new Chapter { Title = "MVC-Architektur", ChapterNumber = 1, CourseId = course6.Id },
                new Chapter { Title = "Entity Framework Core", ChapterNumber = 2, CourseId = course6.Id }
            };

            context.Chapters.AddRange(chapters);
            context.SaveChanges();
        }
    }
}