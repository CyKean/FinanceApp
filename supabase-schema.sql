-- ============================================================================
-- FinanceApp Supabase Database Schema
-- PostgreSQL with Row Level Security (RLS)
-- ============================================================================

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- Custom Types
-- ============================================================================

CREATE TYPE transaction_type AS ENUM ('expense', 'income');
CREATE TYPE account_type AS ENUM ('cash', 'bank', 'savings', 'ewallet', 'creditcard', 'other');
CREATE TYPE category_type AS ENUM ('expense', 'income');
CREATE TYPE sync_status AS ENUM ('synced', 'pending_create', 'pending_update', 'pending_delete', 'failed');
CREATE TYPE sync_operation_type AS ENUM ('create', 'update', 'delete');
CREATE TYPE recurring_frequency AS ENUM ('daily', 'weekly', 'monthly', 'yearly');
CREATE TYPE goal_status AS ENUM ('active', 'completed', 'paused', 'cancelled');
CREATE TYPE prediction_confidence AS ENUM ('low', 'moderate', 'high', 'insufficient_data');
CREATE TYPE spending_trend AS ENUM ('increasing', 'decreasing', 'stable');

-- ============================================================================
-- Profiles Table (extends Supabase Auth users)
-- ============================================================================

CREATE TABLE profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    email TEXT NOT NULL UNIQUE,
    display_name TEXT,
    avatar_url TEXT,
    default_currency TEXT DEFAULT 'PHP',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- RLS for profiles
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view own profile" ON profiles
    FOR SELECT USING (auth.uid() = id);

CREATE POLICY "Users can update own profile" ON profiles
    FOR UPDATE USING (auth.uid() = id);

CREATE POLICY "Users can insert own profile" ON profiles
    FOR INSERT WITH CHECK (auth.uid() = id);

-- ============================================================================
-- Accounts Table
-- ============================================================================

CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    type account_type NOT NULL,
    balance_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    balance_currency TEXT NOT NULL DEFAULT 'PHP',
    description TEXT,
    icon TEXT,
    color TEXT,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_synced_at TIMESTAMPTZ,
    sync_status sync_status NOT NULL DEFAULT 'pending_create'
);

-- Indexes
CREATE INDEX idx_accounts_user_id ON accounts(user_id);
CREATE INDEX idx_accounts_user_id_default ON accounts(user_id, is_default) WHERE is_default = TRUE;
CREATE INDEX idx_accounts_sync_status ON accounts(sync_status) WHERE sync_status != 'synced';

-- RLS
ALTER TABLE accounts ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view own accounts" ON accounts
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own accounts" ON accounts
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own accounts" ON accounts
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own accounts" ON accounts
    FOR DELETE USING (auth.uid() = user_id);

-- ============================================================================
-- Categories Table
-- ============================================================================

CREATE TABLE categories (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    type category_type NOT NULL,
    icon TEXT,
    color TEXT,
    parent_category_id UUID REFERENCES categories(id) ON DELETE SET NULL,
    is_system BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_synced_at TIMESTAMPTZ,
    sync_status sync_status NOT NULL DEFAULT 'pending_create'
);

-- Indexes
CREATE INDEX idx_categories_user_id ON categories(user_id);
CREATE INDEX idx_categories_user_type ON categories(user_id, type);
CREATE INDEX idx_categories_user_type_active ON categories(user_id, type, is_active) WHERE is_active = TRUE;
CREATE INDEX idx_categories_parent ON categories(parent_category_id);
CREATE INDEX idx_categories_sync_status ON categories(sync_status) WHERE sync_status != 'synced';

-- RLS
ALTER TABLE categories ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view own categories" ON categories
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own categories" ON categories
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own categories" ON categories
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own categories" ON categories
    FOR DELETE USING (auth.uid() = user_id);

-- ============================================================================
-- Transactions Table
-- ============================================================================

CREATE TABLE transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    type transaction_type NOT NULL,
    amount_amount DECIMAL(18,2) NOT NULL,
    amount_currency TEXT NOT NULL DEFAULT 'PHP',
    date DATE NOT NULL,
    notes TEXT,
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    category_id UUID NOT NULL REFERENCES categories(id) ON DELETE RESTRICT,
    recurring_transaction_id UUID REFERENCES recurring_transactions(id) ON DELETE SET NULL,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_synced_at TIMESTAMPTZ,
    sync_status sync_status NOT NULL DEFAULT 'pending_create'
);

