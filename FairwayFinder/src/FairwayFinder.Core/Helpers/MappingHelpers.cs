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
    
    public static Course ToModel(this CourseFormModel form)
    {
        return new Course
        {
            course_id = form.course_id ?? 0,
            course_name = form.name,
            address = form.address,
            phone_number = form.phone_number
        };
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
    
    public static Teebox ToModel(this TeeboxFormModel form)
    {
        return new Teebox
        {
            teebox_id = form.TeeboxId ?? 0,
            teebox_name = form.Name,
            course_id = form.CourseId,
            par = form.Par,
            slope = form.Slope,
            rating = form.Rating,
            yardage_out = form.YardageOut,
            yardage_in = form.YardageIn,
            yardage_total = form.Yardage,
            is_nine_hole = form.IsNineHoles,
            is_womens = false
        };
    }
}