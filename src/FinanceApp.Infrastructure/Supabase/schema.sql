-- FinanceApp Supabase schema
-- Run this in the Supabase dashboard: SQL Editor -> New query -> paste -> Run.
-- Requires the pgcrypto extension for gen_random_uuid() (enabled by default).

create table if not exists accounts (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    is_deleted boolean not null default false,
    version integer not null default 1,
    name text not null,
    type text not null,
    balance_amount numeric(18,2) not null default 0,
    balance_currency text not null default 'PHP',
    description text,
    icon text,
    color text,
    user_id uuid not null,
    is_default boolean not null default false,
    sort_order integer not null default 0
);

create table if not exists categories (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    is_deleted boolean not null default false,
    version integer not null default 1,
    name text not null,
    type text not null,
    icon text,
    color text,
    parent_category_id uuid,
    user_id uuid not null,
    is_system boolean not null default false,
    sort_order integer not null default 0,
    is_active boolean not null default true
);

create table if not exists transactions (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    is_deleted boolean not null default false,
    version integer not null default 1,
    type text not null,
    amount numeric(18,2) not null,
    currency text not null default 'PHP',
    date date not null,
    notes text,
    account_id uuid not null,
    category_id uuid not null,
    user_id uuid not null,
    recurring_transaction_id uuid
);

create table if not exists budgets (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    is_deleted boolean not null default false,
    version integer not null default 1,
    name text not null,
    amount numeric(18,2) not null,
    currency text not null default 'PHP',
    spent_amount numeric(18,2) not null default 0,
    start_date date not null,
    end_date date not null,
    category_id uuid not null,
    user_id uuid not null,
    icon text,
    color text
);

create table if not exists recurring_transactions (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    is_deleted boolean not null default false,
    version integer not null default 1,
    name text not null,
    type text not null,
    amount numeric(18,2) not null,
    currency text not null default 'PHP',
    frequency text not null,
    start_date date not null,
    end_date date,
    account_id uuid not null,
    category_id uuid not null,
    user_id uuid not null,
    notes text,
    last_generated_at timestamptz,
    next_due_date timestamptz,
    is_active boolean not null default true
);

create table if not exists financial_goals (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    is_deleted boolean not null default false,
    version integer not null default 1,
    name text not null,
    target_amount numeric(18,2) not null,
    target_currency text not null default 'PHP',
    current_amount numeric(18,2) not null default 0,
    target_date date not null,
    start_date date not null,
    status text not null,
    description text,
    icon text,
    color text,
    user_id uuid not null,
    linked_account_id uuid
);

-- Row Level Security: users can only touch their own rows.
alter table accounts enable row level security;
alter table categories enable row level security;
alter table transactions enable row level security;
alter table budgets enable row level security;
alter table recurring_transactions enable row level security;
alter table financial_goals enable row level security;

create policy "Users manage own accounts"
    on accounts for all to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "Users manage own categories"
    on categories for all to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "Users manage own transactions"
    on transactions for all to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "Users manage own budgets"
    on budgets for all to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "Users manage own recurring transactions"
    on recurring_transactions for all to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "Users manage own financial goals"
    on financial_goals for all to authenticated using (auth.uid() = user_id) with check (auth.uid() = user_id);
