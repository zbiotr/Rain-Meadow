﻿using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RainMeadow
{
    public static class RainMeadowModManager
    {
        private static void UpdateFromOrWriteToFile(string path, ref string[] lines)
        {
            path = Path.Combine(Custom.RootFolderDirectory(), path);
            if (File.Exists(path))
            {
                lines = File.ReadAllLines(path);
            }
            else
            {
                File.WriteAllLines(path, lines);
            }
        }

        public static string[] highImpactMods = {
            "rwremix",
            "moreslugcats",
        };

        public static string[] GetRequiredMods()
        {
            UpdateFromOrWriteToFile("meadow-highimpactmods.txt", ref highImpactMods);

            var requiredMods = highImpactMods.Union(RainMeadowModInfoManager.MergedModInfo.SyncRequiredMods.Except(RainMeadowModInfoManager.MergedModInfo.SyncRequiredModsOverride)).ToList();

            return ModManager.ActiveMods
                .Where(mod => requiredMods.Contains(mod.id)
                    || Directory.Exists(Path.Combine(mod.path, "modify", "world")))
                .Select(mod => mod.id)
                .ToArray();
        }

        public static string[] bannedMods = {
            "maxi-mol.mousedrag",
            "fyre.BeastMaster",
            "slime-cubed.devconsole",
            "zrydnoob.UnityExplorer",
            "warp",
            "presstopup",
            "CandleSign.debugvisualizer",
            "maxi-mol.freecam",
            "henpemaz_spawnmenu",  //gotta be safe
            "autodestruct",
            "DieButton",
            "emeralds_features",
            "flirpy.rivuletunscammedlungcapacity",
            "Aureuix.Kaboom",
            "TM.PupMagnet",
            "iwantbread.slugpupstuff",
            "blujai.rocketficer",
            "slugcatstatsconfig",
            "explorite.slugpups_cap_configuration",
            "slime-cubed.slugbase",
        };

        public static string[] GetBannedMods()
        {
            UpdateFromOrWriteToFile("meadow-highimpactmods.txt", ref highImpactMods);
            UpdateFromOrWriteToFile("meadow-bannedmods.txt", ref bannedMods);

            var effectiveHighImpactMods = highImpactMods.Union(RainMeadowModInfoManager.MergedModInfo.SyncRequiredMods.Except(RainMeadowModInfoManager.MergedModInfo.SyncRequiredModsOverride)).ToList();
            var effectiveBannedMods = bannedMods.Union(RainMeadowModInfoManager.MergedModInfo.BannedOnlineMods.Except(RainMeadowModInfoManager.MergedModInfo.BannedOnlineModsOverride)).ToList();

            // (high impact + banned) - enabled
            return effectiveHighImpactMods.Concat(effectiveBannedMods)
                .Except(ModManager.ActiveMods.Select(mod => mod.id))
                .ToArray();
        }

        internal static void CheckMods(string[] requiredMods, string[] bannedMods)
        {
            RainMeadow.Debug($"required: [ {string.Join(", ", requiredMods)} ]");
            RainMeadow.Debug($"banned:   [ {string.Join(", ", bannedMods)} ]");
            var active = ModManager.ActiveMods.Select(mod => mod.id);
            bool reorder = true; //or change mods whatsoever
            var disable = GetRequiredMods().Union(bannedMods).Except(requiredMods).Intersect(active);
            var enable = requiredMods.Except(active);

            //determine whether a reorder is necessary
            if (!disable.Any() && !enable.Any())
            {
                reorder = false;
                int prevIdx = -1;
                foreach (var reqID in requiredMods)
                {
                    int newIdx = ModManager.ActiveMods.Find(mod => reqID == mod.id).loadOrder;
                    if (newIdx <= prevIdx)
                    {
                        reorder = true;
                        break;
                    }
                    prevIdx = newIdx;
                }
            }

            RainMeadow.Debug($"active:  [ {string.Join(", ", active)} ]");
            RainMeadow.Debug($"enable:  [ {string.Join(", ", enable)} ]");
            RainMeadow.Debug($"disable: [ {string.Join(", ", disable)} ]");
            RainMeadow.Debug($"reorder: {reorder}");

            if (!reorder) return;

            var lobbyID = MatchmakingManager.currentInstance.GetLobbyID();
            /*if (enable.Any() || disable.Any()) //moved to individual implementations in ModApplier.cs, since ConfirmReorder doesn't kick players
            {
                RWCustom.Custom.rainWorld.processManager.RequestMainProcessSwitch(RainMeadow.Ext_ProcessID.LobbySelectMenu);
                OnlineManager.LeaveLobby();
            }*/

            List<bool> pendingEnabled = ModManager.InstalledMods.ConvertAll(mod => mod.enabled);
            List<int> pendingLoadOrder = ModManager.InstalledMods.ConvertAll(mod => mod.loadOrder);

            List<string> missingMods = new();
            List<ModManager.Mod> modsToEnable = new(), modsToDisable = new();

            foreach (var id in enable)
            {
                int index = ModManager.InstalledMods.FindIndex(mod => mod.id == id);
                if (index == -1) missingMods.Add(id);
                else
                {
                    pendingEnabled[index] = true;

                    modsToEnable.Add(ModManager.InstalledMods[index]);
                }
            }

            //enable missing dependencies
            foreach (var id in requiredMods)
            {
                int index = ModManager.InstalledMods.FindIndex(mod => mod.id == id);
                if (index < 0) continue;
                foreach (var depID in ModManager.InstalledMods[index].requirements)
                {
                    int depIdx = ModManager.InstalledMods.FindIndex(mod => mod.id == depID);
                    if (depIdx < 0)
                        missingMods.Add(depID);
                    else if (!pendingEnabled[depIdx])
                    {
                        pendingEnabled[depIdx] = true;
                        modsToEnable.Add(ModManager.InstalledMods[depIdx]);
                    }
                }
            }

            foreach (var id in disable)
            {
                int index = ModManager.InstalledMods.FindIndex(_mod => _mod.id == id);
                pendingEnabled[index] = false;
                modsToDisable.Add(ModManager.InstalledMods[index]);
            }

            //reorder mods
            //try using negative indices, just to simplify things? Will that even work??
            if (missingMods.Count < 1)
            {
                for (int i = 0; i < requiredMods.Length; i++)
                    pendingLoadOrder[ModManager.InstalledMods.FindIndex(_mod => _mod.id == requiredMods[i])] = i - requiredMods.Length;

                string loadOrderString = "Load Order: "; //log the load order
                for (int i = 0; i < pendingLoadOrder.Count; i++)
                    loadOrderString += pendingLoadOrder[i] + "-" + ModManager.InstalledMods[i].id + ", ";
                RainMeadow.Debug(loadOrderString);

                //attempt to ensure all dependencies are ordered properly
                int lowIdx = -1 - requiredMods.Length;
                bool allGoodPass = false;
                for (int attempts = 1; !allGoodPass && attempts <= 50; attempts++)
                {
                    allGoodPass = true;
                    //loop through each installed mod that will be enabled
                    for (int i = 0; allGoodPass && i < pendingEnabled.Count; i++)
                    {
                        if (!pendingEnabled[i]) continue;
                        foreach (var reqID in ModManager.InstalledMods[i].requirements)
                        {
                            int reqIndex = ModManager.InstalledMods.FindIndex(mod => mod.id == reqID);
                            var req = ModManager.InstalledMods[reqIndex];
                            if (req == default(ModManager.Mod) && !missingMods.Contains(reqID))
                                missingMods.Add(reqID);
                            else
                            {
                                pendingEnabled[reqIndex] = true;
                                if (req.loadOrder >= ModManager.InstalledMods[i].loadOrder)
                                {
                                    req.loadOrder = lowIdx--;
                                    allGoodPass = false;
                                }
                            }
                        }
                    }
                }

                if (missingMods.Count < 1)
                {
                    //fix load order!
                    List<ModManager.Mod> modsToLoad = new();
                    for (int i = 0; i < pendingEnabled.Count; i++)
                    {
                        if (pendingEnabled[i]) modsToLoad.Add(ModManager.InstalledMods[i]);
                    }
                    List<ModManager.Mod> reorderedMods = new();
                    foreach(var mod in modsToLoad)
                    {
                        int index = reorderedMods.FindIndex(m => mod.loadOrder > m.loadOrder); //first mod with higher order
                        if (index < 0) //no mods with higher load order, so add to the end
                            reorderedMods.Add(mod);
                        else //found a mod with higher load order, so insert just in front
                            reorderedMods.Insert(index, mod);
                    }
                    
                    //finally reorder and print debug list
                    loadOrderString = "Final load order: "; //for debugging
                    for (int i = 0; i < reorderedMods.Count; i++) {
                        reorderedMods[i].loadOrder = i;
                        loadOrderString += i + "-" + reorderedMods[i].id + ", ";
                    }
                    RainMeadow.Debug(loadOrderString);
                    modsToLoad.Clear();
                    reorderedMods.Clear();
                }
            }

            ModApplier modApplier = new(RWCustom.Custom.rainWorld.processManager, pendingEnabled, pendingLoadOrder);

            //check for missing DLC
            List<ModManager.Mod> missingDLC = modsToEnable.Where(mod => mod.DLCMissing).ToList();

            if (missingDLC.Count > 0)
                modApplier.ShowMissingDLCMessage(missingDLC);
            else if (enable.Any() || disable.Any() || missingMods.Count > 0)
                modApplier.ShowConfirmation(modsToEnable, modsToDisable, missingMods);
            else
                modApplier.ConfirmReorder();

            modApplier.OnFinish += (ModApplier modApplyer) =>
            {
                RainMeadow.Debug("Finished applying");

                if (modApplier.requiresRestart)
                {
                    Utils.Restart($"+connect_lobby {lobbyID}");
                }
                //else
                //REJOIN LOBBY... but... how...?
            };
        }

        internal static void Reset()
        {
            if (ModManager.MMF)
            {
                RainMeadow.Debug("Restoring config settings");

                var mmfOptions = MachineConnector.GetRegisteredOI(MoreSlugcats.MMF.MOD_ID);
                MachineConnector.ReloadConfig(mmfOptions);
            }
        }
    }
}
