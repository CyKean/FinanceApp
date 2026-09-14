namespace FinanceApp.Domain.ValueObjects;

public readonly record struct CategoryId
{
    public Guid Value { get; }

    public CategoryId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CategoryId cannot be empty", nameof(value));

        Value = value;
    }

    public static CategoryId New() => new(Guid.NewGuid());
    public static CategoryId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(CategoryId id) => id.Value;
    public static explicit operator CategoryId(Guid value) => new(value);
}