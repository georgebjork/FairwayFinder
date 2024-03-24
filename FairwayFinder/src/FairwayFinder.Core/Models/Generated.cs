using System.ComponentModel.DataAnnotations.Schema;

namespace FairwayFinder.Core.Models;

public class Teebox
{
    public long TeeboxId { get; set; }
    public long CourseId { get; set; }
    public string TeeboxName { get; set; } = "";
    public long Par { get; set; }
    public decimal Rating { get; set; }
    public long Slope { get; set; }
    public long YardageOut { get; set; }
    public long YardageIn { get; set; }
    public long YardageTotal { get; set; }
    public bool IsNineHole { get; set; }
    public bool IsWomens { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedOn { get; set; }
}

public class Score
{
    public long ScoreId { get; set; }
    public long RoundId { get; set; }
    public long HoleId { get; set; }
    public short HoleScore { get; set; }
    public string ScoreType { get; set; } = "";
    public string UserId { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedOn { get; set; }
}

public class Hole
{
    public long HoleId { get; set; }
    public long TeeboxId { get; set; }
    public long CourseId { get; set; }
    public long HoleNumber { get; set; }
    public long Yardage { get; set; }
    public long Handicap { get; set; }
    public long Par { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedBy { get; set; }
    public string UpdatedOn { get; set; } = "";
}

public class Round
{
    public long RoundId { get; set; }
    public long CourseId { get; set; }
    public long TeeboxId { get; set; }
    public DateTime DatePlayed { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedOn { get; set; }
}

public class Stats
{
    public long StatId { get; set; }
    public long ScoreId { get; set; }
    public bool? HitFairway { get; set; }
    public string MissFairwayType { get; set; } = "";
    public bool? HitGreen { get; set; }
    public string MissGreenType { get; set; } = "";
    public short? NumberOfPutts { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedOn { get; set; }
}

public class Course
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public string Address { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedOn { get; set; }
}