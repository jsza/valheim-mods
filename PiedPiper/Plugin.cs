using BepInEx;
using HarmonyLib;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using ZstdNet;
using OC = Mono.Cecil.Cil.OpCodes;

namespace Valheim_PiedPiper
{
    [BepInPlugin("MVP.PiedPiper", "Pied Piper - Fast Network Compression", "0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
    }

    public static class CompStatsMan
    {
        public static Dictionary<ZSteamSocket, CompStats> compStats = new Dictionary<ZSteamSocket, CompStats>();
        public static Dictionary<ZSteamSocket, CompressionStream> compStreams = new Dictionary<ZSteamSocket, CompressionStream>();
        public static Dictionary<ZSteamSocket, MemoryStream> compStreamsData = new Dictionary<ZSteamSocket, MemoryStream>();

        public static CompressionStream GetCompStream(ZSteamSocket socket)
        {
            CompressionStream compStream;
            if (!compStreams.TryGetValue(socket, out compStream))
            {
                var stream = new MemoryStream();
                compStream = new CompressionStream(new MemoryStream());
                compStreams.Add(socket, compStream);
                compStreamsData.Add(socket, stream);
            }
            return compStream;
        }
    }

    public class CompStats
    {
        public long dataSent;
        public long dataSentCompressed;
        public long dataSentZstd;
        public long dataSentZstdStream;
        public long dataSentZstdSlow;
        public float timeElapsed;
    }

    public static class CompressionMan
    {
        public static Compressor compressor = new Compressor();
        //public static Compressor slowCompressor = new Compressor(new CompressionOptions(1));
        public static Decompressor decompressor = new Decompressor();

        public static byte[] ProcessForCompression(byte[] data, ZSteamSocket socket)
        {
            return compressor.Wrap(data);
            byte[] dataClone = data.ToArray();
            //buffer.Add(dataClone);

            /*using (var stream = new FileStream("C:\\Users\\jayess\\code\\ValheimCompressionDictData.dat", FileMode.Append))
            {
                stream.Write(data, 0, data.Length);
            }*/

            CompStats cs;
            if (!CompStatsMan.compStats.TryGetValue(socket, out cs))
            {
                cs = new CompStats();
                CompStatsMan.compStats.Add(socket, cs);
            }
            cs.dataSent += dataClone.Length;

            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(dataClone.ToArray(), 0, dataClone.Length);
            }
            cs.dataSentCompressed += output.ToArray().Length;


            cs.dataSentZstd += CompressionMan.compressor.Wrap(data.ToArray()).Length;
            //cs.dataSentZstdSlow += CompressionMan.slowCompressor.Wrap(data.ToArray()).Length;

            //using (var compressor = new Compressor(new CompressionOptions(File.ReadAllBytes("C:\\Users\\jayess\\code\\ValheimCompressionDict.dat"))))
            //{
            //    cs.dataSentZstdStream += compressor.Wrap(data.ToArray()).Length;
            //}
        }

        public static byte[] ProcessForDecompression(byte[] data, ZSteamSocket socket)
        {
            // TODO: Check if data is compressed
            //if (IsZstdCompressed(data)) {
            //}
            return decompressor.Unwrap(data);
        }
    }


    [Harmony]
    public static class Patches
    {
        //[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
        //public static class ZDOMan_SendZDOs_Patch
        //{
        //    public static void SerializeIfDataChanged(ZDOMan.ZDOPeer peer, ZDO zdo, ZPackage pkg, ZPackage pkg2)
        //    {
        //        ZDOMan.ZDOPeer.PeerZDOInfo peerZDOInfo;
        //        if (!peer.m_zdos.TryGetValue(zdo.m_uid, out peerZDOInfo) || zdo.m_dataRevision != peerZDOInfo.m_dataRevision)
        //        {
        //            zdo.Serialize(pkg2);
        //        }
        //        pkg.Write(pkg2);
        //    }

        //    public static void ILManipulator(ILContext il)
        //    {
        //        int idxZDO = 0;
        //        int idxPkg = 0;
        //        int idxPkg2 = 0;
        //        new ILCursor(il)
        //            .GotoNext(MoveType.AfterLabel,
        //                i => i.MatchLdloc(out idxZDO),
        //                i => i.MatchLdloc(out idxPkg2),
        //                i => i.MatchCallvirt<ZDO>("Serialize"),
        //                i => i.MatchLdloc(out idxPkg),
        //                i => i.MatchLdloc(out _),
        //                i => i.MatchCallvirt<ZPackage>("Write")
        //            )
        //            .RemoveRange(6)
        //            .Emit(OC.Ldarg_1)
        //            .Emit(OC.Ldloc, idxZDO)
        //            .Emit(OC.Ldloc, idxPkg)
        //            .Emit(OC.Ldloc, idxPkg2)
        //            .EmitDelegate<Action<ZDOMan.ZDOPeer, ZDO, ZPackage, ZPackage>>(SerializeIfDataChanged)
        //        ;
        //    }
        //}

