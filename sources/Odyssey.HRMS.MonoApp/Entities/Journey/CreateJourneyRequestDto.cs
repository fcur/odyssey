using System.ComponentModel.DataAnnotations;

namespace Odyssey.HRMS.MonoApp.Entities.Journey;

public sealed class CreateJourneyRequestDto: IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return [];
    }
}