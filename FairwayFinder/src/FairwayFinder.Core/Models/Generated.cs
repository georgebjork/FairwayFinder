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
public class Score : IEntityMetadata
{
    [Key]
    public long score_id { get; set; }
    public long round_id { get; set; }
    public long hole_id { get; set; }
    public short hole_score { get; set; }
    public string user_id { get; set; } = "";
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
    
    [Computed]
    public long hole_number { get; set; }
}

[Table("hole")]
public class Hole : IEntityMetadata
{
    [Key]
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
public class Round : IEntityMetadata
{
    [Key]
    public long round_id { get; set; }
    public long course_id { get; set; }
    public long teebox_id { get; set; }
    public DateTime date_played { get; set; }
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
    public int score { get; set; }
    public int score_out { get; set; }
    public int score_in { get; set; }
    public string user_id { get; set; } = "";
}

[Table("hole_stats")]
public class HoleStats : IEntityMetadata
{
    [Key]
    public long hole_stats_id { get; set; }
    public long round_id { get; set; }
    public long score_id { get; set; }
    public bool? hit_fairway { get; set; }
    public int miss_fairway_type { get; set; }
    public bool? hit_green { get; set; }
    public int miss_green_type { get; set; }
    public short? number_of_putts { get; set; }
    public int approach_yardage { get; set; }
    public string created_by { get; set; } = "";
    public DateTime created_on { get; set; }
    public string updated_by { get; set; } = "";
    public DateTime updated_on { get; set; }
    public bool is_deleted { get; set; }
}

[Table("round_stats")]
public class RoundStats : IEntityMetadata
{
    [Key]
    public long round_stats_id { get; set; }
    public long round_id { get; set; }
    public int hole_in_one { get; set; }
    public int double_eagles { get; set; }
    public int eagles { get; set; }
    public int birdies { get; set; }
    public int pars { get; set; }
    public int bogies { get; set; }
    public int double_bogies { get; set; }
    public int triple_or_worse { get; set; }
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