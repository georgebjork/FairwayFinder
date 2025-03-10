using FairwayFinder.Core.Models.Interfaces;
using FairwayFinder.Core.Services;

namespace FairwayFinder.Core.Helpers;

public static class EntityMetadataHelper
{
    public static T NewRecord<T>(T obj, string userId) where T : IEntityMetadata
    {
        var currentTime = DateTime.UtcNow;
        
        obj.created_on = currentTime;
        obj.updated_on = currentTime;
        obj.created_by = userId;
        obj.updated_by = userId;

        return obj;
    }
    
    public static T UpdateRecord<T>(T obj, string userId) where T : IEntityMetadata
    {
        var currentTime = DateTime.UtcNow;
        
        obj.updated_on = currentTime;
        obj.updated_by = userId;

        return obj;
    }
}