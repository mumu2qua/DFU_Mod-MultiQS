using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;

namespace MultiQS
{

    public class MultiQS : MonoBehaviour
    {
        static Mod mod;
        [Invoke(StateManager.StateTypes.Game, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<MultiQS>();
        }

        private class GameSave
        {
            public SaveInfo_v1 SaveInfo;
            public int Key;

            public GameSave(int key)
            {
                Key = key;
                SaveInfo = SaveLoadManager.Instance.GetSaveInfo(key);
            }
        }

        private KeyCode QSKeyBinding;
        private string RestoreKeyFile;
        private string TimeFormat;
        private string BackupName = "QuickSave.old";
        private int QuickSaveBackupMaxNum;
        private bool UnlimitedQS;

        void Awake()
        {
            // Loading settings
            var settings = mod.GetSettings();
            QuickSaveBackupMaxNum = settings.GetValue<int>("Main", "MaxOldQuicksavesNumber");
            UnlimitedQS = settings.GetValue<bool>("Main", "UnlimitedOldQuicksaves");

            // Setup time format
            bool UseTFHFormat = settings.GetValue<bool>("Main", "UseTwentyFourHourFormat");
            if (UseTFHFormat)
                TimeFormat="yyyyMMdd_HH:mm:ss";
            else
                TimeFormat="yyyyMMdd_hh:mm:ss tt";

            // Create Persistent Storage Folder if neccessary
            Directory.CreateDirectory(mod.PersistentDataDirectory);
            RestoreKeyFile = Path.Combine(mod.PersistentDataDirectory, "RestoreKeyFile.txt");

            // Hijack the quicksave key
            LoadQSKey();
            InputManager.OnSavedKeyBinds += OnQuickSaveKeyChanged;

            mod.IsReady = true;
        }

        void Update()
        {
            if (Input.GetKeyDown(QSKeyBinding))
            {
                if (SaveLoadManager.Instance.IsSavingPrevented)
                {
                    DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("cannotSaveNow"));
                    return;
                }
                if (SaveLoadManager.Instance.HasQuickSave(GameManager.Instance.PlayerEntity.Name))
                    BackupQuickSave();
                SaveLoadManager.Instance.QuickSave();
            }
        }

        void BackupQuickSave()
        {
            int ExistingQuickSave = SaveLoadManager.Instance.FindSaveFolderByNames(GameManager.Instance.PlayerEntity.Name, "QuickSave");

            // Get timestamp from the save
            SaveInfo_v1 ExistingQuickSaveInfo = SaveLoadManager.Instance.GetSaveInfo(ExistingQuickSave);
            string dateTimeString = DateTime.FromBinary(ExistingQuickSaveInfo.dateAndTime.realTime).ToString(TimeFormat);

            string SaveName = BackupName + dateTimeString;
            SaveLoadManager.Instance.Rename(ExistingQuickSave, BackupName + dateTimeString);
            if (!UnlimitedQS)
                RemoveOldQuickSaves();
        }

        void RemoveOldQuickSaves()
        {
            int[] PlayerCharSaves = SaveLoadManager.Instance.GetCharacterSaveKeys(GameManager.Instance.PlayerEntity.Name);
            List<GameSave> QSBacks = new List<GameSave>();
            foreach (int save in PlayerCharSaves)
            {
                GameSave GameSave = new GameSave(save);
                if (GameSave.SaveInfo.saveName.Contains(BackupName))
                {
                    QSBacks.Add(GameSave);
                }
            }

            if (QSBacks.Count() > QuickSaveBackupMaxNum)
            {
                QSBacks.Sort(delegate (GameSave gs1, GameSave gs2) {
                    return gs1.SaveInfo.dateAndTime.realTime.CompareTo(gs2.SaveInfo.dateAndTime.realTime);
                });
                while(QSBacks.Count() > QuickSaveBackupMaxNum)
                {
                    SaveLoadManager.Instance.DeleteSaveFolder(QSBacks.First().Key);
                    QSBacks.RemoveAt(0);
                }
            }
        }

        void LoadQSKey()
        {
            string LoadedKeyCode;
            if (File.Exists(RestoreKeyFile))
            {
                LoadedKeyCode = File.ReadLines(RestoreKeyFile).First();
                try {
                    QSKeyBinding = (KeyCode)Enum.Parse(typeof(KeyCode), LoadedKeyCode);
                }
                catch (ArgumentException)
                {
                    // Unlikely to ever happen, but I'm paranoid
                    Debug.LogWarning($"MultiQS: Invalid key string: {LoadedKeyCode}. Falling back to default.");
                    File.WriteAllText(RestoreKeyFile, "F9");
                    QSKeyBinding = (KeyCode)Enum.Parse(typeof(KeyCode), "F9");
                }
                // Game is hardcoded to restore hotkeys if assigned to None
                // so have to clear it every time.
                InputManager.Instance.ClearBinding(InputManager.Actions.QuickSave);
            }
            else
            {
                UpdateKey();
            }

            // Binding isn't actually cleared until this is called
            InputManager.Instance.SaveKeyBinds();
        }

        // Saves current QuickSave Keybinding to persistent storage and hijacks it
        void UpdateKey()
        {
            QSKeyBinding = InputManager.Instance.GetBinding(InputManager.Actions.QuickSave);
            File.WriteAllText(RestoreKeyFile, QSKeyBinding.ToString());
            InputManager.Instance.ClearBinding(InputManager.Actions.QuickSave);
        }

        // Whenever assigned new QuickSave key
        void OnQuickSaveKeyChanged()
        {
            KeyCode CurrentQSKey = InputManager.Instance.GetBinding(InputManager.Actions.QuickSave);
            if(CurrentQSKey == KeyCode.None)
                return;
            UpdateKey();
        }
    }
}

// vim: ts=4 sts=4 et
