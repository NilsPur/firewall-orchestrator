﻿using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ComplianceQueries : Queries
    {
        public static readonly string addNetworkZone;
        public static readonly string deleteNetworkZone;
        public static readonly string getNetworkZones;
        public static readonly string updateNetworkZones;

        static ComplianceQueries()
        {
            try
            {
                addNetworkZone = File.ReadAllText(QueryPath + "compliance/addNetworkZone.graphql");
                deleteNetworkZone = File.ReadAllText(QueryPath + "compliance/deleteNetworkZone.graphql");
                getNetworkZones = File.ReadAllText(QueryPath + "compliance/getNetworkZones.graphql");
                updateNetworkZones = File.ReadAllText(QueryPath + "compliance/updateNetworkZone.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Compliance Queries", "Api compliance queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
