using FairwayFinder.Core.Exceptions;
using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Repositories;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record EditTeeboxRequest : IRequest<Result<TeeboxFormModel>> {
    public int TeeboxId { get; set; }
}

public class EditTeeboxHandler : IRequestHandler<EditTeeboxRequest, Result<TeeboxFormModel>> {
    private readonly ILogger<EditCourseHandler> _logger;
    private readonly ITeeboxManagementService _teeboxManagementService;
    private readonly ITeeboxRepository _teeboxRepository;

    public EditTeeboxHandler(ILogger<EditCourseHandler> logger, ITeeboxManagementService teeboxManagementService, ITeeboxRepository teeboxRepository)
    {
        _logger = logger;
        _teeboxManagementService = teeboxManagementService;
        _teeboxRepository = teeboxRepository;
    }
    
    public async Task<Result<TeeboxFormModel>> Handle(EditTeeboxRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var teebox = await _teeboxRepository.GetTeeboxById(request.TeeboxId);
            if (teebox is null) throw new NullTeeboxException();

            var form = new TeeboxFormModel
            {
                CourseId = teebox.course_id,
                TeeboxId = teebox.teebox_id,
                Name = teebox.teebox_name,
                Par = teebox.par,
                Slope = teebox.slope,
                Rating = teebox.rating,
                YardageOut = teebox.yardage_out,
                YardageIn = teebox.yardage_in,
                IsNineHoles = teebox.is_nine_hole,
                IsWomenTees = teebox.is_womens
            };

            return form;
        }
        catch (NullTeeboxException ex)
        {
            _logger.LogWarning(ex, "Golf course with id {0} came back null in EditTeeboxHandler", request.TeeboxId);
            return new Result<TeeboxFormModel>(new Exception("Teebox does not exist", ex)); 
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred in the EditTeeboxHandler");
            return new Result<TeeboxFormModel>(new Exception("An error occured, unable to edit teebox", ex)); 
        }
    }
}
