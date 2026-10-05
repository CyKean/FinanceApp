-- Migration 0004: row level security on every synced table
-- Run this in Supabase dashboard -> SQL Editor.
-- Safe to run multiple times, and safe on a database that never had the policies.
--
-- Why this exists:
--   RLS was not in force on every table. A request carrying only the project's
--   anon key - no signed-in user at all - could read another user's rows out of
--   `accounts`, balances included. The policies in schema.sql are the only thing
--   standing between one user's finances and another's, so this re-applies all of
--   them unconditionally rather than trusting that they landed.
--
--   schema.sql could not have been relied on to do this. Its RLS section uses bare
--   `create policy`, which is an error the moment the policy already exists, and a
--   pasted script stops at its first error. A run that failed partway left the
--   tables it had not reached unprotected - and a `create table if not exists`
--   run over the top of that succeeds silently, so the gap looks like a working
--   database. Everything below is therefore drop-then-create.

alter table accounts enable row level security;
alter table categories enable row level security;
alter table transactions enable row level security;
alter table budgets enable row level security;
alter table recurring_transactions enable row level security;
alter table financial_goals enable row level security;

-- Dropped first so this is re-runnable, and so a policy left over from an older
-- schema - one scoped to a different role, or covering only part of the tables -
-- cannot sit alongside the replacement and keep answering for it.
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

-- Check, so a run that did not take is visible here rather than at the next sync.
-- Every row should say enabled, and every table should list exactly one policy,
-- for the authenticated role only. Anything showing relrowsecurity false or a
-- second policy has been left exposed.
select c.relname                             as table,
       c.relrowsecurity                      as rls_enabled,
       count(distinct p.polname)                      as policies,
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