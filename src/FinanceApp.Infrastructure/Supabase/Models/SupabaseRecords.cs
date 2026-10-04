namespace FinanceApp.Infrastructure.Supabase.Models;

using PostgrestAttributes = global::Supabase.Postgrest.Attributes;
using PostgrestModels = global::Supabase.Postgrest.Models;

/// <summary>
/// PostgREST table models. Column names must match the Supabase schema
/// (see schema.sql). Enums are stored as text, Money as amount + currency.
/// </summary>

[PostgrestAttributes.Table("accounts")]
public class AccountRecord : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [PostgrestAttributes.Column("type")]
    public string Type { get; set; } = string.Empty;

    [PostgrestAttributes.Column("balance_amount")]
    public decimal BalanceAmount { get; set; }

    [PostgrestAttributes.Column("balance_currency")]
    public string BalanceCurrency { get; set; } = "PHP";

    /// <summary>
    /// The balance the account was opened with, in <see cref="BalanceCurrency"/>.
    /// Carried separately from <see cref="BalanceAmount"/> because it is the input
    /// a balance is derived from, and the derived figure is not authoritative
    /// enough to rebuild it.
    /// </summary>
    [PostgrestAttributes.Column("initial_balance_amount")]
    public decimal InitialBalanceAmount { get; set; }

    [PostgrestAttributes.Column("description")]
    public string? Description { get; set; }

    [PostgrestAttributes.Column("icon")]
    public string? Icon { get; set; }

    [PostgrestAttributes.Column("color")]
    public string? Color { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }

    [PostgrestAttributes.Column("is_default")]
    public bool IsDefault { get; set; }

    [PostgrestAttributes.Column("sort_order")]
    public int SortOrder { get; set; }
}

/// <summary>
/// <see cref="AccountRecord"/> without the initial-balance columns, for Supabase
/// projects whose accounts table predates migration 0003. PostgREST rejects the
/// whole request when a selected column does not exist, so reading and writing
/// these accounts needs a shape the older table can answer.
/// </summary>
[PostgrestAttributes.Table("accounts")]
public class AccountRecordLite : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [PostgrestAttributes.Column("type")]
    public string Type { get; set; } = string.Empty;

    [PostgrestAttributes.Column("balance_amount")]
    public decimal BalanceAmount { get; set; }

    [PostgrestAttributes.Column("balance_currency")]
    public string BalanceCurrency { get; set; } = "PHP";

    [PostgrestAttributes.Column("description")]
    public string? Description { get; set; }

    [PostgrestAttributes.Column("icon")]
    public string? Icon { get; set; }

    [PostgrestAttributes.Column("color")]
    public string? Color { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }

    [PostgrestAttributes.Column("is_default")]
    public bool IsDefault { get; set; }

    [PostgrestAttributes.Column("sort_order")]
    public int SortOrder { get; set; }
}

[PostgrestAttributes.Table("categories")]
public class CategoryRecord : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [PostgrestAttributes.Column("type")]
    public string Type { get; set; } = string.Empty;

    [PostgrestAttributes.Column("icon")]
    public string? Icon { get; set; }

    [PostgrestAttributes.Column("color")]
    public string? Color { get; set; }

    [PostgrestAttributes.Column("parent_category_id")]
    public Guid? ParentCategoryId { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }

    [PostgrestAttributes.Column("is_system")]
    public bool IsSystem { get; set; }

    [PostgrestAttributes.Column("sort_order")]
    public int SortOrder { get; set; }

    [PostgrestAttributes.Column("is_active")]
    public bool IsActive { get; set; }
}

[PostgrestAttributes.Table("transactions")]
public class TransactionRecord : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("type")]
    public string Type { get; set; } = string.Empty;

    [PostgrestAttributes.Column("amount")]
    public decimal Amount { get; set; }

    [PostgrestAttributes.Column("currency")]
    public string Currency { get; set; } = "PHP";

    [PostgrestAttributes.Column("date")]
    public DateTime Date { get; set; }

    [PostgrestAttributes.Column("notes")]
    public string? Notes { get; set; }

    [PostgrestAttributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [PostgrestAttributes.Column("category_id")]
    public Guid CategoryId { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }

    [PostgrestAttributes.Column("recurring_transaction_id")]
    public Guid? RecurringTransactionId { get; set; }
}

