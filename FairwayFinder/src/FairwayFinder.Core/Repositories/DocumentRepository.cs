using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FairwayFinder.Core.Repositories;

public class DocumentRepository(IConfiguration configuration) : BasePgRepository(configuration), IDocumentRepository { }