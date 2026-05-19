using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterResearchMenu
{
    public class ResearchIconExtension : DefModExtension
    {
        public string texPath;
        public string markerTexPath;
    }

    public class ResearchFoundationExtension : DefModExtension
    {
    }

    public class EmergenceExtension : DefModExtension
    {
        public TechLevel targetLevel;
    }

    public class GroupParentExtension : DefModExtension
    {
        public GroupNodeDef groupNode;
    }

    public class GroupNodeDef : Def
    {
        public string texPath;
        public ResearchTabDef tab;
        public TechLevel techLevel = TechLevel.Undefined;
        public List<ResearchProjectDef> prerequisites;

        private Texture2D resolvedTex;
        public Texture2D GetTexture()
        {
            if (resolvedTex == null && !texPath.NullOrEmpty())
                resolvedTex = ContentFinder<Texture2D>.Get(texPath, false);
            return resolvedTex;
        }
    }

    public static class Extensions
    {
        public static bool IsFoundation(this ResearchNode node)
        {
            return !node.isPhantom && node.def.IsFoundation();
        }

        public static bool IsFoundation(this ResearchProjectDef def)
        {
            return def.HasModExtension<ResearchFoundationExtension>();
        }
    }
}
