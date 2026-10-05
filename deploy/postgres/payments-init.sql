CREATE ROLE payments_runtime NOLOGIN;
CREATE ROLE payments_migrator NOLOGIN;

GRANT CONNECT ON DATABASE payments TO payments_runtime;
GRANT CONNECT ON DATABASE payments TO payments_migrator;

GRANT USAGE ON SCHEMA public TO payments_runtime;
GRANT USAGE, CREATE ON SCHEMA public TO payments_migrator;

ALTER DEFAULT PRIVILEGES FOR ROLE payments_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO payments_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE payments_migrator IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO payments_runtime;
