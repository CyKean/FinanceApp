namespace FinanceApp.Application.Validators;

using FluentValidation;
using FinanceApp.Application.DTOs;
using FinanceApp.Domain.ValueObjects;

public class CreateFinancialGoalDtoValidator : AbstractValidator<CreateFinancialGoalDto>
{
    public CreateFinancialGoalDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Goal name is required")
            .MaximumLength(100).WithMessage("Goal name cannot exceed 100 characters");

        RuleFor(x => x.TargetAmount)
            .NotNull().WithMessage("Target amount is required")
            .Must(a => a.Amount > 0).WithMessage("Target amount must be positive");

        RuleFor(x => x.TargetDate)
            .NotEmpty().WithMessage("Target date is required")
            .GreaterThan(DateTime.Today).WithMessage("Target date must be in the future");

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.TargetDate).WithMessage("Start date must be before target date")
            .When(x => x.StartDate.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("Icon cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Icon));

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Color must be a valid hex color (e.g., #FF0000)")
            .When(x => !string.IsNullOrEmpty(x.Color));
    }
}

public class UpdateFinancialGoalDtoValidator : AbstractValidator<UpdateFinancialGoalDto>
{
    public UpdateFinancialGoalDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Goal name cannot be empty")
            .MaximumLength(100).WithMessage("Goal name cannot exceed 100 characters")
            .When(x => x.Name != null);

        RuleFor(x => x.TargetAmount)
            .Must(a => a != null && a.Amount > 0).WithMessage("Target amount must be positive")
            .When(x => x.TargetAmount != null);

        RuleFor(x => x.TargetDate)
            .NotEmpty().WithMessage("Target date is required")
            .GreaterThan(DateTime.Today).WithMessage("Target date must be in the future")
            .When(x => x.TargetDate.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("Icon cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Icon));

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Color must be a valid hex color (e.g., #FF0000)")
            .When(x => !string.IsNullOrEmpty(x.Color));

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid goal status")
            .When(x => x.Status.HasValue);
    }
}

public class GoalProgressDtoValidator : AbstractValidator<GoalProgressDto>
{
    public GoalProgressDtoValidator()
    {
        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required")
            .Must(a => a.Amount > 0).WithMessage("Amount must be positive");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}