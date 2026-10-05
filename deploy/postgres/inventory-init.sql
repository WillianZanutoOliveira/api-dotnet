CREATE ROLE inventory_runtime NOLOGIN;
GRANT CONNECT ON DATABASE inventory TO inventory_runtime;
GRANT USAGE, CREATE ON SCHEMA public TO inventory_runtime;
