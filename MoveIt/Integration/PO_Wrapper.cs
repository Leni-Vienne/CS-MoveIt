﻿using ColossalFramework.Plugins;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// High level PO wrapper, always available

namespace MoveIt
{
    internal class PO_Manager
    {
        private IPO_Logic Logic;
        private static GameObject gameObject;

        private HashSet<uint> visibleIds = new HashSet<uint>();
        internal Dictionary<uint, IPO_Object> visibleObjects = new Dictionary<uint, IPO_Object>();

        internal List<IPO_Object> Objects => new List<IPO_Object>(visibleObjects.Values);
        internal IPO_Object GetProcObj(uint id) => visibleObjects[id];

        internal static readonly string[] VersionNames = { "1.6" };

        internal bool Enabled = false;
        private bool _active = false;
        public bool Active
        {
            get
            {
                if (!Enabled)
                    return false;
                return _active;
            }
            set
            {
                Debug.Log($"AAA PO UPDATE:{_active} -> {value}");
                if (!Enabled)
                    _active = false;
                _active = value;
            }
        }

        internal PO_Manager()
        {
            try
            {
                InitialiseLogic();
            }
            catch (TypeLoadException)
            {
                Enabled = false;
                Logic = new PO_LogicDisabled();
            }
        }

        private void InitialiseLogic()
        {
            if (isModEnabled())
            {
                Enabled = true;
                //Logic = new PO_LogicEnabled();
                gameObject = new GameObject("MIT_POLogic");
                gameObject.AddComponent<PO_LogicEnabled>();
                Logic = gameObject.GetComponent<PO_LogicEnabled>();
            }
            else
            {
                Enabled = false;
                Logic = new PO_LogicDisabled();
            }
        }

        internal void Clone(uint originalId, Vector3 position, float angle, Action action)
        {
            Logic.Clone(originalId, position, angle, action);
        }

        internal void StartConvertAction()
        {
            if (InitialiseTool(true) != null)
            {
                ConvertToPOAction convertAction = new ConvertToPOAction();
                ActionQueue.instance.Push(convertAction);
                ActionQueue.instance.Do();
            }
        }

        internal void InitialiseTool()
        {
            InitialiseTool(!MoveItTool.PO.Active);
        }

        internal bool? InitialiseTool(bool enable)
        {
            bool altered = false;

            if (MoveItTool.PO.Active == Enabled)
            {
                return false;
            }

            try
            {
                MoveItTool.PO.Active = enable;
                if (MoveItTool.PO.Active)
                {
                    if (MoveItTool.instance.ToolState == MoveItTool.ToolStates.Cloning)
                    {
                        MoveItTool.instance.StopCloning();
                    }

                    altered = MoveItTool.PO.ToolEnabled();
                    UIToolOptionPanel.instance.PO_button.activeStateIndex = 1;
                    ActionQueue.instance.Push(new TransformAction());
                }
                else
                {
                    UIToolOptionPanel.instance.PO_button.activeStateIndex = 0;
                    Action.ClearPOFromSelection();
                }
                UIFilters.POToggled();
            }
            catch (ArgumentException e)
            {
                Debug.Log($"PO Integration failed:\n{e}");
                if (MoveItTool.PO.Active)
                {
                    MoveItTool.PO.Active = false;
                    UIToolOptionPanel.instance.PO_button.activeStateIndex = 0;
                }
                return null;
            }
            return altered;
        }

        /// <returns>Bool - whether any PO changed since MIT was disabled</returns>
        internal bool ToolEnabled()
        {
            Dictionary<uint, IPO_Object> newVisible = new Dictionary<uint, IPO_Object>();
            HashSet<uint> newIds = new HashSet<uint>();

            foreach (IPO_Object obj in Logic.Objects)
            {
                newVisible.Add(obj.Id, obj);
                newIds.Add(obj.Id);
            }

            HashSet<uint> removed = new HashSet<uint>(visibleIds);
            removed.ExceptWith(newIds);
            HashSet<uint> added = new HashSet<uint>(newIds);
            added.ExceptWith(visibleIds);

            List<Instance> toRemove = new List<Instance>();
            foreach (Instance instance in Action.selection)
            {
                uint id = instance.id.NetLane;
                if (id > 0)
                {
                    if (removed.Contains(id))
                    {
                        toRemove.Add(instance);
                    }
                }
            }
            foreach (Instance instance in toRemove)
            {
                Action.selection.Remove(instance);
            }
            MoveItTool.m_debugPanel.UpdatePanel();

            //Debug.Log($"Visible from:{visibleObjects.Count} to:{newVisible.Count}\nSelected from:{selectedIds.Count} to:{newSelectedIds.Count}");

            visibleObjects = newVisible;
            visibleIds = newIds;

            if (added.Count > 0 || removed.Count > 0)
                return true;

            return false;
        }

