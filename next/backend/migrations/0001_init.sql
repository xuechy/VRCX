-- users base table
CREATE TABLE IF NOT EXISTS users (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	name text NOT NULL,
	created_at timestamptz NOT NULL DEFAULT now()
);

-- helper extension for uuid in Postgres (if available)
CREATE EXTENSION IF NOT EXISTS pgcrypto;