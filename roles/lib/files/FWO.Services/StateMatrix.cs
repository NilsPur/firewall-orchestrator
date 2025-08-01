﻿using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using System.Text.Json.Serialization; 
using Newtonsoft.Json; 

namespace FWO.Services
{
    public enum WorkflowPhases
    {
        request = 0,
        approval = 1,
        planning = 2,
        verification = 3,
        implementation = 4,
        review = 5,
        recertification = 6
    }

    public class StateMatrix
    {
        [JsonProperty("matrix"), JsonPropertyName("matrix")]
        public Dictionary<int, List<int>> Matrix { get; set; } = [];

        [JsonProperty("derived_states"), JsonPropertyName("derived_states")]
        public Dictionary<int, int> DerivedStates { get; set; } = [];

        [JsonProperty("lowest_input_state"), JsonPropertyName("lowest_input_state")]
        public int LowestInputState { get; set; }

        [JsonProperty("lowest_start_state"), JsonPropertyName("lowest_start_state")]
        public int LowestStartedState { get; set; }

        [JsonProperty("lowest_end_state"), JsonPropertyName("lowest_end_state")]
        public int LowestEndState { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; }

        public Dictionary<WorkflowPhases, bool> PhaseActive = [];
        public bool IsLastActivePhase = true;
        public int MinImplTasksNeeded;
        public int MinTicketCompleted;

        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
        {
            GlobalStateMatrix glbStateMatrix = new ();
            await glbStateMatrix.Init(apiConnection, taskType);
            Matrix = glbStateMatrix.GlobalMatrix[phase].Matrix;
            DerivedStates = glbStateMatrix.GlobalMatrix[phase].DerivedStates;
            LowestInputState = glbStateMatrix.GlobalMatrix[phase].LowestInputState;
            LowestStartedState = glbStateMatrix.GlobalMatrix[phase].LowestStartedState;
            LowestEndState = glbStateMatrix.GlobalMatrix[phase].LowestEndState;
            Active = glbStateMatrix.GlobalMatrix[phase].Active;
            foreach (var phas in glbStateMatrix.GlobalMatrix)
            {
                PhaseActive.Add(phas.Key, glbStateMatrix.GlobalMatrix[phas.Key].Active);
                if(glbStateMatrix.GlobalMatrix[phas.Key].Active && phas.Key > phase)
                {
                    IsLastActivePhase = false;
                }
            }
            MinImplTasksNeeded = glbStateMatrix.GlobalMatrix[WorkflowPhases.implementation].LowestInputState;
            MinTicketCompleted = glbStateMatrix.GlobalMatrix[PhaseActive.LastOrDefault(p => p.Value == true).Key].LowestEndState;
        }

        public bool getNextActivePhase(ref WorkflowPhases phase)
        {
            foreach (var tmpPhase in PhaseActive)
            {
                if (tmpPhase.Key > phase && tmpPhase.Value)
                {
                    phase = tmpPhase.Key;
                    return true;
                }
            }
            return false;
        }

        public List<int> getAllowedTransitions(int stateIn)
        {
            return Matrix.TryGetValue(stateIn, out List<int>? value) ? value : [];
        }

        public int getDerivedStateFromSubStates(List<int> statesIn)
        {
            if(statesIn.Count == 0)
            {
                return 0;
            }
            int stateOut;
            int backAssignedState = LowestInputState;
            int initState = 0;
            int inWorkState = LowestEndState;
            int minFinishedState = 999;
            int backAssignedTasks = 0;
            int openTasks = 0;
            int inWorkTasks = 0;
            int finishedTasks = 0;
            foreach(int state in statesIn)
            {
                if(state < LowestInputState)
                {
                    backAssignedTasks++;
                    if(state < backAssignedState)
                    {
                        backAssignedState = state;
                    }
                }
                else if(state < LowestStartedState)
                {
                    openTasks++;
                    initState = state;
                }
                else if(state < LowestEndState)
                {
                    inWorkTasks++;
                    if(state < inWorkState)
                    {
                        inWorkState = state;
                    }
                }
                else
                {
                    finishedTasks++;
                    if(state < minFinishedState)
                    {
                        minFinishedState = state;
                    }
                }
            }

            if(backAssignedTasks > 0)
            {
                stateOut = backAssignedState;
            }
            else if(inWorkTasks > 0)
            {
                stateOut = inWorkState;
            }
            else if(finishedTasks == statesIn.Count)
            {
                stateOut = minFinishedState;
            }
            else if(openTasks == statesIn.Count)
            {
                stateOut = initState;
            }
            else
            {
                stateOut = LowestStartedState;
            }

            if(DerivedStates.ContainsKey(stateOut))
            {
                return DerivedStates[stateOut];
            }
            return stateOut;
        }
    }

    public class GlobalStateMatrix
    {
        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public Dictionary<WorkflowPhases, StateMatrix> GlobalMatrix { get; set; } = [];


        public async Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master, bool reset = false)
        {
            string matrixKey = taskType switch
            {
                WfTaskType.master => "reqMasterStateMatrix",
                WfTaskType.generic => "reqGenStateMatrix",
                WfTaskType.access => "reqAccStateMatrix",
                WfTaskType.rule_delete => "reqRulDelStateMatrix",
                WfTaskType.rule_modify => "reqRulModStateMatrix",
                WfTaskType.group_create => "reqGrpCreStateMatrix",
                WfTaskType.group_modify => "reqGrpModStateMatrix",
                WfTaskType.group_delete => "reqGrpDelStateMatrix",
                WfTaskType.new_interface => "reqNewIntStateMatrix",
                _ => throw new NotSupportedException($"Error: wrong task type:" + taskType.ToString()),
            };

            if(reset)
            {
                matrixKey += "Default";
            }

            List<GlobalStateMatrixHelper> confData = await apiConnection.SendQueryAsync<List<GlobalStateMatrixHelper>>(ConfigQueries.getConfigItemByKey, new { key = matrixKey });
            GlobalStateMatrix glbStateMatrix = System.Text.Json.JsonSerializer.Deserialize<GlobalStateMatrix>(confData[0].ConfData) ?? throw new JsonException("Config data could not be parsed.");
            GlobalMatrix = glbStateMatrix.GlobalMatrix;
        }
    }

    public class GlobalStateMatrixHelper
    {
        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public string ConfData = "";
    }

    public class StateMatrixDict
    {
        public Dictionary<string, StateMatrix> Matrices { get; set; } = [];

        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection)
        {
            Matrices = [];
            foreach(WfTaskType taskType in Enum.GetValues(typeof(WfTaskType)))
            {
                Matrices.Add(taskType.ToString(), new StateMatrix());
                await Matrices[taskType.ToString()].Init(phase, apiConnection, taskType);
            }
        }
    }
}
