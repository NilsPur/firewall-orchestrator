CREATE TABLE IF NOT EXISTS "ai_provider"
(
	"id" BIGSERIAL,
	"kind" Varchar NOT NULL,
	"display_name" Varchar NOT NULL Default '',
	"endpoint_url" Varchar NOT NULL Default '',
	"api_key_env_variable" Varchar NOT NULL Default '',
	"timeout_seconds" Integer NOT NULL Default 60,
	"max_retries" Integer NOT NULL Default 1,
	"enabled" Boolean NOT NULL Default FALSE,
	primary key ("id")
);

CREATE TABLE IF NOT EXISTS "ai_model"
(
	"id" BIGSERIAL,
	"provider_id" Bigint NOT NULL,
	"model_id" Varchar NOT NULL Default '',
	"display_name" Varchar NOT NULL Default '',
	"enabled" Boolean NOT NULL Default FALSE,
	"streaming_supported" Boolean,
	"tool_calls_supported" Boolean,
	"vision_supported" Boolean,
	"reasoning_supported" Boolean,
	"context_size" Integer,
	"temperature" Real,
	"top_p" Real,
	"top_k" Integer,
	"max_output_tokens" Integer,
	"reasoning_effort" Integer,
	primary key ("id")
);

ALTER TABLE "ai_model" DROP CONSTRAINT IF EXISTS "ai_model_provider_id_ai_provider_id_fkey";
ALTER TABLE "ai_model" ADD CONSTRAINT "ai_model_provider_id_ai_provider_id_fkey" FOREIGN KEY ("provider_id") REFERENCES "ai_provider" ("id") ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE TABLE IF NOT EXISTS "ai_session"
(
	"id" BIGSERIAL,
	"user_id" Integer NOT NULL,
	"model_id" Varchar NOT NULL Default '',
	"provider_id" Bigint NOT NULL,
	"name" Varchar NOT NULL,
	"created" Timestamp with time zone NOT NULL Default now(),
	"system_prompt" Text NOT NULL,
	"state" jsonb NOT NULL Default '{}'::jsonb,
	primary key ("id")
);

ALTER TABLE "ai_session" DROP CONSTRAINT IF EXISTS "ai_session_user_id_uiuser_uiuser_id_fkey";
ALTER TABLE "ai_session" ADD CONSTRAINT "ai_session_user_id_uiuser_uiuser_id_fkey" FOREIGN KEY ("user_id") REFERENCES "uiuser" ("uiuser_id") ON UPDATE RESTRICT ON DELETE CASCADE;

-- Sessions created before the provider foreign key existed may reference a missing provider (e.g. provider id 0).
ALTER TABLE "ai_session" ALTER COLUMN "provider_id" DROP DEFAULT;
DELETE FROM ai_session WHERE provider_id NOT IN (SELECT id FROM ai_provider);
ALTER TABLE "ai_session" DROP CONSTRAINT IF EXISTS "ai_session_provider_id_ai_provider_id_fkey";
ALTER TABLE "ai_session" ADD CONSTRAINT "ai_session_provider_id_ai_provider_id_fkey" FOREIGN KEY ("provider_id") REFERENCES "ai_provider" ("id") ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_ai_session01 on ai_session (user_id);
CREATE INDEX IF NOT EXISTS idx_ai_model01 on ai_model (provider_id);

INSERT INTO config (config_key, config_value, config_user)
VALUES ('system_prompt', 'You are the Firewall Orchestrator assistant. Answer using the user''s FWO access scope. Ground factual answers in data returned by FWO tools instead of assumptions. Use the appropriate read-only tool before answering factual FWO questions, and state when tool data is missing or insufficient.', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
VALUES ('aiLastModelId', '', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO ai_provider (id, kind, display_name, endpoint_url, api_key_env_variable, enabled)
SELECT seed.id, seed.kind, seed.display_name, seed.endpoint_url, seed.api_key_env_variable, seed.enabled
FROM (VALUES
	(1, 'Ollama', 'Ollama', 'http://127.0.0.1:11434', '', TRUE),
	(2, 'OpenAi', 'OpenAI', '', 'OPENAI_API_KEY', FALSE),
	(3, 'Anthropic', 'Anthropic', '', 'ANTHROPIC_API_KEY', FALSE),
	(4, 'Google', 'Google', '', 'GOOGLE_API_KEY', FALSE),
	(5, 'OpenAiCompatible', 'OpenAI-compatible', '', '', FALSE)
) AS seed(id, kind, display_name, endpoint_url, api_key_env_variable, enabled)
WHERE NOT EXISTS (SELECT 1 FROM ai_provider);

SELECT setval(pg_get_serial_sequence('ai_provider', 'id'), (SELECT COALESCE(MAX(id), 1) FROM ai_provider));
