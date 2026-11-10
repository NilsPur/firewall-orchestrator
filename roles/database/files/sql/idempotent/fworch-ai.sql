-- The credential holding the API key of the AI provider: the one the global config item aiCredentialId names
-- (FWO.Data.Ai.AiSettings.kCredentialIdKey, set on the AI settings page). The technical role ai-harness of the AI
-- harness reads credentials only through this view, so it gets the secret of this single credential and of no other.
-- The config value is compared as text, so a value that is not a number matches no credential instead of failing.
CREATE OR REPLACE VIEW v_ai_provider_credential AS
    SELECT
        credential.id,
        credential.is_key_pair,
        credential.secret
    FROM credential
    JOIN config ON config.config_key = 'aiCredentialId'
        AND config.config_user = 0
        AND config.config_value = credential.id::VARCHAR;
