namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Domain.Interfaces;

public class GuidGenerator : IGuidGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
}