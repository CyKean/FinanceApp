-- Migration 0003: account opening balance column
-- Run this in Supabase dashboard -> SQL Editor ONLY if you already ran an
-- older schema.sql that created the accounts table WITHOUT this column.
-- (Fresh installs: just run the full schema.sql instead - it includes it.)
-- Safe to run multiple times (IF NOT EXISTS).
-- Local SQLite applies the same column automatically via EnsureExtraColumns and
-- seeds it from the current balance.
--
-- Why this is needed: a client derives an account's balance from its opening
-- balance plus its transactions. The opening balance is the only part that
-- cannot be derived from transactions, so it has to be stored. Existing
-- accounts are seeded with the balance they currently show, which is what their
-- balance was before this change, so adopting it changes no one's balance.
--
-- The column is added without a default and seeded while it is still nullable:
-- that is what makes a second run of this script a no-op, because there is
-- nothing left that is null to seed.

alter table accounts add column if not exists initial_balance_amount numeric(18,2);

update accounts
set initial_balance_amount = balance_amount
where initial_balance_amount is null;

alter table accounts alter column initial_balance_amount set default 0;
alter table accounts alter column initial_balance_amount set not null;