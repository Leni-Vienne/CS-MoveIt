﻿using ColossalFramework.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// TMPE wrapper, supports 2.0

namespace MoveIt
{
    internal class TMPE_Manager
    {
        const string NAME = "TrafficManager";
        const UInt64 ID1 = 1806963141;
        const UInt64 ID2 = 1637663252;

        internal bool Enabled = false;
        internal readonly Assembly Assembly;

        internal readonly Type tRecordable, tNodeRecord, tSegmentRecord, tSegmentEndRecord, tRecordUtil;
        internal readonly MethodInfo mRecord, mTransfer, mSerialize, mDeserialize;
        internal readonly ConstructorInfo mNewNodeRecord, mNewSegmentRecord, mNewSegmentEndRecord;

        internal TMPE_Manager()
        {
            if (isModInstalled())
            {
                Enabled = true;

                Assembly = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.StartsWith(NAME))
                    {
                        Assembly = assembly;
                        break;
                    }
                }

                if (Assembly == null) throw new Exception("Assembly not found (Failed [TMPE-F1])");

                tRecordable = Assembly.GetType("TrafficManager.Util.Record.IRecordable")
                    ?? throw new Exception("Type TrafficManager.Util.Record.IRecordable not found (Failed [TMPE-F2])");

                tNodeRecord = Assembly.GetType("TrafficManager.Util.Record.NodeRecord")
                    ?? throw new Exception("Type TrafficManager.Util.Record.NodeRecord not found (Failed [TMPE-F3])");

                tSegmentRecord = Assembly.GetType("TrafficManager.Util.Record.SegmentRecord")
                    ?? throw new Exception("Type TrafficManager.Util.Record.SegmentRecord not found (Failed [TMPE-F4])");

                tSegmentEndRecord = Assembly.GetType("TrafficManager.Util.Record.SegmentEndRecord")
                    ?? throw new Exception("Type TrafficManager.Util.Record.SegmentEndRecord not found (Failed [TMPE-F5])");

                tRecordUtil = Assembly.GetType("TrafficManager.Util.Record.RecordUtil")
                    ?? throw new Exception("Type TrafficManager.Util.Record.RecordUtil not found (Failed [TMPE-F6])");

                mRecord = tRecordable.GetMethod("Record")
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.Record() not found (Failed [TMPE-F1-10)");

                mTransfer = tRecordable.GetMethod("Transfer")
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.Transfer(map) not found (Failed [TMPE-F11])");

                mSerialize = tRecordable.GetMethod("Serialize")
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.Serialize() not found (Failed [TMPE-F12])");

                mDeserialize = tRecordUtil.GetMethod("Deserialize")
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.Deserialize(byte[]) not found (Failed [TMPE-F13])");

                mNewNodeRecord = tNodeRecord.GetConstructor(new Type[] { typeof(ushort) })
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.NodeRecord..ctor(id) not found (Failed [TMPE-F20])");

                mNewSegmentRecord = tSegmentRecord.GetConstructor(new Type[] { typeof(ushort) })
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.SegmentRecord..ctor(id) not found (Failed [TMPE-F21])");

                mNewSegmentEndRecord = tSegmentEndRecord.GetConstructor(new Type[] { typeof(ushort), typeof(bool) })
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.SegmentEndRecord..ctor(id,startNode) not found (Failed [TMPE-F22])");

            }
            else
            {
                Enabled = false;
            }
        }

        internal object CopyNode(ushort nodeId)
        {
            var args = new object[] { nodeId };
            object record = mNewNodeRecord.Invoke(args);
            mRecord.Invoke(record, null);
            return record;
        }

        internal object CopySegment(ushort segmentId)
        {
            var args = new object[] { segmentId };
            object record = mNewSegmentRecord.Invoke(args);
            mRecord.Invoke(record, null);
            return record;
        }

        internal object CopySegmentEnd(ushort segmentId, bool startNode)
        {
            var args = new object[] { segmentId, startNode };
            object record = mNewSegmentRecord.Invoke(args);
            mRecord.Invoke(record, null);
            return record;
        }

        internal void Paste(object record, Dictionary<InstanceID,InstanceID> map)
        {
            var args = new object[] { map };
            mTransfer.Invoke(record, args);
        }

        internal string Encode(object record)
        {
            byte[] bytes = mSerialize.Invoke(record, null) as byte[];
            return Convert.ToBase64String(bytes);
        }

        internal object Decode(string data)
        {
            byte[] bytes = Convert.FromBase64String(data);
            var args = new object[] { bytes };
            return mDeserialize.Invoke(null, args) as byte[];
        }

        internal static bool isModInstalled()
        {
            return PluginManager.instance.GetPluginsInfo().Any(mod => (
                    mod.publishedFileID.AsUInt64 == ID1 ||
                    mod.publishedFileID.AsUInt64 == ID2 ||
                    mod.name.Contains(NAME)
            ) && mod.isEnabled);
        }

        internal static string getVersionText()
        {
            if (isModInstalled())
            {
                return "Traffic manager found, integration enabled!\n ";
            }

            return "Traffic manager not found, integration disabled.\n ";
        }
    }
}
