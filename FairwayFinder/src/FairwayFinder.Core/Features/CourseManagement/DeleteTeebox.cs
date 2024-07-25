using FairwayFinder.Core.Features.CourseManagement.Services;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record DeleteTeeboxRequest : IRequest<Result<bool>> 
{
    public int TeeboxId { get; set; }
}

public class DeleteTeeboxHandler : IRequestHandler<DeleteTeeboxRequest, Result<bool>> 
{
    private readonly ITeeboxManagementService _teeboxManagementService;
    private readonly ILogger<DeleteCourseHandler> _logger;
    
    public DeleteTeeboxHandler(ITeeboxManagementService teeboxManagementService, ILogger<DeleteCourseHandler> logger)
    {
        _teeboxManagementService = teeboxManagementService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteTeeboxRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.TeeboxId <= 0) throw new ArgumentOutOfRangeException(nameof(request.TeeboxId), "A non-valid id was passed.");
            
            // Delete teebox.
            var result = await _teeboxManagementService.DeleteTeebox(request.TeeboxId);
            return result;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Invalid TeeboxId in DeleteTeeboxHandler.");
            return new Result<bool>(ex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An unexpected error occurred in DeleteTeeboxHandler.");
            return new Result<bool>(new Exception("An error occurred, unable to delete teebox", ex));
        }
    }
}