        //[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
        //public static class ZDOMan_SendZDOs_Patch
        //{
        //    public static void ILManipulator(ILContext il)
        //    {
        //        new ILCursor(il)
        //            .GotoNext(MoveType.AfterLabel,
        //                i => i.MatchLdcI4(10240)
        //            )
        //            .Remove()
        //            .Emit(OC.Ldc_I4, 20480)
        //            .GotoNext(MoveType.AfterLabel,
        //                i => i.MatchLdcI4(2048)
        //            )
        //            .Remove()
        //            .Emit(OC.Ldc_I4, 4096)
        //        ;
        //    }
        //}

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
        public static class ZNet_Shutdown_Patch
        {
            public static void Prefix()
            {
                //var dict = DictBuilder.TrainFromBuffer(ZSteamSocket_SendQueuedPackages_Patch.buffer, 1048576);
                //File.WriteAllBytes("C:\\Users\\jayess\\code\\ValheimCompressionDict.dat", dict);
            }
        }

        [HarmonyPatch(typeof(ZSteamSocket))]
        public static class ZSteamSocket_Patches
        {
            public static List<byte[]> buffer = new List<byte[]>();

            [HarmonyILManipulator]
            [HarmonyPatch(nameof(ZSteamSocket.SendQueuedPackages))]
            public static void Transpile_SendQueuedPackages(ILContext il)
            {
                var cursor = new ILCursor(il);
                //foreach (var instr in cursor.Instrs)
                //{
                //    ZLog.Log($"{instr.OpCode} {instr.Operand}");
                //}
                cursor
                    .GotoNext(MoveType.After,
                        i => i.MatchCallvirt(AccessTools.Method(typeof(Queue<byte[]>), "Peek"))
                    )
                    // Push "this" (ZSteamSocket instance) to call stack
                    .Emit(OC.Ldarg_0)
                    // 
                    .EmitDelegate<Func<byte[], ZSteamSocket, byte[]>>(CompressionMan.ProcessForCompression)
                ;
            }

            [HarmonyILManipulator]
            [HarmonyPatch(nameof(ZSteamSocket.Recv))]
            public static void Transpile_Recv(ILContext il)
            {
                var cursor = new ILCursor(il);
                //foreach (var instr in cursor.Instrs)
                //{
                //    ZLog.Log($"{instr.OpCode} {instr.Operand}");
                //}
                int idxZpkg = 0;
                cursor
                    .GotoNext(MoveType.After,
                        i => i.MatchCall(AccessTools.Method(typeof(Marshal), nameof(Marshal.Copy), new Type[] { typeof(IntPtr), typeof(byte[]), typeof(int), typeof(int) })),
                        i => i.MatchLdloc(out _),
                        i => i.MatchNewobj<ZPackage>()
                    )
                    .GotoPrev(MoveType.After,
                        i => i.MatchLdloc(out idxZpkg)
                    )
                    .Emit(OC.Ldarg_0)
                    .EmitDelegate<Func<byte[], ZSteamSocket, byte[]>>(CompressionMan.ProcessForDecompression)
                ;
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(ZSteamSocket.Update))]
            public static void Post_Update(ZSteamSocket __instance, float dt)
            {
                return;
                CompStats cs;
                if (!CompStatsMan.compStats.TryGetValue(__instance, out cs))
                {
                    cs = new CompStats();
                    CompStatsMan.compStats.Add(__instance, cs);
                }

                if (cs.timeElapsed >= 5f)
                {
                    ZLog.Log(String.Concat(new[] {
                            $"Data sent\n" +
                            $"Uncompressed: {cs.dataSent}\n" +
                            $"Deflate: {cs.dataSentCompressed} | Ratio {((cs.dataSentCompressed > 0 && cs.dataSent > 0) ? ((float)cs.dataSentCompressed / (float)cs.dataSent * 100f) : 0f)}\n",
                            $"ZStd 3: {cs.dataSentZstd} | Ratio {((cs.dataSentZstd> 0 && cs.dataSent > 0) ? ((float)cs.dataSentZstd / (float)cs.dataSent * 100f) : 0f)}\n",
                            $"ZStd 1: {cs.dataSentZstdSlow} | Ratio {((cs.dataSentZstdSlow> 0 && cs.dataSent > 0) ? ((float)cs.dataSentZstdSlow / (float)cs.dataSent * 100f) : 0f)} "
                            //$"ZStd Dict: {cs.dataSentZstdStream} | Ratio {((cs.dataSentZstdStream> 0 && cs.dataSent > 0) ? ((float)cs.dataSentZstdStream / (float)cs.dataSent * 100f) : 0f)} "
                        }));
                    cs.dataSent = 0L;
                    cs.dataSentCompressed = 0L;
                    cs.dataSentZstd = 0L;
                    cs.dataSentZstdStream = 0L;
                    cs.dataSentZstdSlow = 0L;
                    cs.timeElapsed = 0f;
                }
                cs.timeElapsed += dt;
            }
        }
    }
}
