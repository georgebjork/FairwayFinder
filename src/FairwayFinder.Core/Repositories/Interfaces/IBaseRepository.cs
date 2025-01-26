namespace FairwayFinder.Core.Repositories.Interfaces;

public interface IBaseRepository {
    Task<int> Insert<T>(T? data) where T: class;
    Task<bool> Update<T>(T? data) where T: class;
}