[PostgrestAttributes.Table("budgets")]
public class BudgetRecord : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [PostgrestAttributes.Column("amount")]
    public decimal Amount { get; set; }

    [PostgrestAttributes.Column("currency")]
    public string Currency { get; set; } = "PHP";

    [PostgrestAttributes.Column("spent_amount")]
    public decimal SpentAmount { get; set; }

    [PostgrestAttributes.Column("start_date")]
    public DateTime StartDate { get; set; }

    [PostgrestAttributes.Column("end_date")]
    public DateTime EndDate { get; set; }

    [PostgrestAttributes.Column("category_id")]
    public Guid CategoryId { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }

    [PostgrestAttributes.Column("icon")]
    public string? Icon { get; set; }

    [PostgrestAttributes.Column("color")]
    public string? Color { get; set; }

    [PostgrestAttributes.Column("linked_account_id")]
    public Guid? LinkedAccountId { get; set; }
}

[PostgrestAttributes.Table("budgets")]
public class BudgetRecordLite : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [PostgrestAttributes.Column("amount")]
    public decimal Amount { get; set; }

    [PostgrestAttributes.Column("currency")]
    public string Currency { get; set; } = "PHP";

    [PostgrestAttributes.Column("spent_amount")]
    public decimal SpentAmount { get; set; }

    [PostgrestAttributes.Column("start_date")]
    public DateTime StartDate { get; set; }

    [PostgrestAttributes.Column("end_date")]
    public DateTime EndDate { get; set; }

    [PostgrestAttributes.Column("category_id")]
    public Guid CategoryId { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }
}

[PostgrestAttributes.Table("recurring_transactions")]
public class RecurringTransactionRecord : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [PostgrestAttributes.Column("type")]
    public string Type { get; set; } = string.Empty;

    [PostgrestAttributes.Column("amount")]
    public decimal Amount { get; set; }

    [PostgrestAttributes.Column("currency")]
    public string Currency { get; set; } = "PHP";

    [PostgrestAttributes.Column("frequency")]
    public string Frequency { get; set; } = string.Empty;

    [PostgrestAttributes.Column("start_date")]
    public DateTime StartDate { get; set; }

    [PostgrestAttributes.Column("end_date")]
    public DateTime? EndDate { get; set; }

    [PostgrestAttributes.Column("account_id")]
    public Guid AccountId { get; set; }

    [PostgrestAttributes.Column("category_id")]
    public Guid CategoryId { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }

    [PostgrestAttributes.Column("notes")]
    public string? Notes { get; set; }

    [PostgrestAttributes.Column("last_generated_at")]
    public DateTime? LastGeneratedAt { get; set; }

    [PostgrestAttributes.Column("next_due_date")]
    public DateTime? NextDueDate { get; set; }

    [PostgrestAttributes.Column("is_active")]
    public bool IsActive { get; set; }
}

[PostgrestAttributes.Table("financial_goals")]
public class FinancialGoalRecord : PostgrestModels.BaseModel
{
    [PostgrestAttributes.PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [PostgrestAttributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [PostgrestAttributes.Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [PostgrestAttributes.Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [PostgrestAttributes.Column("version")]
    public int Version { get; set; }

    [PostgrestAttributes.Column("name")]
    public string Name { get; set; } = string.Empty;

    [PostgrestAttributes.Column("target_amount")]
    public decimal TargetAmount { get; set; }

    [PostgrestAttributes.Column("target_currency")]
    public string TargetCurrency { get; set; } = "PHP";

    [PostgrestAttributes.Column("current_amount")]
    public decimal CurrentAmount { get; set; }

    [PostgrestAttributes.Column("target_date")]
    public DateTime TargetDate { get; set; }

    [PostgrestAttributes.Column("start_date")]
    public DateTime StartDate { get; set; }

    [PostgrestAttributes.Column("status")]
    public string Status { get; set; } = string.Empty;

    [PostgrestAttributes.Column("description")]
    public string? Description { get; set; }

    [PostgrestAttributes.Column("icon")]
    public string? Icon { get; set; }

    [PostgrestAttributes.Column("color")]
    public string? Color { get; set; }

    [PostgrestAttributes.Column("user_id")]
    public Guid UserId { get; set; }

    [PostgrestAttributes.Column("linked_account_id")]
    public Guid? LinkedAccountId { get; set; }
}
