﻿using System.Linq;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;
using QudUX.Concepts;
using static QudUX.Concepts.Constants.MethodsAndFields;
using static QudUX.HarmonyPatches.PatchHelpers;
using NAudio.MediaFoundation;

namespace QudUX.HarmonyPatches
{
    /// <summary>
    /// 
    /// Egocarib's version was only patching the Cook method but the current 
    /// state of that method suggests that a lot of things were reworked.
    /// I kept his work for prosperity, and patched the new methods that I
    /// believe to be relevant:
    /// Campfire.CookFromIngredients
    /// Campfire.CookFromRecipe
    /// 
    /// </summary>
    [HarmonyPatch(typeof(XRL.World.Parts.Campfire))]
    public class Patch_XRL_World_Parts_Campfire
    {

        [HarmonyPrepare]
        static bool Prepare()
        {
            if (!Options.UI.UseQudUXCookMenus)
            {
                PatchHelpers.LogPatchResult("Campfire",
                    "Skipped. The \"Use revamped cooking menus\" option is disabled.");
                return false;
            }
            return true;
        }

        #region OLD_PATCH
        /// <summary>
    /// The following patch replaces the "cook with ingredients" and "choose recipe" selection screens
    /// with entirely new UI screens. This is a fairly complex transpiler that modifies a number of
    /// details in the Campfire class. Unfortunately the code that handles these menus is overly
    /// complex, as it uses a lot of looping pop-ups instead of real menus.
    /// </summary>

        // This is where we find the first array parameter that we are looking for (ingredient GameObject array)
        private readonly static CodeInstruction FirstSegmentTargetInstruction = new CodeInstruction(OpCodes.Call, Campfire_GetValidCookingIngredients);

