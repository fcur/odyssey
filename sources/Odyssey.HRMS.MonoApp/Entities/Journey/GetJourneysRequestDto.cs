using System.ComponentModel.DataAnnotations;

namespace Odyssey.HRMS.MonoApp.Entities.Journey;

public sealed class GetJourneysRequestDto: IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return [];
    }
}