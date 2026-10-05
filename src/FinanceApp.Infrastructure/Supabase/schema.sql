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
    -- Derived: initial_balance_amount plus this account's transactions. Kept as
    -- a column so the server can show it too, but clients recompute it from
    -- the transactions rather than trusting it.
    balance_amount numeric(18,2) not null default 0,
    balance_currency text not null default 'PHP',
    -- The balance the account was opened with, in balance_currency. This is the
    -- input a balance is derived from, so it has to survive the round trip:
    -- without it another device cannot work out what an account's balance
    -- should be.
    initial_balance_amount numeric(18,2) not null default 0,
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
    color text,
    linked_account_id uuid
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
--
-- Every statement here is drop-then-create. A pasted script stops at its first
-- error, and a bare `create policy` errors the moment the policy already exists -
-- so a second run used to stop here, leaving the tables below it unprotected while
-- the `create table if not exists` statements above reported success. That is how
-- `accounts` came to be readable by an anonymous request. Dropping first makes the
-- whole file safe to run again, which is the only way it can be trusted.
alter table accounts enable row level security;
alter table categories enable row level security;
alter table transactions enable row level security;
alter table budgets enable row level security;
alter table recurring_transactions enable row level security;
alter table financial_goals enable row level security;

drop policy if exists "Users manage own accounts" on accounts;
drop policy if exists "Users manage own categories" on categories;
drop policy if exists "Users manage own transactions" on transactions;
drop policy if exists "Users manage own budgets" on budgets;
drop policy if exists "Users manage own recurring transactions" on recurring_transactions;
drop policy if exists "Users manage own financial goals" on financial_goals;

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

-- Upgrade for databases created before these columns existed (idempotent).
-- Safe to run on a fresh database - every statement is a no-op there.
alter table budgets add column if not exists icon text;
alter table budgets add column if not exists color text;
alter table budgets add column if not exists linked_account_id uuid;

-- Verify rather than assume: every table must report relrowsecurity true and
-- exactly one policy, for the authenticated role only.
select c.relname                               as table,
       c.relrowsecurity                        as rls_enabled,
       count(distinct p.polname)               as policies,
       coalesce(string_agg(distinct r.rolname, ', '), '(none)') as applies_to
from pg_class c
join pg_namespace n on n.oid = c.relnamespace
left join pg_policy p on p.polrelid = c.oid
left join pg_roles r on r.oid = any (p.polroles)
where n.nspname = 'public'
  and c.relname in ('accounts', 'categories', 'transactions', 'budgets',
                    'recurring_transactions', 'financial_goals')
group by c.relname, c.relrowsecurity
order by c.relname;