        // This is where we find the second array parameter that we are looking for (corresponding "selected ingredient" boolean array)
        private readonly static List<CodeInstruction> SecondSegmentTargetInstructions = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Ldstr, "CookingAndGathering_Spicer"),
            new CodeInstruction(OpCodes.Callvirt, GameObject_HasSkill),
            new CodeInstruction(OpCodes.Newobj, List_Bool_ctor)
        };

        // This brings us to just before parameters are pushed onto the stack for the "choose ingredients" method we want to replace
        private readonly static List<CodeInstruction> ThirdSegmentTargetInstructions = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Endfinally),
            new CodeInstruction(OpCodes.Ldstr, "Choose ingredients to cook with.")
        };

        // This is the "choose ingredients" method we want to replace
        private readonly static CodeInstruction ThirdSegmentFinalInstruction = new CodeInstruction(OpCodes.Call, Popup_ShowOptionList);

        // Here is where we modify a flag to force recipes with missing ingredients to be included in the array
        private readonly static CodeInstruction FourthSegmentTarget1_Instruction = new CodeInstruction(OpCodes.Ldc_I4_2);
        private readonly static OpCode FourthSegmentTarget2_OpCodeOnly = OpCodes.Bne_Un;
        private readonly static CodeInstruction FourthSegmentTarget3_Instruction = new CodeInstruction(OpCodes.Ldc_I4_0);
        private readonly static int FourthSegmentAllowedInstructionDistance = 4;

        // This brings us to just before parameters are pushed onto the stack for the "choose recipe" method we want to replace
        private readonly static CodeInstruction FifthSegmentTargetInstruction = new CodeInstruction(OpCodes.Ldstr, "Choose a recipe");
        private readonly static CodeInstruction FifthSegmentFinalInstruction = new CodeInstruction(OpCodes.Call, Popup_ShowOptionList);

        // Here is where we null out (Nop) some instructions that we no longer need because our new menu replaces these functionalities
        private readonly static CodeInstruction SixthSegmentTargetInstruction = new CodeInstruction(OpCodes.Ldstr, "Add to favorite recipes");
        private readonly static CodeInstruction SixthSegmentFinalInstruction = new CodeInstruction(OpCodes.Newobj, List_GameObject_ctor);

        //max allowed distance between individual instructions in the above sequences
        private static int AllowedInstructionDistance = 20;

        // [HarmonyTranspiler]
        // [HarmonyPatch("Cook")]
        static IEnumerable<CodeInstruction> OldCampfirePatch(IEnumerable<CodeInstruction> instructions)
        {
            int patchSegment = 1;
            int patchSegment1_stloc_ct = 0;
            object ingredientList_LocalVarIndex = null;
            object ingredientBools_LocalVarIndex = null;
            object recipeList_LocalVarIndex = null;
            int idx = 0;
            int gap = 0;
            bool found = false;
            bool patchComplete = false;
            foreach (var instruction in instructions)
            {
                if (found)
                {
                    if (patchSegment == 1)
                    {
                        if (instruction.opcode == OpCodes.Stloc_S)
                        {
                            patchSegment1_stloc_ct++;
                        }
                        if (patchSegment1_stloc_ct == 2)
                        {
                            //save the local variable index of the ingredient list
                            ingredientList_LocalVarIndex = instruction.operand;
                            patchSegment++;
                            found = false;
                        }
                    }
                    else if (patchSegment == 2)
                    {
                        if (instruction.opcode == OpCodes.Stloc_S)
                        {
                            ingredientBools_LocalVarIndex = instruction.operand;
                            patchSegment++;
                            found = false;
                        }
                    }
                    else if (patchSegment == 3)
                    {
                        //ignore all the lines that push stuff onto the stack for Popup.ShowOptionList
                        if (!PatchHelpers.InstructionsAreEqual(instruction, ThirdSegmentFinalInstruction))
                        {
                            continue;
                        }
                        //replace the call to Popup.ShowOptionList with our custom ingredient selection menu
                        yield return new CodeInstruction(OpCodes.Ldloc_S, ingredientList_LocalVarIndex);
                        yield return new CodeInstruction(OpCodes.Ldloc_S, ingredientBools_LocalVarIndex);
                        yield return new CodeInstruction(OpCodes.Call, QudUX_IngredientSelectionScreen_Static_Show);
                        patchSegment++;
                        found = false;
                        continue;
                    }
                    else if (patchSegment == 4)
                    {
                        //unused
                    }
                    else if (patchSegment == 5)
                    {
                        if (recipeList_LocalVarIndex == null && instruction.opcode == OpCodes.Ldloc_S)
                        {
                            //grab the recipe list variable, we'll need it below
                            recipeList_LocalVarIndex = instruction.operand;
                        }
                        else if (PatchHelpers.InstructionsAreEqual(instruction, FifthSegmentFinalInstruction))
                        {
                            //replace the call to Popup.ShowOptionList with our custom recipe selection menu
                            yield return new CodeInstruction(OpCodes.Ldloc_S, recipeList_LocalVarIndex);
                            yield return new CodeInstruction(OpCodes.Call, QudUX_RecipeSelectionScreen_Static_Show);
                            patchSegment++;
                            found = false;
                        }
                        continue;
                    }
                    else if (!patchComplete)
                    {
                        if (PatchHelpers.InstructionsAreEqual(instruction, SixthSegmentFinalInstruction))
                        {
                            patchComplete = true;
                            PatchHelpers.LogPatchResult("Campfire",
                                "Patched successfully." /* Adds completely new UI screens for ingredient- and recipe-based cooking. */ );
                            //allow this instruction to fall through, we want it and everything after it.
                        }
                        else
                        {
                            //null out various instructions (several of them are used as labels, so can't just ignore them)
                            instruction.opcode = OpCodes.Nop;
                            instruction.operand = null;
                        }
                    }
                }
                else if (patchSegment == 1)
                {
                    if (PatchHelpers.InstructionsAreEqual(instruction, FirstSegmentTargetInstruction))
                    {
                        found = true;
                        idx = 0;
                    }
                }
                else if (patchSegment == 2)
                {
                    if (PatchHelpers.InstructionsAreEqual(instruction, SecondSegmentTargetInstructions[idx]))
                    {
                        if (++idx == SecondSegmentTargetInstructions.Count())
                        {
                            found = true;
                            idx = 0;
                        }
                        gap = 0;
                    }
                    else
                    {
                        if (++gap > AllowedInstructionDistance)
                        {
                            idx = 0;
                        }
                    }
                }
                else if (patchSegment == 3)
                {
                    if (PatchHelpers.InstructionsAreEqual(instruction, ThirdSegmentTargetInstructions[idx]))
                    {
                        if (++idx == ThirdSegmentTargetInstructions.Count())
                        {
                            found = true;
                            instruction.opcode = OpCodes.Nop; //null out this instruction (can't remove it because it's used as a label)
                            instruction.operand = null;
                            idx = 0;
                        }
                        gap = 0;
                    }
                    else
                    {
                        if (++gap > AllowedInstructionDistance)
                        {
                            idx = 0;
                        }
                    }
                }
                else if (patchSegment == 4)
                {
                    if (idx == 0)
                    {
                        if (PatchHelpers.InstructionsAreEqual(instruction, FourthSegmentTarget1_Instruction))
                        {
                            idx++;
                        }
                    }
                    else if (idx == 1)
                    {
                        if (instruction.opcode == FourthSegmentTarget2_OpCodeOnly)
                        {
                            idx++;
                        }
                        else
                        {
                            idx = 0;
                        }
                    }
                    else if (idx == 2)
                    {
                        if (!PatchHelpers.InstructionsAreEqual(instruction, FourthSegmentTarget3_Instruction))
                        {
                            if (++gap > FourthSegmentAllowedInstructionDistance)
                            {
                                idx = 0;
                                gap = 0;
                            }
                        }
                        else
                        {
                            instruction.opcode = OpCodes.Ldc_I4_1; //modify to set flag to true instead of false, so that recipes without ingredients on hand aren't hidden from the array
                            patchSegment++;
                            idx = 0;
                            gap = 0;
                        }
                    }
                }
                else if (patchSegment == 5)
                {
                    if (PatchHelpers.InstructionsAreEqual(instruction, FifthSegmentTargetInstruction))
                    {
                        found = true;
                        instruction.opcode = OpCodes.Nop; //null out this instruction (can't remove it because it's used as a label)
                        instruction.operand = null;
                    }
                }
                else if (patchSegment == 6)
                {
                    if (PatchHelpers.InstructionsAreEqual(instruction, SixthSegmentTargetInstruction))
                    {
                        found = true;
                        instruction.opcode = OpCodes.Nop;
                        instruction.operand = null;
                    }
                }
                yield return instruction;
            }
            if (patchComplete == false)
            {
                PatchHelpers.LogPatchResult("Campfire",
                    "Failed. This patch may not be compatible with the current game version. "
                    + "The game's default cooking UI pop-ups will be used instead of QudUX's revamped screens.");
            }
        }
