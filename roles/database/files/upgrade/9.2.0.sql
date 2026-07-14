CREATE TABLE IF NOT EXISTS "ai_provider"
(
	"id" BIGSERIAL,
	"kind" Varchar NOT NULL,
	"display_name" Varchar NOT NULL Default '',
	"endpoint_url" Varchar NOT NULL Default '',
	"api_key_env_variable" Varchar NOT NULL Default '',
	"timeout_seconds" Integer NOT NULL Default 60,
	"max_retries" Integer NOT NULL Default 1,
	primary key ("id")
);

ALTER TABLE "ai_provider" DROP COLUMN IF EXISTS "enabled";

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
	"name" Varchar NOT NULL,
	"created" Timestamp with time zone NOT NULL Default now(),
	"system_prompt" Text NOT NULL,
	"state" Text NOT NULL Default '',
	primary key ("id")
);

ALTER TABLE "ai_session" DROP CONSTRAINT IF EXISTS "ai_session_user_id_uiuser_uiuser_id_fkey";
ALTER TABLE "ai_session" ADD CONSTRAINT "ai_session_user_id_uiuser_uiuser_id_fkey" FOREIGN KEY ("user_id") REFERENCES "uiuser" ("uiuser_id") ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_ai_session01 on ai_session (user_id);
CREATE INDEX IF NOT EXISTS idx_ai_model01 on ai_model (provider_id);

INSERT INTO config (config_key, config_value, config_user)
VALUES ('aiAssistantActive', 'true', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
VALUES ('system_prompt', 'You are the Firewall Orchestrator assistant. Answer using the user''s FWO access scope. Ground factual answers in data returned by FWO tools instead of assumptions. Use the appropriate read-only tool before answering factual FWO questions, and state when tool data is missing or insufficient.', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO ai_provider (id, kind, display_name, endpoint_url, api_key_env_variable)
SELECT 1, 'OpenAi', 'OpenAI', '', 'OPENAI_API_KEY'
WHERE NOT EXISTS (SELECT 1 FROM ai_provider);

INSERT INTO ai_model (provider_id, model_id, display_name, enabled, streaming_supported, tool_calls_supported, vision_supported, reasoning_supported, context_size, max_output_tokens, reasoning_effort)
SELECT 1, 'gpt-5.4-mini', 'GPT-5.4 mini', TRUE, TRUE, TRUE, FALSE, TRUE, 400000, 128000, 2
WHERE NOT EXISTS (SELECT 1 FROM ai_model)
AND EXISTS (SELECT 1 FROM ai_provider WHERE id = 1);

SELECT setval(pg_get_serial_sequence('ai_provider', 'id'), (SELECT COALESCE(MAX(id), 1) FROM ai_provider));
