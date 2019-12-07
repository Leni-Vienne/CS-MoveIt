﻿using ColossalFramework;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

// Low level PO wrapper, only accessed by high level

namespace MoveIt
{
    internal class PO_LogicEnabled : MonoBehaviour, IPO_Logic
    {
        internal static Assembly POAssembly = null;
        protected Type _tPOLogic = null;
        internal Type tPOLogic = null, tPOMod = null, tPO = null, tPInfo = null, tPUtils = null, tVertex = null, tPOMoveIt = null;
        internal object POLogic = null;

        private readonly BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        private void Start()
        {
            POAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Length >= 17 && assembly.FullName.Substring(0, 17) == "ProceduralObjects")
                {
                    POAssembly = assembly;
                    break;
                }
            }

            if (POAssembly == null)
            {
                throw new NullReferenceException("PO Assembly not found [PO-F3]");
            }

            tPOLogic = POAssembly.GetType("ProceduralObjects.ProceduralObjectsLogic");
            tPOMod = POAssembly.GetType("ProceduralObjects.ProceduralObjectsMod");
            tPO = POAssembly.GetType("ProceduralObjects.Classes.ProceduralObject");
            tPInfo = POAssembly.GetType("ProceduralObjects.Classes.ProceduralInfo");
            tPUtils = POAssembly.GetType("ProceduralObjects.Classes.ProceduralUtils");
            tVertex = POAssembly.GetType("ProceduralObjects.Classes.Vertex");
            tPOMoveIt = POAssembly.GetType("ProceduralObjects.Classes.PO_MoveIt");
            POLogic = FindObjectOfType(tPOLogic);
        }

        public List<IPO_Object> Objects
        {
            get
            {
                List<IPO_Object> objects = new List<IPO_Object>();

                var objectList = tPOLogic.GetField("proceduralObjects", flags).GetValue(POLogic);

                HashSet<int> activeIds = (HashSet<int>)tPOLogic.GetField("activeIds", flags).GetValue(POLogic);
                string msg = $"activeIds ({activeIds.Count}):\n";
                foreach (int a in activeIds)
                {
                    msg += $"{a},";
                }
                Debug.Log(msg);

                int count = (int)objectList.GetType().GetProperty("Count").GetValue(objectList, null);
                msg = $"List ({count}):\n";
                for (int i = 0; i < count; i++)
                {
                    var v = objectList.GetType().GetMethod("get_Item").Invoke(objectList, new object[] { i });
                    IPO_Object o = new PO_ObjectEnabled(v);
                    objects.Add(o);
                    msg += $"{o.ProcId},";
                }
                Debug.Log(msg);

                return objects;
            }
        }

        public void Clone(uint originalId, Vector3 position, float angle, Action action)
        {
            MoveItTool.POProcessing = true;
            tPOMoveIt.GetMethod("CallPOCloning", new Type[] { tPO }).Invoke(null, new[] { GetPOById(originalId).GetProceduralObject() });
            StartCoroutine(RetrieveClone(originalId, position, angle, action));
        }

        public IEnumerator<object> RetrieveClone(uint originalId, Vector3 position, float angle, Action action)
        {
            const uint MaxAttempts = 1000_000;

            var original = GetPOById(originalId).GetProceduralObject();
            Type[] types = new Type[] { tPO, tPO.MakeByRefType(), typeof(uint).MakeByRefType() };
            object[] paramList = new[] { original, null, null };
            MethodInfo retrieve = tPOMoveIt.GetMethod("TryRetrieveClone", BindingFlags.Public | BindingFlags.Static, null, types, null );

            uint c = 0;
            while (c < MaxAttempts && !(bool)retrieve.Invoke(null, paramList))
            {
                if (c % 100 == 0)
                {
                    BindingFlags f = BindingFlags.Static | BindingFlags.Public;
                    object queueObj = tPOMoveIt.GetField("queuedCloning", f).GetValue(null);
                    int queueCount = (int)queueObj.GetType().GetProperty("Count").GetValue(queueObj, null);
                    object doneObj = tPOMoveIt.GetField("doneCloning", f).GetValue(null);
                    int doneCount = (int)doneObj.GetType().GetProperty("Count").GetValue(doneObj, null);
                }
                c++;
                yield return new WaitForSeconds(0.05f);
            }

            if (c == MaxAttempts)
            {
                throw new Exception($"Failed to clone object #{originalId}! [PO-F4]");
            }

            IPO_Object clone = new PO_ObjectEnabled(paramList[1]);

            InstanceID cloneID = default;
            cloneID.NetLane = clone.Id;
            MoveItTool.PO.visibleObjects.Add(cloneID.NetLane, clone);

            MoveableProc cloneInstance = new MoveableProc(cloneID)
            {
                position = position,
                angle = angle
            };

            Action.selection.Add(cloneInstance);
            ((CloneAction)action).m_clones.Add(cloneInstance);
            //MoveItTool.PO.SelectionAdd(cloneInstance);

            MoveItTool.instance.ToolState = MoveItTool.ToolStates.Default;

            yield return new WaitForSeconds(0.25f);
            Debug.Log($"Cloned {originalId} to #{clone.Id}");
            MoveItTool.POProcessing = false;
        }

        public void Delete(IPO_Object obj)
        {
            var poList = tPOLogic.GetField("proceduralObjects", flags).GetValue(POLogic);
            var poSelList = tPOLogic.GetField("pObjSelection", flags).GetValue(POLogic);

            poList.GetType().GetMethod("Remove", flags, null, new Type[] { tPO }, null).Invoke(poList, new object[] { obj.GetProceduralObject() });
            poSelList.GetType().GetMethod("Remove", flags, null, new Type[] { tPO }, null).Invoke(poSelList, new object[] { obj.GetProceduralObject() });
            if (tPOLogic.GetField("activeIds", flags) != null)
            {
                var activeIds = tPOLogic.GetField("activeIds", flags).GetValue(POLogic);
                activeIds.GetType().GetMethod("Remove", flags, null, new Type[] { typeof(int) }, null).Invoke(activeIds, new object[] { obj.ProcId });
            }
        }

        /// <param name="id">The NetLane id</param>
        internal IPO_Object GetPOById(uint id)
        {
            foreach (IPO_Object po in Objects)
            {
                if (po.Id == id)
                    return po;
            }

            return null;
        }

        private object AvailableProcInfos
        {
            get => tPOLogic.GetField("availableProceduralInfos").GetValue(POLogic);
        }

        public IPO_Object ConvertToPO(Instance instance)
        {
            // Most code adapted from PO ProceduralObjectsLogic.ConvertToProcedural, by Simon Ryr

            if (AvailableProcInfos == null)
                tPOLogic.GetField("availableProceduralInfos").SetValue(POLogic, tPUtils.GetMethod("CreateProceduralInfosList").Invoke(null, null));
            if ((int)AvailableProcInfos.GetType().GetProperty("Count").GetValue(AvailableProcInfos, null) == 0)
                tPOLogic.GetField("availableProceduralInfos").SetValue(POLogic, tPUtils.GetMethod("CreateProceduralInfosList").Invoke(null, null));

            try
            {
                object procInfo = null;
                int count, i;

                if (instance is MoveableProp mp)
                {
                    count = (int)AvailableProcInfos.GetType().GetProperty("Count").GetValue(AvailableProcInfos, null);
                    for (i = 0; i < count; i++)
                    {
                        var info = AvailableProcInfos.GetType().GetMethod("get_Item").Invoke(AvailableProcInfos, new object[] { i });
                        if (info == null) continue;

                        if ((PropInfo)info.GetType().GetField("propPrefab").GetValue(info) == (PropInfo)instance.Info.Prefab)
                        {
                            procInfo = info;
                            break;
                        }
                    }

                    if (procInfo == null) throw new NullReferenceException("procInfo is null when converting to PO");

                    if ((bool)procInfo.GetType().GetField("isBasicShape").GetValue(procInfo) && 
                        (int)tPOLogic.GetField("basicTextures").GetValue(POLogic).GetType().GetProperty("Count").GetValue(tPOLogic.GetField("basicTextures").GetValue(POLogic), null) > 0)
                    {
                        tPOLogic.GetField("currentlyEditingObject").SetValue(POLogic, null);
                        tPOLogic.GetField("chosenProceduralInfo").SetValue(POLogic, procInfo);
                    }
                    else
                    {
                        tPOLogic.GetMethod("SpawnObject", new Type[] { tPInfo, typeof(Texture2D) }).Invoke(POLogic, new[] { procInfo, null });
                        var v = tVertex.GetMethod("CreateVertexList", new Type[] { tPO }).Invoke(null, new[] { tPOLogic.GetField("currentlyEditingObject").GetValue(POLogic) });
                        tPOLogic.GetField("tempVerticesBuffer").SetValue(POLogic, v);
                    }
                }
                else if (instance is MoveableBuilding mb)
                {
                    count = (int)AvailableProcInfos.GetType().GetProperty("Count").GetValue(AvailableProcInfos, null);
                    for (i = 0; i < count; i++)
                    {
                        var info = AvailableProcInfos.GetType().GetMethod("get_Item").Invoke(AvailableProcInfos, new object[] { i });
                        if (info == null) continue;

                        if ((BuildingInfo)info.GetType().GetField("buildingPrefab").GetValue(info) == (BuildingInfo)instance.Info.Prefab)
                        {
                            procInfo = info;
                            break;
                        }
                    }

                    if (procInfo == null) throw new NullReferenceException("procInfo is null when converting to PO");

                    if ((bool)procInfo.GetType().GetField("isBasicShape").GetValue(procInfo) &&
                        (int)tPOLogic.GetField("basicTextures").GetValue(POLogic).GetType().GetProperty("Count").GetValue(tPOLogic.GetField("basicTextures").GetValue(POLogic), null) > 0)
                    {
                        tPOLogic.GetField("currentlyEditingObject").SetValue(POLogic, null);
                        tPOLogic.GetField("chosenProceduralInfo").SetValue(POLogic, procInfo);
                    }
                    else
                    {
                        tPOLogic.GetMethod("SpawnObject", new Type[] { tPInfo, typeof(Texture2D) }).Invoke(POLogic, new[] { procInfo, null });
                        var v = tVertex.GetMethod("CreateVertexList", new Type[] { tPO }).Invoke(null, new[] { tPOLogic.GetField("currentlyEditingObject").GetValue(POLogic) });
                        tPOLogic.GetField("tempVerticesBuffer").SetValue(POLogic, v);
                    }
                }

                object poObj = tPOLogic.GetField("currentlyEditingObject").GetValue(POLogic);
                tPOLogic.GetField("pObjSelection", flags).GetValue(POLogic).GetType().GetMethod("Add", new Type[] { tPO }).Invoke(tPOLogic.GetField("pObjSelection", flags).GetValue(POLogic), new[] { poObj });
                return new PO_ObjectEnabled(poObj);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static string getVersion()
        {
            try
            {
                Assembly poAssembly = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Length >= 17 && assembly.FullName.Substring(0, 17) == "ProceduralObjects")
                    {
                        poAssembly = assembly;
                        break;
                    }
                }
                if (poAssembly == null)
                {
                    return "(Failed [PO-F1])";
                }

                Type tPO = poAssembly.GetType("ProceduralObjects.ProceduralObjectsMod");
                object version = tPO.GetField("VERSION", BindingFlags.Public | BindingFlags.Static).GetValue(null);

                return version.ToString();
            }
            catch (Exception e)
            {
                Debug.Log($"PO INTERATION FAILED\n" + e);
            }

            return "(Failed [PO-F2])";
        }
    }


    internal class PO_ObjectEnabled : IPO_Object
    {
        internal object procObj;

        public uint Id { get; set; } // The InstanceID.NetLane value
        public bool Selected { get; set; }
        public int ProcId { get => (int)Id - 1; set => Id = (uint)value + 1; }

        internal Type tPOLogic = null, tPOMod = null, tPO = null;

        public Vector3 Position
        {
            get => (Vector3)tPO.GetField("m_position").GetValue(procObj);
            set
            {
                tPO.GetField("m_position").SetValue(procObj, value);
            }
        }
        private Quaternion Rotation
        {
            get => (Quaternion)tPO.GetField("m_rotation").GetValue(procObj);
            set => tPO.GetField("m_rotation").SetValue(procObj, value);
        }

        public string Name
        {
            get
            {
                string name = (string)tPO.GetField("basePrefabName").GetValue(procObj);
                if (name.Length < 35)
                    return "[PO]" + name;
                return "[PO]" + name.Substring(0, 35);
            }
            set { }
        }

        public float Angle
        {
            get
            {
                float a = -Rotation.eulerAngles.y % 360f;
                if (a < 0) a += 360f;
                return a * Mathf.Deg2Rad;
            }

            set
            {
                float a = -(value * Mathf.Rad2Deg) % 360f;
                if (a < 0) a += 360f;
                
                Quaternion q = Rotation;
                q.eulerAngles = new Vector3(Rotation.eulerAngles.x, a, Rotation.eulerAngles.z);
                Rotation = q;
            }
        }

        private Info_POEnabled _info = null;
        public IInfo Info
        {
            get
            {
                if (_info == null)
                    _info = new Info_POEnabled(this);

                return _info;
            }
            set => _info = (Info_POEnabled)value;
        }

        public object GetProceduralObject()
        {
            return procObj;
        }

        public PO_ObjectEnabled(object obj)
        {
            tPOLogic = PO_LogicEnabled.POAssembly.GetType("ProceduralObjects.3Logic");
            tPOMod = PO_LogicEnabled.POAssembly.GetType("ProceduralObjects.ProceduralObjectsMod");
            tPO = PO_LogicEnabled.POAssembly.GetType("ProceduralObjects.Classes.ProceduralObject");

            procObj = obj;
            ProcId = (int)tPO.GetField("id").GetValue(procObj);
        }

        public void SetPositionY(float h)
        {
            Position = new Vector3(Position.x, h, Position.z);
        }

        public float GetDistance(Vector3 location)
        {
            return Vector3.Distance(Position, location);
        }

        public void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color)
        {
            RenderOverlay(cameraInfo, color, Position);
        }

        public void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, Vector3 position)
        {
            float size = 4f;
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, color, position, size, Position.y - 100f, Position.y + 100f, renderLimits: false, alphaBlend: true);
        }

        public string DebugQuaternion()
        {
            return $"{Id}:{Rotation.w},{Rotation.x},{Rotation.y},{Rotation.z}";
        }
    }


    public class Info_POEnabled : IInfo
    {
        private PO_ObjectEnabled _obj = null;
        private PrefabInfo _Prefab = null;

        public Info_POEnabled(object i)
        {
            _obj = (PO_ObjectEnabled)i;
        }

        public string Name => _obj.Name;

        public PrefabInfo Prefab
        {
            get
            {
                _Prefab = (PrefabInfo)_obj.tPO.GetField("_baseBuilding").GetValue(_obj.procObj);
                if (_Prefab == null)
                {
                    _Prefab = (PrefabInfo)_obj.tPO.GetField("_baseProp").GetValue(_obj.procObj);
                }
                return _Prefab;
            }
            set
            {
                _Prefab = value;
            }
        }
    }
}
