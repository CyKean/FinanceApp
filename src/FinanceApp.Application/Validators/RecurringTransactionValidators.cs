namespace FinanceApp.Application.Validators;

using FluentValidation;
using FinanceApp.Application.DTOs;
using FinanceApp.Domain.ValueObjects;

public class CreateRecurringTransactionDtoValidator : AbstractValidator<CreateRecurringTransactionDto>
{
    public CreateRecurringTransactionDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid transaction type");

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required")
            .Must(a => a.Amount > 0).WithMessage("Amount must be positive");

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Invalid frequency");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.AccountId)
            .NotEqual(AccountId.From(Guid.Empty)).WithMessage("Account is required");

        RuleFor(x => x.CategoryId)
            .NotEqual(CategoryId.From(Guid.Empty)).WithMessage("Category is required");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate.Value >= x.StartDate)
            .WithMessage("End date must be after start date")
            .When(x => x.EndDate.HasValue);
    }
}

public class UpdateRecurringTransactionDtoValidator : AbstractValidator<UpdateRecurringTransactionDto>
{
    public UpdateRecurringTransactionDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name cannot be empty")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters")
            .When(x => x.Name != null);

        RuleFor(x => x.Amount)
            .Must(a => a != null && a.Amount > 0).WithMessage("Amount must be positive")
            .When(x => x.Amount != null);

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Invalid frequency")
            .When(x => x.Frequency.HasValue);

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required")
            .When(x => x.StartDate.HasValue);

        RuleFor(x => x.AccountId)
            .Must(id => id.HasValue && id.Value != AccountId.From(Guid.Empty)).WithMessage("Account is required")
            .When(x => x.AccountId.HasValue);

        RuleFor(x => x.CategoryId)
            .Must(id => id.HasValue && id.Value != CategoryId.From(Guid.Empty)).WithMessage("Category is required")
            .When(x => x.CategoryId.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || !x.StartDate.HasValue || x.EndDate.Value >= x.StartDate)
            .WithMessage("End date must be after start date")
            .When(x => x.EndDate.HasValue && x.StartDate.HasValue);
    }
}