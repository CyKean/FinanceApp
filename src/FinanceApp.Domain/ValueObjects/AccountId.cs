namespace FinanceApp.Domain.ValueObjects;

public readonly record struct AccountId
{
    public Guid Value { get; }

    public AccountId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("AccountId cannot be empty", nameof(value));

        Value = value;
    }

    public static AccountId New() => new(Guid.NewGuid());
    public static AccountId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(AccountId id) => id.Value;
    public static explicit operator AccountId(Guid value) => new(value);
}