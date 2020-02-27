using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using System;
using RimWorld;
using System.Linq;

namespace BionicIcons
{
    [StaticConstructorOnStartup]
    public class BionicIcons
    {
        static MethodInfo graphicDataInit = typeof(GraphicData).GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Instance);
        static Dictionary<string, List<BionicIconsTextureDef>> replacements = new Dictionary<string, List<BionicIconsTextureDef>>();
        static Dictionary<BodyPartDef, List<BionicIconsIconDef>> replacementsIcon = new Dictionary<BodyPartDef, List<BionicIconsIconDef>>();

        static BionicIcons()
        {
            //Log.Message( "Bionic icons static constr." );//------------------
            HashSet<ThingDef> processed = new HashSet<ThingDef>();
            List<BionicIconsIconDef> directReplacements = new List<BionicIconsIconDef>();

            // populates replacements
            foreach (BionicIconsTextureDef def in DefDatabase<BionicIconsTextureDef>.AllDefs ) // iterates over all the BionicIconsTextureDef defined in xml files
            {
                //Log.Message( String.Format( "BionicIconsTextureDef: {0} - {1}", def.defName, def.texture ) );//---debug message
                List<BionicIconsTextureDef> list;
                if (!replacements.TryGetValue(def.texture, out list)) // checks if there is an entry for the texture to replace
                {
                    list = new List<BionicIconsTextureDef>();       // adds a new one if there isnt one present
                    replacements.Add(def.texture, list);
                }

                list.Add(def);   // associates the BionicIconsTextureDef to the testure to replace ( 1 texture to replace -> n BionicIconsTextureDef )
            }

            IEnumerable<BionicIconsIconDef> icons = DefDatabase<BionicIconsIconDef>.AllDefs; // extracts all BionicIconsIconDef defined in xml files
            icons = icons.OrderBy(x => x.nameContains == null ? 1 : 0).ThenBy(x => x.defName); // sorts them by whether they have a nameContains or not, then by defName 
            foreach (BionicIconsIconDef def in icons)
            {

                // debug log
                //Log.Message( String.Format( "BionicIconsIconDef: {0} - {1}", def.defName, def.thingDef ) );//---debug message


                foreach (string bodyPartName in def.BodyParts())
                {
                    BodyPartDef bodyPart = DefDatabase<BodyPartDef>.GetNamedSilentFail(bodyPartName); // skip if the body part doesnt exist
                    if (bodyPart == null) continue;

                    List<BionicIconsIconDef> list;
                    if (!replacementsIcon.TryGetValue(bodyPart, out list))
                    {
                        list = new List<BionicIconsIconDef>();
                        replacementsIcon.Add(bodyPart, list);
                    }

                    list.Add(def);  // 1 bodypart -> n BionicIconsIconDef
                }

                if (def.thingDef != null) // if it references a specific thingdef patch it in white
                {
                    ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(def.thingDef);
                    if (thingDef == null) continue;

                    if (processed.Contains(thingDef)) continue;
                    replace(thingDef, def.texture, Color.white, null);
                    processed.Add(thingDef);
                }
            }

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipe.appliedOnFixedBodyParts.NullOrEmpty()) continue;

                foreach (BodyPartDef bodyPart in recipe.appliedOnFixedBodyParts)
                {
                    foreach (IngredientCount ing in recipe.ingredients)
                    {
                        foreach (ThingDef def in ing.filter.AllowedThingDefs)
                        {
                            //Log.Message( String.Format( "Recipe: {0} - {1} - {2}", recipe.defName, def.defName, def.isTechHediff ) );//---debug message
                            if (processed.Contains(def)) continue;
                            if (processDef(recipe, bodyPart, def)) // called on all operations for each ingredient
                            {
                                processed.Add( def ); 
                            }
                        }
                    }
                }
            }
        }

        private static bool processDef(RecipeDef recipe, BodyPartDef bodyPart, ThingDef def)
        {
            if (!def.isTechHediff) return false; // closes on ingredients that are not the installed part
            if (def.graphicData == null) return false;

            List<BionicIconsTextureDef> colors;
            if (!replacements.TryGetValue(def.graphicData.texPath, out colors)) return false; // checks if the part has a texture that can be replaced

            List<BionicIconsIconDef> icons;
            if (!replacementsIcon.TryGetValue(bodyPart, out icons)) return false; // checks if the bodypart has an icon

            string tex = null;
            foreach (BionicIconsIconDef option in icons) // takes the first compatible icon texture
            {
                if (option.nameContains != null && !def.defName.Contains(option.nameContains)) continue;
                if (recipe.addsHediff != null)
                {
                    bool isSolid = recipe.addsHediff.addedPartProps != null && recipe.addsHediff.addedPartProps.solid;

                    if (option.SolidOnly && !isSolid) continue;
                    if (option.SoftOnly && isSolid) continue;
                }

                tex = option.texture;
                break;
            }
            if (tex == null) return false;

            Color color = Color.white;
            BionicIconsTextureDef textureDef = null;
            foreach (BionicIconsTextureDef option in colors)
            {
                if (option.nameContains != null && !def.defName.Contains(option.nameContains)) continue;
                if (option.category != null && def.thingCategories != null && !def.thingCategories.Contains(option.category)) continue;

                textureDef = option;
                color = option.color;
                break;
            }

            replace(def, tex, color, textureDef);
            return true;
        }

        private static void replace(ThingDef def, string tex, Color color, BionicIconsTextureDef textureDef)
        {
            
            if (textureDef != null)
            {
                def.graphicData.graphicClass = typeof(Graphic_SingleWithMask);
                def.graphicData.drawSize = new Vector2(1.0f, 1.0f);
                def.graphicData.color = textureDef.color;
                def.graphicData.texPath = textureDef.replacement;
                def.graphicData.shaderType = ShaderTypeDefOf.CutoutComplex;
                Graphic_SingleWithMask.maskPath = tex;
                def.graphicData.colorTwo = textureDef.colorIcon;
            }
            else
            {
                def.graphicData.graphicClass = typeof(Graphic_Single);
                def.graphicData.drawSize = new Vector2(1.0f, 1.0f);
                def.graphicData.color = color;
                def.graphicData.texPath = tex;
            }

            graphicDataInit.Invoke(def.graphicData, new object[] { });
            def.graphic = def.graphicData.Graphic;
            def.uiIcon = def.graphicData.Graphic.MatSingle.mainTexture as Texture2D;
            def.uiIconColor = color;
        }
    }
}
