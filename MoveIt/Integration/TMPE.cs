﻿using ColossalFramework.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        internal static readonly Version MinVersion = new Version(11, 5, 1, 0);
        
        internal readonly Version Version;

        internal readonly Assembly Assembly;

        internal readonly Type tRecordable, tNodeRecord, tSegmentRecord, tSegmentEndRecord;
        internal readonly MethodInfo mRecord, mTransfer;
        internal readonly ConstructorInfo mNewNodeRecord, mNewSegmentRecord, mNewSegmentEndRecord;

        internal TMPE_Manager()
        {
            if (isModInstalled())
            {
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

                Version = Assembly.GetName().Version;
                if (Version < MinVersion)
                    return;

                tRecordable = Assembly.GetType("TrafficManager.Util.Record.IRecordable")
                    ?? throw new Exception("Type TrafficManager.Util.Record.IRecordable not found (Failed [TMPE-F2])");

                tNodeRecord = Assembly.GetType("TrafficManager.Util.Record.NodeRecord")
                    ?? throw new Exception("Type TrafficManager.Util.Record.NodeRecord not found (Failed [TMPE-F3])");

                tSegmentRecord = Assembly.GetType("TrafficManager.Util.Record.SegmentRecord")
                    ?? throw new Exception("Type TrafficManager.Util.Record.SegmentRecord not found (Failed [TMPE-F4])");

                tSegmentEndRecord = Assembly.GetType("TrafficManager.Util.Record.SegmentEndRecord")
                    ?? throw new Exception("Type TrafficManager.Util.Record.SegmentEndRecord not found (Failed [TMPE-F5])");

                mRecord = tRecordable.GetMethod("Record")
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.Record() not found (Failed [TMPE-F1-10)");

                mTransfer = tRecordable.GetMethod("Transfer")
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.Transfer(map) not found (Failed [TMPE-F11])");

                mNewNodeRecord = tNodeRecord.GetConstructor(new Type[] { typeof(ushort) })
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.NodeRecord..ctor(id) not found (Failed [TMPE-F20])");

                mNewSegmentRecord = tSegmentRecord.GetConstructor(new Type[] { typeof(ushort) })
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.SegmentRecord..ctor(id) not found (Failed [TMPE-F21])");

                mNewSegmentEndRecord = tSegmentEndRecord.GetConstructor(new Type[] { typeof(ushort), typeof(bool) })
                    ?? throw new Exception("Method TrafficManager.Util.Record.IRecordable.SegmentEndRecord..ctor(id,startNode) not found (Failed [TMPE-F22])");

            }
            else
            {
                Version = new Version(0,0);
            }
        }

        internal object CopyNode(ushort nodeId)
        {
            if (Version < MinVersion) return null;
            var args = new object[] { nodeId };
            object record = mNewNodeRecord.Invoke(args);
            mRecord.Invoke(record, null);
            return record;
        }

        internal object CopySegment(ushort segmentId)
        {
            if (Version < MinVersion) return null;
            var args = new object[] { segmentId };
            object record = mNewSegmentRecord.Invoke(args);
            mRecord.Invoke(record, null);
            return record;
        }

        internal object CopySegmentEnd(ushort segmentId, bool startNode)
        {
            if (Version < MinVersion) return null;
            object[] args = new object[] { segmentId, startNode };
            object record = mNewSegmentEndRecord.Invoke(args);
            mRecord.Invoke(record, null);
            return record;
        }

        internal void Paste(object record, Dictionary<InstanceID,InstanceID> map)
        {
            if (Version < MinVersion || record == null)
                return;
            var args = new object[] { map };
            mTransfer.Invoke(record, args);
        }

        internal string Encode64(object record)
        {
            if (Version < MinVersion || record == null)
                return null;
            return EncodeUtil.Encode64(record);
        }
        internal object Decode64(string base64Data)
        {
            if (Version < MinVersion || base64Data == null || base64Data=="")
                return null;
            return EncodeUtil.Decode64(base64Data);
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