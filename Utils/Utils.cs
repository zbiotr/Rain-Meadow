﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RWCustom;
using UnityEngine;

namespace RainMeadow
{
    internal static class Utils
    {
        public static InGameTranslator Translator => Custom.rainWorld.inGameTranslator;

        public static string Translate(string text)
        {
            return Translator.Translate(text);
        }

        public static string GetMeadowTitleFileName(bool isShadow)
        {
            var fileName = isShadow ? "shadow" : "title";

            var translatedfileName = fileName + "_" + Translator.currentLanguage.value.ToLower();

            // Fallback to English
            if (!File.Exists(AssetManager.ResolveFilePath($"illustrations/rainmeadowtitle/{translatedfileName}.png")))
            {
                return fileName + "_english";
            }

            return translatedfileName;
        }

        public static string GetTranslatedLobbyName(string username)
        {
            var lobbyName = Translator.Translate("<USERNAME>'s Lobby");

            return lobbyName.Replace("<USERNAME>", username);
        }


        public static void Restart(string args = "")
        {
            Process currentProcess = Process.GetCurrentProcess();
            string text = "\"" + currentProcess.MainModule.FileName + "\"";
            IDictionary environmentVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
            List<string> list = new List<string>();
            foreach (object obj in environmentVariables)
            {
                DictionaryEntry dictionaryEntry = (DictionaryEntry)obj;
                if (dictionaryEntry.Key.ToString().StartsWith("DOORSTOP"))
                {
                    list.Add(dictionaryEntry.Key.ToString());
                }
            }
            foreach (string text2 in list)
            {
                environmentVariables.Remove(text2);
            }
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.EnvironmentVariables.Clear();
            foreach (object obj2 in environmentVariables)
            {
                DictionaryEntry dictionaryEntry2 = (DictionaryEntry)obj2;
                processStartInfo.EnvironmentVariables.Add((string)dictionaryEntry2.Key, (string)dictionaryEntry2.Value);
            }
            processStartInfo.UseShellExecute = false;
            processStartInfo.FileName = text;
            processStartInfo.Arguments = args;
            Process.Start(processStartInfo);
            Application.Quit();
        }

        /// <summary>
        /// Adds a range of items to a list, excluding items which are already in the list.
        /// </summary>
        /// <param name="self">The list to add to.</param>
        /// <param name="items">The range of items to add.</param>
        public static void AddDistinctRange<T>(this IList<T> self, IEnumerable<T> items)
        {
            foreach(var item in items)
            {
                if (self.Contains(item))
                {
                    continue;
                }

                self.Add(item);
            }
        }
    }
}
