-- Migration 0002: budget identity columns
-- Run this in Supabase dashboard -> SQL Editor ONLY if you already ran an
-- older schema.sql that created the budgets table WITHOUT these columns.
-- (Fresh installs: just run the full schema.sql instead - it includes them.)
-- Safe to run multiple times (IF NOT EXISTS).
-- Local SQLite applies the same columns automatically via EnsureExtraColumns.

alter table budgets add column if not exists icon text;
alter table budgets add column if not exists color text;
alter table budgets add column if not exists linked_account_id uuid;
