using System.Threading;
using System.Threading.Tasks;
using FurnitureAuthoring.Application.Models;

namespace FurnitureAuthoring.Application.Abstractions;

public interface IFurniturePatchBuildWorker
{
    Task<PatchBuildResult> BuildAsync(PatchBuildRequest request, CancellationToken cancellationToken = default);
}