#endregion
        
        private static bool _CookWithIngredientsPatched = false;
        private static bool _CookWithRecipePatched = false;

        [HarmonyTranspiler]
        [HarmonyPatch("CookFromIngredients")]
        [HarmonyDebug]
        static IEnumerable<CodeInstruction> Transpiler_CookFromIngredient(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
        {
            //First we need to retrieve Tuple List that compiles all ingredients data
            //Then we need to retrieve the list of bool representing selected ingredients
            //Then pass it as parameters for our IngredientSelectionScreen static show
            //discard all insutructions drawing source menu until bool list evaluation;

            // Ingredients are stored in a List<Tuple<int, GameObject, string>>, saved on index 2
            // It is accessible through OPCode Ldloc_2, not special operand are needed so we 
            // can directly look for the list of bool
            var Seq1 = new PatchTargetInstructionSet( new List<PatchTargetInstruction>(){
                new PatchTargetInstruction(OpCodes.Ldloca_S),
                new PatchTargetInstruction(OpCodes.Call, 0),
                new PatchTargetInstruction(OpCodes.Pop, 0),
                new PatchTargetInstruction(OpCodes.Ldloc_S, 0), // push List<bool> on stack
                new PatchTargetInstruction(OpCodes.Ldc_I4_0, 0),
                new PatchTargetInstruction(OpCodes.Callvirt, 0),
                new PatchTargetInstruction(OpCodes.Ldloca_S, 0),
                new PatchTargetInstruction(OpCodes.Call, 0),
                new PatchTargetInstruction(OpCodes.Brtrue_S, 0),
                new PatchTargetInstruction(OpCodes.Leave_S, 0),
            });

            var Seq2 = new PatchTargetInstructionSet( new List<PatchTargetInstruction>()
            {
                new PatchTargetInstruction(OpCodes.Ldloc_S),
                new PatchTargetInstruction(OpCodes.Ldc_I4_1, 0),
                new PatchTargetInstruction(OpCodes.Sub, 0),
                new PatchTargetInstruction(OpCodes.Stloc_S, 0),
                new PatchTargetInstruction(OpCodes.Br, 0),
                new PatchTargetInstruction(OpCodes.Ldloc_S, 0),
                new PatchTargetInstruction(OpCodes.Brtrue_S, 0),
                new PatchTargetInstruction(OpCodes.Ldloc_S, 0),
                new PatchTargetInstruction(OpCodes.Ldloc_S, 0),
                new PatchTargetInstruction(OpCodes.Bgt, 0),
                // We stop at the instruction below because it also has a Label
                // pointing right where we want to be. i.e. at the beginning of
                // the loop evaluating bools
                new PatchTargetInstruction(OpCodes.Ldc_I4_0, 0) 
            });

            int seq=1;
            CodeInstruction seq2Last = null;
            Label jumpOverSourceDisplay = gen.DefineLabel();

            foreach(CodeInstruction instruction in instructions)
            {
                // When completed, we do nothing.
                // The point of this sequence is to:
                // • Get the instruction that pushes the list<bool> of selected ingredients
                // • Get the instruction right before the scope display source menu to setup a jump over it
                //
                // Seq2 is there to detect when the source display loop ends
                // then we can add the jump label to our instructions to complete the override
                if(seq == 1 && Seq1.IsMatchComplete(instruction))
                {
                    // instructoin below is leave.s, originally jump right into source loop
                    // We override its operand by our own label, so that it jumps were we want
                    // instead of stepping into source loop
                    instruction.operand = jumpOverSourceDisplay;
                    yield return instruction;
                    //yield return new CodeInstruction(OpCodes.Br, jumpOverSourceDisplay); // Add uncondionnal jump
                    seq++;
                    continue;
                }
                else if(seq == 2 && Seq2.IsMatchComplete(instruction)) // going in if Seq2 is not completed
                {
                    // We don't do anything when Seq2 is completed
                    // We just want the iterator to point at the right place
                    seq++;
                    // still keeping a ref to stop instruction Because we patch right before
                    seq2Last = instruction;
                    continue;
                }
                else if(seq > 2 && !_CookWithIngredientsPatched)
                {   
                    // When sequence 2 is completed, we insert the call of our function, before
                    // putting back the stop instruction of Seq2, which is the beginning
                    // of the bool loop;
                    CodeInstruction loadIngredientList = new CodeInstruction(OpCodes.Ldloc_2);
                    loadIngredientList.labels.Add(jumpOverSourceDisplay);
                    yield return loadIngredientList; // Push ingredient List on stack
                    yield return Seq1.MatchedInstructions[3].Clone(); // Push
                    yield return new CodeInstruction(OpCodes.Call, QudUX_IngredientSelectionScreen_Static_Show);
                    yield return seq2Last;
                    _CookWithIngredientsPatched = true;
                }

                yield return instruction;
            }

            LogResult();
        }

        private static void LogResult()
        {
            string msg = "";
            if(!_CookWithIngredientsPatched || !_CookWithRecipePatched)
            {
                string target = "both";
                if(_CookWithIngredientsPatched || _CookWithRecipePatched)
                {
                    target = _CookWithIngredientsPatched ? "CookWithRecipe" : "CookWithIngredients";
                }
                msg = $"Failed ({target}). This patch may not be compatible with the current game version. "
                    + "The game's default cooking UI pop-ups will be used instead of QudUX's revamped screens.";
            }
            else msg =  "Patched successfully.";

            PatchHelpers.LogPatchResult("Campfire", msg);
        }

        [HarmonyFinalizer]
        [HarmonyPatch("CookFromIngredients")]
        static void Finalizer(System.Exception __exception)
         {
            if(__exception != null)
            {
                Utilities.Logger.Log("[CookFromIngredients Patch] " + __exception.Message);
            }
        }

    }
}