-- Indexes
CREATE INDEX idx_transactions_user_id ON transactions(user_id);
CREATE INDEX idx_transactions_user_date ON transactions(user_id, date DESC);
CREATE INDEX idx_transactions_user_type ON transactions(user_id, type);
CREATE INDEX idx_transactions_user_account ON transactions(user_id, account_id);
CREATE INDEX idx_transactions_user_category ON transactions(user_id, category_id);
CREATE INDEX idx_transactions_recurring ON transactions(recurring_transaction_id);
CREATE INDEX idx_transactions_sync_status ON transactions(sync_status) WHERE sync_status != 'synced';

-- RLS
ALTER TABLE transactions ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view own transactions" ON transactions
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own transactions" ON transactions
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own transactions" ON transactions
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own transactions" ON transactions
    FOR DELETE USING (auth.uid() = user_id);

-- ============================================================================
-- Budgets Table
-- ============================================================================

CREATE TABLE budgets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    amount_amount DECIMAL(18,2) NOT NULL,
    amount_currency TEXT NOT NULL DEFAULT 'PHP',
    spent_amount_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    spent_amount_currency TEXT NOT NULL DEFAULT 'PHP',
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    category_id UUID NOT NULL REFERENCES categories(id) ON DELETE RESTRICT,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_synced_at TIMESTAMPTZ,
    sync_status sync_status NOT NULL DEFAULT 'pending_create'
);

-- Indexes
CREATE INDEX idx_budgets_user_id ON budgets(user_id);
CREATE INDEX idx_budgets_user_dates ON budgets(user_id, start_date, end_date);
CREATE INDEX idx_budgets_user_category ON budgets(user_id, category_id);
CREATE INDEX idx_budgets_sync_status ON budgets(sync_status) WHERE sync_status != 'synced';

-- RLS
ALTER TABLE budgets ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view own budgets" ON budgets
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own budgets" ON budgets
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own budgets" ON budgets
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own budgets" ON budgets
    FOR DELETE USING (auth.uid() = user_id);

-- ============================================================================
-- Recurring Transactions Table
-- ============================================================================

CREATE TABLE recurring_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    type transaction_type NOT NULL,
    amount_amount DECIMAL(18,2) NOT NULL,
    amount_currency TEXT NOT NULL DEFAULT 'PHP',
    frequency recurring_frequency NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE,
    account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    category_id UUID NOT NULL REFERENCES categories(id) ON DELETE RESTRICT,
    notes TEXT,
    last_generated_at TIMESTAMPTZ,
    next_due_date DATE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_synced_at TIMESTAMPTZ,
    sync_status sync_status NOT NULL DEFAULT 'pending_create'
);

-- Indexes
CREATE INDEX idx_recurring_user_id ON recurring_transactions(user_id);
CREATE INDEX idx_recurring_user_active ON recurring_transactions(user_id, is_active) WHERE is_active = TRUE;
CREATE INDEX idx_recurring_next_due ON recurring_transactions(next_due_date) WHERE next_due_date IS NOT NULL;
CREATE INDEX idx_recurring_sync_status ON recurring_transactions(sync_status) WHERE sync_status != 'synced';

-- RLS
ALTER TABLE recurring_transactions ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view own recurring transactions" ON recurring_transactions
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own recurring transactions" ON recurring_transactions
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own recurring transactions" ON recurring_transactions
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own recurring transactions" ON recurring_transactions
    FOR DELETE USING (auth.uid() = user_id);

-- ============================================================================
-- Financial Goals Table
-- ============================================================================

CREATE TABLE financial_goals (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    target_amount_amount DECIMAL(18,2) NOT NULL,
    target_amount_currency TEXT NOT NULL DEFAULT 'PHP',
    current_amount_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    current_amount_currency TEXT NOT NULL DEFAULT 'PHP',
    target_date DATE NOT NULL,
    start_date DATE NOT NULL,
    status goal_status NOT NULL DEFAULT 'active',
    description TEXT,
    icon TEXT,
    color TEXT,
    linked_account_id UUID REFERENCES accounts(id) ON DELETE SET NULL,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_synced_at TIMESTAMPTZ,
    sync_status sync_status NOT NULL DEFAULT 'pending_create'
);

