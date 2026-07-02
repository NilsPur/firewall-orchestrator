using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class AiQueries : Queries
    {
        public static readonly string getAiSessionsForUser;
        public static readonly string getAllAiSessions;
        public static readonly string getAiSessionById;
        public static readonly string addAiSession;
        public static readonly string renameAiSession;
        public static readonly string deleteAiSession;
        public static readonly string saveAiSessionState;
        public static readonly string getAiSettings;
        public static readonly string getAiConfig;
        public static readonly string getAiProviders;
        public static readonly string saveAiSettings;

        static AiQueries()
        {
            try
            {
                string sessionFragment = GetQueryText("ai/fragments/aiSessionDetails.graphql");
                getAiSessionsForUser = sessionFragment + GetQueryText("ai/getAiSessionsForUser.graphql");
                getAllAiSessions = sessionFragment + GetQueryText("ai/getAllAiSessions.graphql");
                getAiSessionById = sessionFragment + GetQueryText("ai/getAiSessionById.graphql");
                addAiSession = GetQueryText("ai/addAiSession.graphql");
                renameAiSession = GetQueryText("ai/renameAiSession.graphql");
                deleteAiSession = GetQueryText("ai/deleteAiSession.graphql");
                saveAiSessionState = GetQueryText("ai/saveAiSessionState.graphql");
                getAiSettings = GetQueryText("ai/getAiSettings.graphql");
                getAiConfig = GetQueryText("ai/getAiConfig.graphql");
                getAiProviders = GetQueryText("ai/getAiProviders.graphql");
                saveAiSettings = GetQueryText("ai/saveAiSettings.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize AiQueries", "Api AiQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
