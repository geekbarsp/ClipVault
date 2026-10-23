create schema if not exists private;

create table private.licenses (
    id uuid primary key default gen_random_uuid(),
    key_hash bytea not null unique,
    key_hint text not null,
    hwid_hash text,
    status text not null default 'active' check (status in ('active', 'suspended', 'revoked')),
    created_at timestamptz not null default now(),
    activated_at timestamptz,
    expires_at timestamptz,
    last_seen_at timestamptz,
    offline_grace_days integer not null default 7 check (offline_grace_days between 0 and 30),
    constraint licenses_hwid_hash_format check (hwid_hash is null or hwid_hash ~ '^[a-f0-9]{64}$')
);

create table private.license_activation_logs (
    id bigint generated always as identity primary key,
    license_id uuid references private.licenses(id) on delete set null,
    action text not null,
    result text not null,
    created_at timestamptz not null default now()
);

create table private.license_admins (
    user_id uuid primary key references auth.users(id) on delete cascade,
    created_at timestamptz not null default now()
);

create table private.license_rate_limits (
    request_hash text not null,
    window_start timestamptz not null,
    request_count integer not null default 1,
    primary key (request_hash, window_start)
);

create index licenses_status_idx on private.licenses(status);
create index licenses_expires_at_idx on private.licenses(expires_at);
create index license_activation_logs_license_created_idx
    on private.license_activation_logs(license_id, created_at desc);
create index license_activation_logs_created_idx
    on private.license_activation_logs(created_at desc);

alter table private.licenses enable row level security;
alter table private.licenses force row level security;
alter table private.license_activation_logs enable row level security;
alter table private.license_activation_logs force row level security;
alter table private.license_admins enable row level security;
alter table private.license_admins force row level security;
alter table private.license_rate_limits enable row level security;
alter table private.license_rate_limits force row level security;

revoke all on schema private from public, anon, authenticated;
revoke all on all tables in schema private from public, anon, authenticated;
revoke all on all sequences in schema private from public, anon, authenticated;

comment on schema private is 'Server-only ClipVault licensing data. Never contains clipboard content.';
comment on table private.licenses is 'Hashed license keys and one-device activation state.';
comment on table private.license_activation_logs is 'Minimal licensing audit events; no raw keys, HWIDs, IP addresses, or clipboard content.';
