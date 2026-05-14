using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BetterResearchMenu
{
    public enum NodeState { Hidden, Dot, Minimized, Expanded }

    public static class State
    {
        public static Dictionary<string, NodeState> nodeStates = new Dictionary<string, NodeState>();
        public static Dictionary<string, Vector2> nodePositions = new Dictionary<string, Vector2>();

        public static void Clear()
        {
            nodeStates = new Dictionary<string, NodeState>();
            nodePositions = new Dictionary<string, Vector2>();
        }

        public static void ExposeData()
        {
            Scribe_Collections.Look(ref nodeStates, "BRM_NodeStates", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref nodePositions, "BRM_NodePositions", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                nodeStates ??= new Dictionary<string, NodeState>();
                nodePositions ??= new Dictionary<string, Vector2>();
            }
        }
    }
}
