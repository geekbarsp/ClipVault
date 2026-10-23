create policy licenses_deny_client_access on private.licenses
    for all to public using (false) with check (false);
create policy license_logs_deny_client_access on private.license_activation_logs
    for all to public using (false) with check (false);
create policy license_admins_deny_client_access on private.license_admins
    for all to public using (false) with check (false);
create policy license_rate_limits_deny_client_access on private.license_rate_limits
    for all to public using (false) with check (false);
