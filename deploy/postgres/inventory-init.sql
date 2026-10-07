CREATE ROLE inventory_runtime NOLOGIN;
CREATE ROLE inventory_migrator NOLOGIN;

GRANT CONNECT ON DATABASE inventory TO inventory_runtime;
GRANT CONNECT ON DATABASE inventory TO inventory_migrator;

GRANT USAGE ON SCHEMA public TO inventory_runtime;
GRANT USAGE, CREATE ON SCHEMA public TO inventory_migrator;

ALTER DEFAULT PRIVILEGES FOR ROLE inventory_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO inventory_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE inventory_migrator IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO inventory_runtime;
