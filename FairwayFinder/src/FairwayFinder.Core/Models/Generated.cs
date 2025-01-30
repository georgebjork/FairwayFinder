using Dapper.Contrib.Extensions;
using FairwayFinder.Core.Models.Interfaces;

namespace FairwayFinder.Core.Models;

[Table("teebox")]
public class Teebox : IEntityMetadata
{
    [Key]
    public long teebox_id { get; set; }
    public long course_id { get; set; }
    public string teebox_name { get; set; } = "";
    public long par { get; set; }
    public decimal rating { get; set; }
    public long slope { get; set; }
    public long yardage_out { get; set; }
    public long yardage_in { get; set; }
    public long yardage_total { get; set; }
    public bool is_nine_hole { get; set; }
    public bool is_womens { get; set; }
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
}

[Table("score")]
public class Score
{
    public long score_id { get; set; }
    public long round_id { get; set; }
    public long hole_id { get; set; }
    public short hole_score { get; set; }
    public string score_type { get; set; } = "";
    public string user_id { get; set; } = "";
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
}

[Table("hole")]
public class Hole
{
    public long hole_id { get; set; }
    public long teebox_id { get; set; }
    public long course_id { get; set; }
    public long hole_number { get; set; }
    public long yardage { get; set; }
    public long handicap { get; set; }
    public long par { get; set; }
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
}

[Table("round")]
public class Round
{
    public long round_id { get; set; }
    public long course_id { get; set; }
    public long teebox_id { get; set; }
    public DateTime date_played { get; set; }
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
}

[Table("stats")]
public class Stats
{
    public long stat_id { get; set; }
    public long score_id { get; set; }
    public bool? hit_fairway { get; set; }
    public string miss_fairway_type { get; set; } = "";
    public bool? hit_green { get; set; }
    public string miss_green_type { get; set; } = "";
    public short? number_of_putts { get; set; }
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
}


[Table("course")]
public class Course : IEntityMetadata
{
    [Key]
    public long course_id { get; set; }
    public string course_name { get; set; } = "";
    public string address { get; set; } = "";
    public string phone_number { get; set; } = "";
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
}