CREATE ROLE payments_runtime NOLOGIN;
GRANT CONNECT ON DATABASE payments TO payments_runtime;
GRANT USAGE, CREATE ON SCHEMA public TO payments_runtime;
