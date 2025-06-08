using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Helpers;

public static class MappingHelpers
{
    public static CourseFormModel ToFormModel(this Course course)
    {
        return new CourseFormModel
        {
            CourseId = course.course_id,
            Name = course.course_name,
            Address = course.address,
            PhoneNumber = course.phone_number
        };
    }
    
    public static Course ToModel(this CourseFormModel form, Course course)
    {
        course.course_id = form.CourseId ?? 0;
        course.course_name = form.Name;
        course.address = form.Address;
        course.phone_number = form.PhoneNumber;

        return course;
    }
    
    
    public static TeeboxFormModel ToFormModel(this Teebox tee)
    {
        return new TeeboxFormModel
        {
            TeeboxId = tee.teebox_id,
            CourseId = tee.course_id,
            Name = tee.teebox_name,
            Par = tee.par,
            Slope = tee.slope,
            Rating = tee.rating,
            YardageOut = tee.yardage_out,
            YardageIn = tee.yardage_in,
            Yardage = tee.yardage_total,
            IsNineHoles = tee.is_nine_hole,
            IsWomenTees = tee.is_womens
        };
    }
    
    public static Teebox ToModel(this TeeboxFormModel form, Teebox tee)
    {
        tee.teebox_id = form.TeeboxId ?? 0;
        tee.teebox_name = form.Name;
        tee.course_id = form.CourseId;
        tee.par = form.Par;
        tee.slope = form.Slope;
        tee.rating = form.Rating;
        tee.yardage_out = form.YardageOut;
        tee.yardage_in = form.YardageIn;
        tee.yardage_total = form.Yardage;
        tee.is_nine_hole = form.IsNineHoles;
        tee.is_womens = false;

        return tee;
    }
    
    public static HoleFormModel ToFormModel(this Hole hole)
    {
        return new HoleFormModel
        {
            TeeboxId = hole.teebox_id,
            CourseId = hole.course_id,
            HoleId = hole.hole_id,
            Par = hole.par,
            Yardage = hole.yardage,
            HoleNumber = hole.hole_number,
            Handicap = hole.handicap,
        };
    }
    
    public static Hole ToModel(this HoleFormModel form, Hole hole)
    {
        hole.teebox_id = form.TeeboxId ?? 0;
        hole.course_id = form.CourseId ?? 0;
        hole.hole_id = form.HoleId ?? 0;
        hole.par = form.Par;
        hole.yardage = form.Yardage;
        hole.hole_number = form.HoleNumber;
        hole.handicap = form.Handicap;

        return hole;
    }

    public static HoleStats ToModel(this HoleStatsFormModel form)
    {
        return new HoleStats
        {
            hole_stats_id = form.HoleStatsId ?? 0,
            round_id = form.RoundId,
            score_id = form.ScoreId,
            hole_id = form.HoleId,
            hit_fairway = null,
            miss_fairway_type = form.MissFairwayType,
            hit_green = null,
            miss_green_type = form.MissGreenType,
            number_of_putts = form.NumberOfPutts,
            approach_yardage = form.YardageOut, 
        };
    }
    
    public static HoleStatsFormModel ToForm(this HoleStats hole)
    {
        return new HoleStatsFormModel
        {
            HoleStatsId = hole.hole_stats_id,
            RoundId = hole.round_id,
            ScoreId = hole.score_id,
            HoleId = hole.hole_id,
            MissedFairway = !hole.hit_fairway ?? false,
            HitFairway = hole.hit_fairway ?? false,
            MissFairwayType = hole.miss_fairway_type,
            MissedGreen = !hole.hit_green ?? false,
            HitGreen = hole.hit_green ?? false,
            MissGreenType = hole.miss_green_type,
            NumberOfPutts = hole.number_of_putts,
            YardageOut = hole.approach_yardage
        };
    }
    
    public static HoleStatsFormModel ToForm(this HoleStatsQueryModel hole)
    {
        return new HoleStatsFormModel
        {
            HoleStatsId = hole.hole_stats_id,
            RoundId = hole.round_id,
            ScoreId = hole.score_id,
            HoleId = hole.hole_id,
            MissedFairway = !hole.hit_fairway ?? false,
            HitFairway = hole.hit_fairway ?? false,
            MissFairwayType = hole.miss_fairway_type,
            MissedGreen = !hole.hit_green ?? false,
            HitGreen = hole.hit_green ?? false,
            MissGreenType = hole.miss_green_type,
            NumberOfPutts = hole.number_of_putts,
            YardageOut = hole.approach_yardage,
            TeeShotOb = hole.tee_shot_ob ?? false,
            ApproachShotOb = hole.approach_shot_ob ?? false
        };
    }

    public static HoleScoreFormModel ToForm(this Score holeScore)
    {
        return new HoleScoreFormModel
        {
            ScoreId = holeScore.score_id,
            HoleId = holeScore.hole_id,
            Score = holeScore.hole_score
        };
    }
    
    public static HoleScoreFormModel ToForm(this HoleScoreQueryModel holeScore)
    {
        return new HoleScoreFormModel
        {
            ScoreId = holeScore.score_id,
            HoleId = holeScore.hole_id,
            Score = holeScore.hole_score,
            Yardage = holeScore.yardage,
            Par = holeScore.par,
            HoleNumber = holeScore.hole_number
        };
    }

    public static Round ToModel(this RoundFormModel form)
    {
        return new Round
        {
            course_id = form.CourseId,
            teebox_id = form.TeeboxId,
            date_played = form.DatePlayed,
            score = 0,
            score_out = 0,
            score_in = 0,
            using_hole_stats = form.UsingHoleStats
        };
    }

    public static RoundFormModel ToForm(this Round round)
    {
        return new RoundFormModel
        {
            RoundId = round.round_id,
            DatePlayed = round.date_played,
            UsingHoleStats = round.using_hole_stats,
            CourseId = round.course_id,
            TeeboxId = round.teebox_id
        };
    }
}