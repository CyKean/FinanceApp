namespace FinanceApp.Application.Validators;

using FluentValidation;
using FinanceApp.Application.DTOs;
using FinanceApp.Domain.ValueObjects;

public class CreateBudgetDtoValidator : AbstractValidator<CreateBudgetDto>
{
    public CreateBudgetDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Budget name is required")
            .MaximumLength(100).WithMessage("Budget name cannot exceed 100 characters");

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required")
            .Must(a => a.Amount > 0).WithMessage("Budget amount must be positive");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must be after start date");

        RuleFor(x => x.CategoryId)
            .Must(id => id.Value != Guid.Empty).WithMessage("Category is required");
    }
}

public class UpdateBudgetDtoValidator : AbstractValidator<UpdateBudgetDto>
{
    public UpdateBudgetDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Budget name cannot be empty")
            .MaximumLength(100).WithMessage("Budget name cannot exceed 100 characters")
            .When(x => x.Name != null);

        RuleFor(x => x.Amount)
            .Must(a => a != null && a.Amount > 0).WithMessage("Budget amount must be positive")
            .When(x => x.Amount != null);

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required")
            .When(x => x.StartDate.HasValue);

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.EndDate >= x.StartDate)
            .WithMessage("End date must be after start date")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);

        RuleFor(x => x.CategoryId)
            .Must(id => id.HasValue && id.Value != default(CategoryId)).WithMessage("Category is required")
            .When(x => x.CategoryId.HasValue);
    }
}