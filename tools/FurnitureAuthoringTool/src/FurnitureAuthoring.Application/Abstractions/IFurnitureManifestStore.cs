using System.Threading;
using System.Threading.Tasks;
using FurnitureAuthoring.Contracts.Models;

namespace FurnitureAuthoring.Application.Abstractions;

public interface IFurnitureManifestStore
{
    Task<FurnitureManifestDto> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(string path, FurnitureManifestDto manifest, CancellationToken cancellationToken = default);
}
