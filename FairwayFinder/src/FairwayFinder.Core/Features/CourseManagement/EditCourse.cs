using FairwayFinder.Core.Exceptions;
using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Repositories;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record EditCourseRequest : IRequest<Result<CourseFormModel>>
{
    public int CourseId { get; set; }
}

public class EditCourseHandler : IRequestHandler<EditCourseRequest, Result<CourseFormModel>>
{
    private readonly ILogger<EditCourseHandler> _logger;
    private readonly ICourseManagementService _courseManagementService;
    private readonly ICourseRepository _courseRepository;

    public EditCourseHandler(ICourseManagementService courseManagementService, ILogger<EditCourseHandler> logger, ICourseRepository courseRepository)
    {
        _courseManagementService = courseManagementService;
        _logger = logger;
        _courseRepository = courseRepository;
    }

    public async Task<Result<CourseFormModel>> Handle(EditCourseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var course = await _courseRepository.GetCourseById(request.CourseId);
            if (course is null) throw new NullCourseException();

            var form = new CourseFormModel
            {
                CourseId = course.course_id,
                Name = course.course_name,
                Address = course.address,
                PhoneNumber = course.phone_number
            };

            return form;
        }
        catch (NullCourseException ex)
        {
            _logger.LogWarning(ex, "Golf course with id {0} came back null in EditCourseHandler", request.CourseId);
            return new Result<CourseFormModel>(new Exception("Golf course does not exist", ex)); 
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred in the EditCourseHandler");
            return new Result<CourseFormModel>(new Exception("An error occured, unable to edit golf course", ex)); 
        }
    }
}