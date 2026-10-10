CREATE ROLE customers_runtime NOLOGIN;
CREATE ROLE customers_migrator NOLOGIN;

GRANT CONNECT ON DATABASE customers TO customers_runtime;
GRANT CONNECT ON DATABASE customers TO customers_migrator;

GRANT USAGE ON SCHEMA public TO customers_runtime;
GRANT USAGE, CREATE ON SCHEMA public TO customers_migrator;

ALTER DEFAULT PRIVILEGES FOR ROLE customers_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO customers_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE customers_migrator IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO customers_runtime;
