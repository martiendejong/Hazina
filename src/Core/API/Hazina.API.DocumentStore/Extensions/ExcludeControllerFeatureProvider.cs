using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hazina.API.DocumentStore.Extensions;

/// <summary>
/// Feature provider that removes a specific controller type from discovery
/// </summary>
public class ExcludeControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly Type _controllerType;

    public ExcludeControllerFeatureProvider(Type controllerType)
    {
        _controllerType = controllerType;
    }

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        feature.Controllers.Remove(
            feature.Controllers.FirstOrDefault(c => c.AsType() == _controllerType));
    }
}
