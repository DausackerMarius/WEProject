using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WeProject.Models // WICHTIG: Namespace an deinen Projektnamen angepasst
{
    public class Course
    {
        public int Id { get; init; }

        [MaxLength(100)]
        public required string Title { get; set; }

        [MaxLength(50)]
        public required string LecturerName { get; set; }

        public required bool IsMasterCourse { get; set; }

        public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
        public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }

    public class Chapter
    {
        public int Id { get; init; }

        [MaxLength(100)]
        public required string Title { get; set; }

        public required int ChapterNumber { get; set; }

        public string? PdfFilePath { get; set; }

        public required int CourseId { get; set; }
        public Course? Course { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }

    public class Question
    {
        public int Id { get; init; }

        public required string Text { get; set; }

        public required int ChapterId { get; set; }
        public Chapter? Chapter { get; set; }

        public ICollection<AnswerOption> AnswerOptions { get; set; } = new List<AnswerOption>();
        public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }

    public class AnswerOption
    {
        public int Id { get; init; }

        public required string Text { get; set; }

        public required bool IsCorrect { get; set; }

        public required int QuestionId { get; set; }
        public Question? Question { get; set; }
    }

    public class Exam
    {
        public int Id { get; init; }

        public required DateTime ExamDate { get; set; }

        public required int CourseId { get; set; }
        public Course? Course { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}