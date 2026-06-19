using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterResearchMenu
{
    public enum NodeState { Hidden, Dot, Minimized, Expanded }
    [HotSwappable]
    public static class State
    {
        public static Dictionary<string, NodeState> nodeStates = [];
        public static Dictionary<string, Vector2> nodePositions = [];
        public static List<string> expandedNodeOrder = [];
        public static HashSet<string> openedNodes = [];
        public static List<string> researchQueue = [];
        public static TechLevel startingScenarioTechLevel = TechLevel.Undefined;
        public static TechLevel currentSavedTechLevel = TechLevel.Undefined;
        public static bool initialized = false;

        public static void Clear()
        {
            nodeStates = [];
            nodePositions = [];
            expandedNodeOrder = [];
            openedNodes = [];
            researchQueue = [];
            startingScenarioTechLevel = TechLevel.Undefined;
            currentSavedTechLevel = TechLevel.Undefined;
            initialized = false;
        }

        public static void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && Faction.OfPlayer != null)
            {
                currentSavedTechLevel = Faction.OfPlayer.def.techLevel;
            }

            Scribe_Collections.Look(ref nodeStates, "BRM_NodeStates", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref nodePositions, "BRM_NodePositions", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref expandedNodeOrder, "BRM_ExpandedNodeOrder", LookMode.Value);
            Scribe_Collections.Look(ref openedNodes, "BRM_OpenedNodes", LookMode.Value);
            Scribe_Collections.Look(ref researchQueue, "BRM_ResearchQueue", LookMode.Value);
            Scribe_Values.Look(ref startingScenarioTechLevel, "BRM_StartingScenarioTechLevel", TechLevel.Undefined);
            Scribe_Values.Look(ref currentSavedTechLevel, "BRM_CurrentSavedTechLevel", TechLevel.Undefined);
            Scribe_Values.Look(ref initialized, "BRM_Initialized", false);

            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                nodeStates ??= [];
                nodePositions ??= [];
                expandedNodeOrder ??= [];
                openedNodes ??= [];
                researchQueue ??= [];

                if (currentSavedTechLevel != TechLevel.Undefined && Faction.OfPlayer != null)
                {
                    Faction.OfPlayer.def.techLevel = currentSavedTechLevel;
                }

                if (startingScenarioTechLevel == TechLevel.Undefined && Faction.OfPlayer != null)
                    startingScenarioTechLevel = Faction.OfPlayer.def.techLevel;
            }
        }
    }
}
