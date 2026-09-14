namespace FinanceApp.Mobile.ViewModels;

using System;
using System.Reflection;

public class AccountTypeExtension
{
    public static Array GetValues()
    {
        return Enum.GetValues(typeof(FinanceApp.Domain.Enums.AccountType));
    }
}