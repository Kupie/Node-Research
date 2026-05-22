using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterResearchMenu
{
    public enum NodeState { Hidden, Dot, Minimized, Expanded }

    public static class State
    {
        public static Dictionary<string, NodeState> nodeStates = [];
        public static Dictionary<string, Vector2> nodePositions = [];
        public static List<string> expandedNodeOrder = [];
        public static HashSet<string> openedNodes = [];
        public static TechLevel startingScenarioTechLevel = TechLevel.Undefined;
        public static bool initialized = false;

        public static void Clear()
        {
            nodeStates = [];
            nodePositions = [];
            expandedNodeOrder = [];
            openedNodes = [];
            startingScenarioTechLevel = TechLevel.Undefined;
            initialized = false;
        }

        public static void ExposeData()
        {
            Scribe_Collections.Look(ref nodeStates, "BRM_NodeStates", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref nodePositions, "BRM_NodePositions", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref expandedNodeOrder, "BRM_ExpandedNodeOrder", LookMode.Value);
            Scribe_Collections.Look(ref openedNodes, "BRM_OpenedNodes", LookMode.Value);
            Scribe_Values.Look(ref startingScenarioTechLevel, "BRM_StartingScenarioTechLevel", TechLevel.Undefined);
            Scribe_Values.Look(ref initialized, "BRM_Initialized", false);

            nodeStates ??= [];
            nodePositions ??= [];
            expandedNodeOrder ??= [];
            openedNodes ??= [];

            if (startingScenarioTechLevel == TechLevel.Undefined && Faction.OfPlayer != null)
                startingScenarioTechLevel = Faction.OfPlayer.def.techLevel;
        }
    }
}