        internal void Delete(IPO_Object obj)
        {
            Logic.Delete(obj);
        }

        internal IPO_Object ConvertToPO(Instance instance)
        {
            return Logic.ConvertToPO(instance);
        }

        internal static bool isModEnabled()
        {
            if (!isModInstalled())
            {
                return false;
            }

            return true;
        }

        internal static bool isModInstalled()
        {
            //string msg = "";
            //foreach (PluginManager.PluginInfo pi in PluginManager.instance.GetPluginsInfo())
            //{
            //    msg += $"\n{pi.publishedFileID.AsUInt64} - {pi.name} ({pi.isEnabled})" +
            //        $"\n - {pi.modPath}";
            //}
            //Debug.Log(msg);

            if (!PluginManager.instance.GetPluginsInfo().Any(mod => (
                    mod.publishedFileID.AsUInt64 == 1094334744uL || 
                    mod.name.Contains("ProceduralObjects") || 
                    mod.name.Contains("Procedural Objects") ||
                    mod.name.Contains("1094334744")
            ) && mod.isEnabled))
            {
                return false;
            }

            return true;
        }

        internal static string getVersionText()
        {
            if (isModInstalled())
            {
                if (VersionNames.Contains(PO_LogicEnabled.getVersion().Substring(0, 3)))
                //if (PO_LogicEnabled.getVersion() == VersionName)
                {
                    return $"PO version {PO_LogicEnabled.getVersion().Substring(0, 3)} found, integration enabled!\n ";
                }
                else
                {
                    return $"PO integration failed - found version {PO_LogicEnabled.getVersion().Substring(0, 3)} (required: 1.6)\n ";
                }
            }

            return "PO is not available. To use these options please quit Cities Skylines and subscribe to PO.\n ";
        }

        internal static string getVersion()
        {
            try
            {
                return _getVersion();
            }
            catch (TypeLoadException)
            {
                return "";
            }
        }

        private static string _getVersion()
        {
            return PO_LogicEnabled.getVersion();
        }
    }


    // PO Logic
    internal interface IPO_Logic
    {
        List<IPO_Object> Objects { get; }
        void Clone(uint originalId, Vector3 position, float angle, Action action);
        IPO_Object ConvertToPO(Instance instance);
        void Delete(IPO_Object obj);
    }

    internal class PO_LogicDisabled : IPO_Logic
    {
        public List<IPO_Object> Objects
        {
            get
            {
                return new List<IPO_Object>();
            }
        }

        public void Clone(uint originalId, Vector3 position, float angle, Action action)
        {
            throw new NotImplementedException($"Trying to clone {originalId} despite no PO!");
        }

        public IPO_Object ConvertToPO(Instance instance)
        {
            throw new NotImplementedException($"Trying to convert {instance} despite no PO!");
        }

        public void Delete(IPO_Object obj)
        {
            throw new NotImplementedException($"Trying to delete {obj} despite no PO!");
        }
    }


    // PO Object
    internal interface IPO_Object
    {
        bool Selected { get; set; }
        /// <summary>
        /// The InstanceID.NetLane value
        /// </summary>
        uint Id { get; set; }
        int ProcId { get; set; }
        Vector3 Position { get; set; }
        float Angle { get; set; }
        IInfo Info { get; set; }
        object GetProceduralObject();
        void SetPositionY(float h);
        float GetDistance(Vector3 location);
        void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color);
        void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position);
        string DebugQuaternion();
    }

    internal class PO_ObjectDisabled : IPO_Object
    {
        public uint Id { get; set; } // The InstanceID.NetLane value
        public int ProcId { get; set; } // The PO's id value

        public Vector3 Position
        {
            get => Vector3.zero;
            set { }
        }

        public float Angle
        {
            get => 0f;
            set { }
        }

        public bool Selected
        {
            get => false;
            set { }
        }

        private Info_PODisabled _info = new Info_PODisabled();
        public IInfo Info
        {
            get => _info;
            set => _info = (Info_PODisabled)value;
        }

        public void SetPositionY(float h)
        { }

        public object GetProceduralObject()
        {
            return null;
        }

        public float GetDistance(Vector3 location) => 0f;

        public void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color)
        { }

        public void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position)
        { }

        public string DebugQuaternion()
        {
            return "";
        }
    }

    public class Info_PODisabled : IInfo
    {
        public string Name => "";
        public PrefabInfo Prefab { get; set; } = null;
    }
}
