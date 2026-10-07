namespace FinanceApp.Application.Validators;

using FluentValidation;
using FinanceApp.Application.DTOs;
using FinanceApp.Domain.ValueObjects;

public class CreateTransactionDtoValidator : AbstractValidator<CreateTransactionDto>
{
    public CreateTransactionDtoValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid transaction type");

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required")
            .Must(a => a.Amount > 0).WithMessage("Amount must be positive")
            .Must(a => a.Amount == decimal.Round(a.Amount, 2)).WithMessage("Amount cannot have more than two decimal places")
            .Must(a => a.Amount <= 99_999_999m).WithMessage("Amount is too large");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required")
            .LessThanOrEqualTo(DateTime.Today.AddDays(1)).WithMessage("Date cannot be in the future");

        RuleFor(x => x.AccountId)
            .NotEqual(default(AccountId)).WithMessage("Account is required");

        RuleFor(x => x.CategoryId)
            .NotEqual(default(CategoryId)).WithMessage("Category is required");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class UpdateTransactionDtoValidator : AbstractValidator<UpdateTransactionDto>
{
    public UpdateTransactionDtoValidator()
    {
        RuleFor(x => x.Amount)
            .Must(a => a != null && a.Amount > 0).WithMessage("Amount must be positive")
            .Must(a => a == null || a.Amount == decimal.Round(a.Amount, 2)).WithMessage("Amount cannot have more than two decimal places")
            .Must(a => a == null || a.Amount <= 99_999_999m).WithMessage("Amount is too large")
            .When(x => x.Amount != null);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid transaction type")
            .When(x => x.Type.HasValue);

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required")
            .LessThanOrEqualTo(DateTime.Today.AddDays(1)).WithMessage("Date cannot be in the future")
            .When(x => x.Date.HasValue);

        RuleFor(x => x.AccountId)
            .Must(id => id.HasValue && id.Value != default(AccountId)).WithMessage("Account is required")
            .When(x => x.AccountId.HasValue);

        RuleFor(x => x.CategoryId)
            .Must(id => id.HasValue && id.Value != default(CategoryId)).WithMessage("Category is required")
            .When(x => x.CategoryId.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class TransactionFilterDtoValidator : AbstractValidator<TransactionFilterDto>
{
    public TransactionFilterDtoValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(200).WithMessage("Page size cannot exceed 200");

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate).WithMessage("Start date must be before end date")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}