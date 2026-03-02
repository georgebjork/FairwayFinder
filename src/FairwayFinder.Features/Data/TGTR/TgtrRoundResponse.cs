using System.Text.Json.Serialization;

namespace FairwayFinder.Features.Data.TGTR;

public class TgtrRoundResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = string.Empty;

    [JsonPropertyName("courseId")]
    public int CourseId { get; set; }

    [JsonPropertyName("courseName")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("teeBoxId")]
    public int TeeBoxId { get; set; }

    [JsonPropertyName("teeBoxName")]
    public string TeeBoxName { get; set; } = string.Empty;

    [JsonPropertyName("yardage")]
    public int Yardage { get; set; }

    [JsonPropertyName("yardageOut")]
    public int YardageOut { get; set; }

    [JsonPropertyName("yardageIn")]
    public int YardageIn { get; set; }

    [JsonPropertyName("slope")]
    public int Slope { get; set; }

    [JsonPropertyName("rating")]
    public decimal Rating { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("scoreOut")]
    public int ScoreOut { get; set; }

    [JsonPropertyName("scoreIn")]
    public int ScoreIn { get; set; }

    [JsonPropertyName("par")]
    public int Par { get; set; }

    [JsonPropertyName("finishedFront")]
    public bool FinishedFront { get; set; }

    [JsonPropertyName("finishedBack")]
    public bool FinishedBack { get; set; }

    [JsonPropertyName("finishedRound")]
    public bool FinishedRound { get; set; }

    [JsonPropertyName("holesPlayed")]
    public int HolesPlayed { get; set; }

    [JsonPropertyName("holeScores")]
    public List<TgtrHoleScore> HoleScores { get; set; } = new();

    [JsonPropertyName("holes")]
    public List<TgtrHole> Holes { get; set; } = new();

    [JsonPropertyName("stats")]
    public TgtrRoundStats? Stats { get; set; }
}

public class TgtrHoleScore
{
    [JsonPropertyName("playerId")]
    public int PlayerId { get; set; }

    [JsonPropertyName("roundId")]
    public int RoundId { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("scoreToPar")]
    public int ScoreToPar { get; set; }

    [JsonPropertyName("fairway")]
    public bool? Fairway { get; set; }

    [JsonPropertyName("gir")]
    public bool? Gir { get; set; }

    [JsonPropertyName("putts")]
    public int? Putts { get; set; }

    [JsonPropertyName("penalties")]
    public int? Penalties { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("handicap")]
    public int Handicap { get; set; }

    [JsonPropertyName("par")]
    public int Par { get; set; }

    [JsonPropertyName("yardage")]
    public int Yardage { get; set; }
}

public class TgtrHole
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("handicap")]
    public int Handicap { get; set; }

    [JsonPropertyName("par")]
    public int Par { get; set; }

    [JsonPropertyName("yardage")]
    public int Yardage { get; set; }
}

public class TgtrRoundStats
{
    [JsonPropertyName("roundId")]
    public int RoundId { get; set; }

    [JsonPropertyName("eagles")]
    public int Eagles { get; set; }

    [JsonPropertyName("birdies")]
    public int Birdies { get; set; }

    [JsonPropertyName("pars")]
    public int Pars { get; set; }

    [JsonPropertyName("bogeys")]
    public int Bogeys { get; set; }

    [JsonPropertyName("doubles")]
    public int Doubles { get; set; }

    [JsonPropertyName("triples")]
    public int Triples { get; set; }

    [JsonPropertyName("worse")]
    public int Worse { get; set; }
}