-- Indexes
CREATE INDEX idx_goals_user_id ON financial_goals(user_id);
CREATE INDEX idx_goals_user_status ON financial_goals(user_id, status);
CREATE INDEX idx_goals_sync_status ON financial_goals(sync_status) WHERE sync_status != 'synced';

-- RLS
ALTER TABLE financial_goals ENABLE ROW LEVEL SECURITY.

CREATE POLICY "Users can view own financial goals" ON financial_goals
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own financial goals" ON financial_goals
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own financial goals" ON financial_goals
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own financial goals" ON financial_goals
    FOR DELETE USING (auth.uid() = user_id);

-- ============================================================================
-- Sync Operations Table
-- ============================================================================

CREATE TABLE sync_operations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    entity_type TEXT NOT NULL,
    entity_id UUID NOT NULL,
    operation_type sync_operation_type NOT NULL,
    status sync_status NOT NULL DEFAULT 'pending_create',
    payload JSONB,
    retry_count INTEGER NOT NULL DEFAULT 0,
    last_attempt_at TIMESTAMPTZ,
    error_message TEXT,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX idx_sync_user_id ON sync_operations(user_id);
CREATE INDEX idx_sync_user_status ON sync_operations(user_id, status) WHERE status != 'synced';
CREATE INDEX idx_sync_entity ON sync_operations(entity_type, entity_id);
CREATE INDEX idx_sync_retry ON sync_operations(status, last_attempt_at) WHERE status = 'failed';

-- RLS
ALTER TABLE sync_operations ENABLE ROW LEVEL SECURITY.

CREATE POLICY "Users can view own sync operations" ON sync_operations
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own sync operations" ON sync_operations
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own sync operations" ON sync_operations
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own sync operations" ON sync_operations
    FOR DELETE USING (auth.uid() = user_id);

-- ============================================================================
-- Updated_at Trigger Function
-- ============================================================================

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply triggers
CREATE TRIGGER update_profiles_updated_at BEFORE UPDATE ON profiles
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_accounts_updated_at BEFORE UPDATE ON accounts
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_categories_updated_at BEFORE UPDATE ON categories
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_transactions_updated_at BEFORE UPDATE ON transactions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_budgets_updated_at BEFORE UPDATE ON budgets
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_recurring_transactions_updated_at BEFORE UPDATE ON recurring_transactions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_financial_goals_updated_at BEFORE UPDATE ON financial_goals
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_sync_operations_updated_at BEFORE UPDATE ON sync_operations
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- Helper Functions for Sync
-- ============================================================================

-- Function to mark entity as synced
CREATE OR REPLACE FUNCTION mark_synced(p_table TEXT, p_id UUID)
RETURNS VOID AS $$
BEGIN
    EXECUTE format('UPDATE %I SET sync_status = ''synced'', last_synced_at = NOW() WHERE id = $1', p_table)
    USING p_id;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Function to check if user owns entity
CREATE OR REPLACE FUNCTION user_owns_entity(p_table TEXT, p_id UUID, p_user_id UUID)
RETURNS BOOLEAN AS $$
DECLARE
    v_count INTEGER;
BEGIN
    EXECUTE format('SELECT COUNT(*) FROM %I WHERE id = $1 AND user_id = $2', p_table)
    INTO v_count
    USING p_id, p_user_id;
    RETURN v_count > 0;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- ============================================================================
-- Default Categories Seed Data (run after user registration)
-- ============================================================================

-- This would be called via a Postgres function or client-side after user signs up
-- System categories are inserted with is_system = TRUE

-- ============================================================================
-- Storage for receipts (optional)
-- ============================================================================

-- INSERT INTO storage.buckets (id, name, public) VALUES ('receipts', 'receipts', false);

-- ============================================================================
-- Realtime publications (for live updates)
-- ============================================================================

-- ALTER PUBLICATION supabase_realtime ADD TABLE accounts;
-- ALTER PUBLICATION supabase_realtime ADD TABLE categories;
-- ALTER PUBLICATION supabase_realtime ADD TABLE transactions;
-- ALTER PUBLICATION supabase_realtime ADD TABLE budgets;
-- ALTER PUBLICATION supabase_realtime ADD TABLE recurring_transactions;
-- ALTER PUBLICATION supabase_realtime ADD TABLE financial_goals;
-- ALTER PUBLICATION supabase_realtime ADD TABLE sync_operations;

-- ============================================================================
-- End of Schema
-- ============================================================================