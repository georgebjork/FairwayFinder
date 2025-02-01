using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Helpers;

public static class MappingHelpers
{
    public static CourseFormModel ToFormModel(this Course course)
    {
        return new CourseFormModel
        {
            course_id = course.course_id,
            name = course.course_name,
            address = course.address,
            phone_number = course.phone_number
        };
    }
    
    public static Course ToModel(this CourseFormModel form, Course course)
    {
        course.course_id = form.course_id ?? 0;
        course.course_name = form.name;
        course.address = form.address;
        course.phone_number = form.phone_number;

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
}