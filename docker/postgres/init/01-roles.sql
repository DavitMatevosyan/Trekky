-- Database roles for Trekky (ADR-0002). LOCAL DEVELOPMENT AND TESTS ONLY:
-- the passwords below are public. Production runs this file too, then sets real passwords
-- (deploy/postgres/10-roles.sh).
--
-- Runs once, as the superuser, when the Postgres container starts with an empty volume
-- (docker-entrypoint-initdb.d). The integration tests run this same file.
--
--   trekky_owner  runs migrations and owns every schema and table
--   trekky_app    used by the API at runtime; no superuser, no BYPASSRLS, not an owner,
--                 so row-level security always applies to it

CREATE ROLE trekky_owner LOGIN PASSWORD 'trekky_owner_dev';
CREATE ROLE trekky_app   LOGIN PASSWORD 'trekky_app_dev' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;

DO $$
BEGIN
    EXECUTE format('GRANT CONNECT, CREATE ON DATABASE %I TO trekky_owner', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO trekky_app', current_database());
END
$$;

-- Everything trekky_owner creates later (module schemas and tables) is usable by trekky_app.
ALTER DEFAULT PRIVILEGES FOR ROLE trekky_owner GRANT USAGE ON SCHEMAS TO trekky_app;
ALTER DEFAULT PRIVILEGES FOR ROLE trekky_owner GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO trekky_app;
ALTER DEFAULT PRIVILEGES FOR ROLE trekky_owner GRANT USAGE, SELECT ON SEQUENCES TO trekky_app;
