//using System.Collections.Generic;
//using System.IO;
//using UnityEngine;

//#region Save Data Structures

//[System.Serializable]
//public class SerializableKeyValuePair
//{
//    public string key;
//    public float value;
//}

//[System.Serializable]
//public class NodeSaveData
//{
//    public string nodeId;
//    public bool hasBeenTriggered;
//    public List<SerializableKeyValuePair> variables;

//    public NodeSaveData()
//    {
//        variables = new List<SerializableKeyValuePair>();
//    }
//}

//[System.Serializable]
//public class MasterSaveData
//{
//    public List<NodeSaveData> storyNodes;
//}

//#endregion

///// <summary>
///// MasterSaveManager collects save data from all StoryNodes in the scene and writes it to one JSON file.
///// It also provides a context menu command to clear the master save file.
///// </summary>
//public class MasterSaveManager : MonoBehaviour
//{
//    public static MasterSaveManager Instance { get; private set; }

//    // Filename for the master save file.
//    private const string fileName = "MasterSaveData.json";
//    private string filePath;

//    private void Awake()
//    {
//        // Setup singleton.
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        Instance = this;
//        DontDestroyOnLoad(gameObject);

//        filePath = Path.Combine(Application.persistentDataPath, fileName);
//    }

//    private void Start()
//    {
//        // On start, collect and save all StoryNode data.
//        SaveAllStoryNodeData();
//    }

//    /// <summary>
//    /// Finds all StoryNodes in the scene, collects their save data, and writes it to one master JSON file.
//    /// </summary>
//    public void SaveAllStoryNodeData()
//    {
//        // Find all StoryNodes.
//        StoryNode[] nodes = FindObjectsOfType<StoryNode>();
//        MasterSaveData masterSave = new MasterSaveData();
//        masterSave.storyNodes = new List<NodeSaveData>();

//        foreach (StoryNode node in nodes)
//        {
//            // Each StoryNode must implement GetNodeSaveData() to return its state.
//            NodeSaveData data = node.GetNodeSaveData();
//            masterSave.storyNodes.Add(data);
//        }

//        string json = JsonUtility.ToJson(masterSave, true);
//        File.WriteAllText(filePath, json);
//        Debug.Log($"[MasterSaveManager] Saved all story node data to: {filePath}");
//    }

//    /// <summary>
//    /// Loads master save data from the JSON file.
//    /// </summary>
//    public MasterSaveData LoadAllStoryNodeData()
//    {
//        if (File.Exists(filePath))
//        {
//            string json = File.ReadAllText(filePath);
//            MasterSaveData masterSave = JsonUtility.FromJson<MasterSaveData>(json);
//            Debug.Log($"[MasterSaveManager] Loaded master save data from: {filePath}");
//            return masterSave;
//        }
//        else
//        {
//            Debug.Log($"[MasterSaveManager] No save file found at: {filePath}");
//            return new MasterSaveData() { storyNodes = new List<NodeSaveData>() };
//        }
//    }

//    /// <summary>
//    /// Clears the master save file.
//    /// WARNING: This deletes all saved data.
//    /// </summary>
//    [ContextMenu("Clear All Master Save Data")]
//    public void ClearAllMasterSaveData()
//    {
//        if (File.Exists(filePath))
//        {
//            File.Delete(filePath);
//            Debug.Log("[MasterSaveManager] Cleared master save data file.");
//        }
//    }
//}
