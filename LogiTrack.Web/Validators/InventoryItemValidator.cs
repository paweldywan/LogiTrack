using FluentValidation;

using LogiTrack.Domain.Models;

namespace LogiTrack.Web.Validators;

public class InventoryItemValidator : AbstractValidator<InventoryItem>
{
    public InventoryItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity must be non-negative");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required")
            .MaximumLength(100).WithMessage("Location must not exceed 100 characters");
    }
}
