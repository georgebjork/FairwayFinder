using System.Text.Json.Serialization;

namespace FairwayFinder.Features.Data.GolfCourseApi;

public class GolfCourseApiCoursesResponse
{
    [JsonPropertyName("courses")]
    public List<GolfCourseApiCourse> Courses { get; set; } = [];

    [JsonPropertyName("metadata")]
    public GolfCourseApiMetadata Metadata { get; set; } = new();
}

public class GolfCourseApiMetadata
{
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("first_page")]
    public int FirstPage { get; set; }

    [JsonPropertyName("last_page")]
    public int LastPage { get; set; }

    [JsonPropertyName("total_records")]
    public int TotalRecords { get; set; }
}

public class GolfCourseApiCourse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("club_name")]
    public string ClubName { get; set; } = string.Empty;

    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public GolfCourseApiLocation? Location { get; set; }

    [JsonPropertyName("tees")]
    public GolfCourseApiTees? Tees { get; set; }
}

public class GolfCourseApiLocation
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
}

public class GolfCourseApiTees
{
    [JsonPropertyName("female")]
    public List<GolfCourseApiTeeBox> Female { get; set; } = [];

    [JsonPropertyName("male")]
    public List<GolfCourseApiTeeBox> Male { get; set; } = [];
}

public class GolfCourseApiTeeBox
{
    [JsonPropertyName("tee_name")]
    public string TeeName { get; set; } = string.Empty;

    [JsonPropertyName("course_rating")]
    public decimal CourseRating { get; set; }

    [JsonPropertyName("slope_rating")]
    public int SlopeRating { get; set; }

    [JsonPropertyName("bogey_rating")]
    public decimal BogeyRating { get; set; }

    [JsonPropertyName("total_yards")]
    public int TotalYards { get; set; }

    [JsonPropertyName("total_meters")]
    public int TotalMeters { get; set; }

    [JsonPropertyName("number_of_holes")]
    public int NumberOfHoles { get; set; }

    [JsonPropertyName("par_total")]
    public int ParTotal { get; set; }

    [JsonPropertyName("front_course_rating")]
    public decimal? FrontCourseRating { get; set; }

    [JsonPropertyName("front_slope_rating")]
    public int? FrontSlopeRating { get; set; }

    [JsonPropertyName("front_bogey_rating")]
    public decimal? FrontBogeyRating { get; set; }

    [JsonPropertyName("back_course_rating")]
    public decimal? BackCourseRating { get; set; }

    [JsonPropertyName("back_slope_rating")]
    public int? BackSlopeRating { get; set; }

    [JsonPropertyName("back_bogey_rating")]
    public decimal? BackBogeyRating { get; set; }

    [JsonPropertyName("holes")]
    public List<GolfCourseApiHole> Holes { get; set; } = [];
}

public class GolfCourseApiHole
{
    [JsonPropertyName("par")]
    public int Par { get; set; }

    [JsonPropertyName("yardage")]
    public int Yardage { get; set; }

    [JsonPropertyName("handicap")]
    public int Handicap { get; set; }
}
