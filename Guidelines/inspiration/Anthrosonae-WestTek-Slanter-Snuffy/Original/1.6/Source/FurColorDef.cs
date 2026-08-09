using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Anthrosonae
{
    public class FurColorDef : Def
    {
        public Color primaryColor;
        public Color? secondaryColor;
        public float selectionWeight;
        public int displayOrder;
        public List<GeneDef> genes;
        public bool blacklistPrimary, blacklistSecondary;
    }
}
