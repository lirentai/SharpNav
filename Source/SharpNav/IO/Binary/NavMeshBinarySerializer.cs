
#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

using SharpNav.Collections;
using SharpNav.Geometry;
using SharpNav.Pathfinding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SharpNav.IO.Binary
{
    public class NavMeshBinarySerializer : NavMeshSerializer
    {
        private static readonly int FormatVersion = 3;

        public TiledNavMesh Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream);
            ReadMeta(reader);
            var data = ReadInfo(reader);
            reader.Close();
            return data;
        }

        public override TiledNavMesh Deserialize(string path)
        {
            return Deserialize(File.OpenRead(path));
        }

        public override void Serialize(string path, TiledNavMesh mesh)
        {
            var writer = new BinaryWriter(File.OpenWrite(path));
            WriteMeta(writer);
            WriteInfo(writer, mesh);
            WriteTiles(writer, mesh);
            writer.Flush();
            writer.Close();
        }

        void WriteMeta(BinaryWriter writer)
        {
            writer.Write(FormatVersion);
            writer.Write(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
        }

        void ReadMeta(BinaryReader reader)
        {
            if (reader.ReadInt32() != FormatVersion)
            {
                throw new ArgumentException("The version of the file does not match the version of the parser. Consider using an older version of SharpNav or re-generating your .snj meshes.");
            }
            var informational_version = reader.ReadString();
        }

        void WriteInfo(BinaryWriter writer, TiledNavMesh mesh)
        {
            WriteVector3(writer, mesh.Origin);
            writer.Write(mesh.TileWidth);
            writer.Write(mesh.TileHeight);
            writer.Write(mesh.MaxTiles);
            writer.Write(mesh.MaxPolys);
        }

        TiledNavMesh ReadInfo(BinaryReader reader)
        {
            var origin = ReadVector3(reader);
            var tileWidth = reader.ReadSingle();
            var tileHeight = reader.ReadSingle();
            var maxTiles = reader.ReadSingle();
            var maxPolys = reader.ReadSingle();
            var mesh = new TiledNavMesh(origin, tileWidth, tileHeight, (int)maxTiles, (int)maxPolys);
            ReadTiles(reader, mesh);
            return mesh;
        }

        void WriteTiles(BinaryWriter writer, TiledNavMesh mesh)
        {
            writer.Write(mesh.Tiles.Count);
            for (var i = 0; i < mesh.Tiles.Count; i++)
            {
                var tile = mesh.Tiles[i];
                WriteMeshTile(writer, tile, mesh.GetTileRef(tile));
            }
        }

        void ReadTiles(BinaryReader reader, TiledNavMesh mesh)
        {
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var tile = ReadMeshTile(reader, mesh.IdManager, out var ref_id);
                mesh.AddTileAt(tile, ref_id);
            }
        }

        void WriteMeshTile(BinaryWriter writer, NavTile tile, NavPolyId id)
        {
            WriteNavPolyId(writer, id);
            WriteVector2i(writer, tile.Location);
            writer.Write(tile.Layer);
            writer.Write(tile.Salt);
            WriteBBox3(writer, tile.Bounds);
            WriteNavPolyArray(writer, tile.Polys);
            WriteVector3Array(writer, tile.Verts);
            WriteMeshDataArray(writer, tile.DetailMeshes);
            WriteVector3Array(writer, tile.DetailVerts);
            WriteTriangleDataArray(writer, tile.DetailTris);
            WriteOffMeshConnectionArray(writer, tile.OffMeshConnections);
            WriteBVTree(writer, tile.BVTree);
            writer.Write(tile.BvQuantFactor);
            writer.Write(tile.BvNodeCount);
            writer.Write(tile.WalkableClimb);
        }

        NavTile ReadMeshTile(BinaryReader reader, NavPolyIdManager manager, out NavPolyId refId)
        {
            refId = ReadNavPolyId(reader);
            var loction = ReadVector2i(reader);
            var layer = reader.ReadInt32();
            var data = new NavTile(loction, layer, manager, refId);
            data.Salt = reader.ReadInt32();
            data.Bounds = ReadBBox3(reader);
            data.Polys = ReadNavPolyArray(reader);
            data.PolyCount = data.Polys.Length;
            data.Verts = ReadVector3Array(reader);
            data.DetailMeshes = ReadMeshDataArray(reader);
            data.DetailVerts = ReadVector3Array(reader);
            data.DetailTris = ReadTriangleDataArray(reader);
            data.OffMeshConnections = ReadOffMeshConnectionArray(reader);
            data.OffMeshConnectionCount = data.OffMeshConnections.Length;
            data.BVTree = ReadBVTree(reader);
            data.BvQuantFactor = reader.ReadInt32();
            data.BvNodeCount = reader.ReadInt32();
            data.WalkableClimb = reader.ReadSingle();
            return data;
        }

        void WriteNavPolyArray(BinaryWriter writer, NavPoly[] polys)
        {
            writer.Write(polys.Length);
            for (var i = 0; i < polys.Length; i++)
            {
                WriteNavPoly(writer, polys[i]);
            }
        }

        NavPoly[] ReadNavPolyArray(BinaryReader reader)
        {
            var array = new NavPoly[reader.ReadInt32()];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = ReadNavPoly(reader);
            }
            return array;
        }

        void WriteNavPoly(BinaryWriter writer, NavPoly poly)
        {
            writer.Write((byte)poly.PolyType);
            WriteLinkList(writer, poly.Links);
            WriteIntArray(writer, poly.Verts);
            WriteIntArray(writer, poly.Neis);
            //TODO TAG
            writer.Write(poly.VertCount);
            WriteArea(writer, poly.Area);
        }

        NavPoly ReadNavPoly(BinaryReader reader)
        {
            var poly = new NavPoly();
            poly.PolyType = (NavPolyType)reader.ReadByte();
            ReadLinkList(reader, poly.Links);
            poly.Verts = ReadIntArray(reader);
            poly.Neis = ReadIntArray(reader);
            poly.VertCount = reader.ReadInt32();
            poly.Area = ReadArea(reader);
            return poly;
        }

        void WriteLinkList(BinaryWriter writer, List<Link> links)
        {
            writer.Write(links.Count);
            for (var i = 0; i < links.Count; i++)
            {
                WriteLink(writer, links[i]);
            }
        }

        void ReadLinkList(BinaryReader reader, List<Link> list)
        {
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                list.Add(ReadLink(reader));
            }
        }

        void WriteLink(BinaryWriter writer, Link link)
        {
            WriteNavPolyId(writer, link.Reference);
            writer.Write(link.Edge);
            writer.Write((byte)link.Side);
            writer.Write(link.BMin);
            writer.Write(link.BMax);
        }

        Link ReadLink(BinaryReader reader)
        {
            var reference = ReadNavPolyId(reader);
            var edge = reader.ReadInt32();
            var side = reader.ReadByte();
            var min = reader.ReadInt32();
            var max = reader.ReadInt32();
            return new Link
            {
                Reference = reference,
                Edge = edge,
                Side = (BoundarySide)side,
                BMin = min,
                BMax = max,
            };
        }

        void WriteArea(BinaryWriter write, Area area)
        {
            write.Write(area.Id);
        }

        Area ReadArea(BinaryReader reader)
        {
            return new Area(reader.ReadByte());
        }

        void WriteMeshDataArray(BinaryWriter writer, PolyMeshDetail.MeshData[] array)
        {
            if (array == null)
            {
                writer.Write(0);
                return;
            }
            writer.Write(array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                WriteMeshData(writer, array[i]);
            }
        }

        PolyMeshDetail.MeshData[] ReadMeshDataArray(BinaryReader reader)
        {
            var array = new PolyMeshDetail.MeshData[reader.ReadInt32()];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = ReadMeshData(reader);
            }
            return array;
        }

        void WriteMeshData(BinaryWriter write, PolyMeshDetail.MeshData data)
        {
            write.Write(data.VertexIndex);
            write.Write(data.VertexCount);
            write.Write(data.TriangleIndex);
            write.Write(data.TriangleCount);
        }

        PolyMeshDetail.MeshData ReadMeshData(BinaryReader reader)
        {
            var vertex_index = reader.ReadInt32();
            var vertex_count = reader.ReadInt32();
            var triangle_index = reader.ReadInt32();
            var triangle_count = reader.ReadInt32();
            return new PolyMeshDetail.MeshData
            {
                VertexCount = vertex_count,
                VertexIndex = vertex_index,
                TriangleCount = triangle_count,
                TriangleIndex = triangle_index
            };
        }

        void WriteTriangleDataArray(BinaryWriter writer, PolyMeshDetail.TriangleData[] array)
        {
            if (array == null)
            {
                writer.Write(0);
                return;
            }
            writer.Write(array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                WriteTriangleData(writer, array[i]);
            }
        }

        PolyMeshDetail.TriangleData[] ReadTriangleDataArray(BinaryReader reader)
        {
            var array = new PolyMeshDetail.TriangleData[reader.ReadInt32()];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = ReadTriangleData(reader);
            }
            return array;
        }

        void WriteTriangleData(BinaryWriter writer, PolyMeshDetail.TriangleData data)
        {
            writer.Write(data.VertexHash0);
            writer.Write(data.VertexHash1);
            writer.Write(data.VertexHash2);
            writer.Write(data.Flags);
        }

        PolyMeshDetail.TriangleData ReadTriangleData(BinaryReader reader)
        {
            var vertex_hash0 = reader.ReadInt32();
            var vertex_hash1 = reader.ReadInt32();
            var vertex_hash2 = reader.ReadInt32();
            var flags = reader.ReadInt32();
            return new PolyMeshDetail.TriangleData(vertex_hash0, vertex_hash1, vertex_hash2, flags);
        }

        void WriteOffMeshConnectionArray(BinaryWriter writer, OffMeshConnection[] array)
        {
            if (array == null)
            {
                writer.Write(0);
                return;
            }
            writer.Write(array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                WriteOffMeshConnection(writer, array[i]);
            }
        }

        OffMeshConnection[] ReadOffMeshConnectionArray(BinaryReader reader)
        {
            var array = new OffMeshConnection[reader.ReadInt32()];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = ReadOffMeshConnection(reader);
            }
            return array;
        }

        void WriteOffMeshConnection(BinaryWriter writer, OffMeshConnection data)
        {
            WriteVector3(writer, data.Pos0);
            WriteVector3(writer, data.Pos1);
            writer.Write(data.Radius);
            writer.Write(data.Poly);
            writer.Write((byte)data.Flags);
            writer.Write((byte)data.Side);
            //todo tag support
        }

        OffMeshConnection ReadOffMeshConnection(BinaryReader reader)
        {
            var pos0 = ReadVector3(reader);
            var pos1 = ReadVector3(reader);
            var radius = reader.ReadSingle();
            var poly = reader.ReadInt32();
            var flags = reader.ReadByte();
            var side = reader.ReadByte();
            return new OffMeshConnection
            {
                Pos0 = pos0,
                Pos1 = pos1,
                Radius = radius,
                Poly = poly,
                Flags = (OffMeshConnectionFlags)flags,
                Side = (BoundarySide)side,
            };
        }

        void WriteBVTree(BinaryWriter writer, BVTree data)
        {
            writer.Write(data.Count);
            for (var i = 0; i < data.Count; i++)
            {
                WriteNode(writer, data[i]);
            }
        }

        BVTree ReadBVTree(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var nodes = new BVTree.Node[length];
            for (var i = 0; i < length; i++)
            {
                nodes[i] = ReadNode(reader);
            }
            return new BVTree(nodes);
        }

        void WriteNode(BinaryWriter writer, BVTree.Node data)
        {
            WritePolyBounds(writer, data.Bounds);
            writer.Write(data.Index);
        }

        BVTree.Node ReadNode(BinaryReader reader)
        {
            var bounds = ReadPolyBounds(reader);
            var index = reader.ReadInt32();
            return new BVTree.Node()
            {
                Bounds = bounds,
                Index = index
            };
        }

        void WritePolyBounds(BinaryWriter writer, PolyBounds bounds)
        {
            WritePolyVertex(writer, bounds.Min);
            WritePolyVertex(writer, bounds.Max);
        }

        PolyBounds ReadPolyBounds(BinaryReader reader)
        {
            var min = ReadPolyVertex(reader);
            var max = ReadPolyVertex(reader);
            return new PolyBounds(min, max);
        }

        void WriteIntArray(BinaryWriter writer, int[] array)
        {
            if (array == null)
            {
                writer.Write(0);
                return;
            }
            writer.Write(array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                writer.Write(array[i]);
            }
        }

        int[] ReadIntArray(BinaryReader reader)
        {
            var array = new int[reader.ReadInt32()];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = reader.ReadInt32();
            }
            return array;
        }

        void WriteVector3Array(BinaryWriter writer, Vector3[] array)
        {
            if (array == null)
            {
                writer.Write(0);
                return;
            }
            writer.Write(array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                WriteVector3(writer, array[i]);
            }
        }

        Vector3[] ReadVector3Array(BinaryReader reader)
        {
            var array = new Vector3[reader.ReadInt32()];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = ReadVector3(reader);
            }
            return array;
        }

        void WriteNavPolyId(BinaryWriter writer, NavPolyId id)
        {
            writer.Write(id.Id);
        }

        NavPolyId ReadNavPolyId(BinaryReader reader)
        {
            return new NavPolyId(reader.ReadInt32());
        }

        void WriteBBox3(BinaryWriter writer, BBox3 bbox3)
        {
            WriteVector3(writer, bbox3.Min);
            WriteVector3(writer, bbox3.Max);
        }

        BBox3 ReadBBox3(BinaryReader reader)
        {
            var min = ReadVector3(reader);
            var max = ReadVector3(reader);
            return new BBox3(min, max);
        }

        void WriteVector2i(BinaryWriter writer, Vector2i vector2i)
        {
            writer.Write(vector2i.X);
            writer.Write(vector2i.Y);
        }

        Vector2i ReadVector2i(BinaryReader reader)
        {
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            return new Vector2i(x, y);
        }

        void WriteVector3(BinaryWriter writer, Vector3 vector3)
        {
            writer.Write(vector3.X);
            writer.Write(vector3.Y);
            writer.Write(vector3.Z);
        }

        Vector3 ReadVector3(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        void WritePolyVertex(BinaryWriter writer, PolyVertex vector3)
        {
            writer.Write(vector3.X);
            writer.Write(vector3.Y);
            writer.Write(vector3.Z);
        }

        PolyVertex ReadPolyVertex(BinaryReader reader)
        {
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var z = reader.ReadInt32();
            return new PolyVertex(x, y, z);
        }
    }
}
