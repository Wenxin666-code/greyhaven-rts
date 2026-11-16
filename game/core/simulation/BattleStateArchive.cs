using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Greyhaven;

internal static class BattleStateArchive
{
    private const int MaxEntries = 8_000_000;
    private const int MaxDepth = 192;
    private static readonly HashSet<string> DerivedCampaignFields = new(StringComparer.Ordinal)
    {
        "River", "trainingTerrain", "Sites",
        "grids", "terrainNavigation", "temporaryNavigationOpen", "temporaryNavigationLinks",
        "temporaryNavigationStamps", "temporaryNavigationEpoch", "navigationCosts", "navigationPrevious",
        "navigationStamp", "navigationEpoch", "navigationHeap", "terrainCuts", "surveyGround", "wallCells", "wallTopology"
    };

    internal static void Save(Stream stream, Campaign world, object sequence)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var codec = new Codec(sequence.GetType(), writer, null);
        codec.Visit(typeof(Campaign), world);
        codec.Visit(sequence.GetType(), sequence);
    }

    internal static void Restore(Stream stream, Campaign world, object sequence)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var codec = new Codec(sequence.GetType(), null, reader);
        if (!ReferenceEquals(codec.Visit(typeof(Campaign), world), world) ||
            !ReferenceEquals(codec.Visit(sequence.GetType(), sequence), sequence) ||
            reader.BaseStream.ReadByte() != -1)
            throw new InvalidDataException("Invalid battle checkpoint.");
        world.ResetRestoredNavigation();
    }

    // Field types come from the game's model and the selected chapter. A save cannot nominate
    // a .NET type. IDs preserve links among units, terrain-independent state and the timeline.
    private sealed class Codec(Type timeline, BinaryWriter writer, BinaryReader reader)
    {
        private readonly Dictionary<object, int> written = new(ReferenceEqualityComparer.Instance);
        private readonly List<object> restored = new() { null };
        private readonly Dictionary<Type, FieldInfo[]> schemas = new();
        private int entries;

        private bool Saving => writer != null;

        private int Int(int value = 0) { if (!Saving) return reader.ReadInt32(); writer.Write(value); return value; }
        private bool Flag(bool value = false) { if (!Saving) return reader.ReadBoolean(); writer.Write(value); return value; }

        private string Text(string value)
        {
            if (!Flag(value != null)) return null;
            if (Saving) { writer.Write(value); return value; }
            int length = reader.Read7BitEncodedInt();
            if (length < 0 || length > 16_384) throw new InvalidDataException("Invalid checkpoint text.");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }

        private int Count(int value)
        {
            int count = Int(value);
            if (count < 0 || count > MaxEntries) throw new InvalidDataException("Invalid checkpoint count.");
            return count;
        }

        private void Reserve(int count)
        {
            entries = checked(entries + count);
            if (entries > MaxEntries) throw new InvalidDataException("Checkpoint is too large.");
        }

        internal object Visit(Type type, object value = null, int depth = 0)
        {
            if (depth >= MaxDepth) throw new InvalidDataException("Checkpoint is too deeply nested.");
            if (type == typeof(string)) return Text((string)value);
            if (type == typeof(int)) return Int(Saving ? (int)value : 0);
            if (type == typeof(bool)) return Flag(Saving && (bool)value);
            if (type == typeof(float))
            {
                if (!Saving) return reader.ReadSingle();
                writer.Write((float)value); return value;
            }
            if (type == typeof(double))
            {
                if (!Saving) return reader.ReadDouble();
                writer.Write((double)value); return value;
            }
            if (type.IsEnum) return Enum.ToObject(type, Int(Saving ? Convert.ToInt32(value) : 0));
            if (Nullable.GetUnderlyingType(type) is { } underlying)
                return Flag(value != null) ? Visit(underlying, value, depth + 1) : null;
            if (type == typeof(TypeInfo) && Flag(value is TypeInfo info && ReferenceEquals(info, Campaign.Info(info.Key))))
            {
                string key = Text((value as TypeInfo)?.Key);
                return Campaign.Info(key) ?? throw new InvalidDataException("Unknown unit type.");
            }

            if (!type.IsValueType)
            {
                if (Saving)
                {
                    if (value == null) { Int(0); return null; }
                    if (written.TryGetValue(value, out int id)) { Int(id); return value; }
                    if (value.GetType() != type) throw new InvalidDataException("Unsupported checkpoint object.");
                    written.Add(value, id = written.Count + 1);
                    Int(-id);
                }
                else
                {
                    int id = Int();
                    if (id == 0) return null;
                    if (id > 0 && id < restored.Count && type.IsInstanceOfType(restored[id])) return restored[id];
                    if (id != -restored.Count) throw new InvalidDataException("Invalid checkpoint reference.");
                }
                Reserve(1);
            }

            if (type.IsArray) return ArrayState(type, (Array)value, depth);
            if (type.IsGenericType && !type.IsValueType) return CollectionState(type, value, depth);
            if (type != timeline && type.Assembly != typeof(Campaign).Assembly &&
                type != typeof(System.Drawing.PointF) && type != typeof(System.Drawing.RectangleF) &&
                type != typeof(System.Drawing.Point) && type != typeof(ValueTuple<int, int>))
                throw new InvalidDataException($"Unsupported checkpoint field {type}.");

            value ??= RuntimeHelpers.GetUninitializedObject(type);
            Register(type, value);
            if (!schemas.TryGetValue(type, out var fields))
            {
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => type != typeof(Campaign) || !DerivedCampaignFields.Contains(field.Name))
                    .OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
                if (Count(fields.Length) != fields.Length)
                    throw new InvalidDataException("Checkpoint layout has changed.");
                foreach (var field in fields)
                {
                    string signature = field.Name + ":" + field.FieldType;
                    if (Text(signature) != signature)
                        throw new InvalidDataException("Checkpoint layout has changed.");
                }
                schemas.Add(type, fields);
            }
            foreach (var field in fields)
            {
                object item = Visit(field.FieldType, Saving ? field.GetValue(value) : null, depth + 1);
                if (!Saving) field.SetValue(value, item);
            }
            return value;
        }

        private void Register(Type type, object value)
        {
            if (!Saving && !type.IsValueType) restored.Add(value);
        }

        private Array ArrayState(Type type, Array array, int depth)
        {
            var element = type.GetElementType();
            var lengths = new int[type.GetArrayRank()];
            int total = 1;
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = Count(Saving ? array.GetLength(i) : 0);
                if (lengths[i] > 16_384 || (long)total * lengths[i] > MaxEntries)
                    throw new InvalidDataException("Invalid checkpoint array size.");
                total *= lengths[i];
            }
            Reserve(total);
            array ??= Array.CreateInstance(element, lengths);
            Register(type, array);
            if (element == typeof(bool))
            {
                if (Saving)
                {
                    var bytes = new byte[total];
                    Buffer.BlockCopy(array, 0, bytes, 0, total);
                    writer.Write(bytes);
                }
                else
                {
                    var bytes = reader.ReadBytes(total);
                    if (bytes.Length != total) throw new EndOfStreamException();
                    Buffer.BlockCopy(bytes, 0, array, 0, total);
                }
            }
            else if (Saving)
            {
                foreach (object item in array) Visit(element, item, depth + 1);
            }
            else
            {
                var index = new int[lengths.Length];
                for (int i = 0; i < total; i++)
                {
                    array.SetValue(Visit(element, depth: depth + 1), index);
                    for (int axis = index.Length - 1; axis >= 0 && ++index[axis] == lengths[axis]; axis--)
                        index[axis] = 0;
                }
            }
            return array;
        }

        private object CollectionState(Type type, object value, int depth)
        {
            var kind = type.GetGenericTypeDefinition();
            if (kind != typeof(List<>) && kind != typeof(HashSet<>) &&
                kind != typeof(Queue<>) && kind != typeof(Dictionary<,>))
                throw new InvalidDataException("Unsupported checkpoint collection.");
            var parts = type.GenericTypeArguments;
            int count = Count(Saving ? (int)type.GetProperty("Count").GetValue(value) : 0);
            Reserve(count);
            value ??= Activator.CreateInstance(type);
            Register(type, value);
            if (value is IDictionary dictionary)
            {
                if (Saving)
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        Visit(parts[0], entry.Key, depth + 1);
                        Visit(parts[1], entry.Value, depth + 1);
                    }
                else
                    for (int i = 0; i < count; i++)
                        dictionary.Add(Visit(parts[0], depth: depth + 1), Visit(parts[1], depth: depth + 1));
            }
            else if (Saving)
            {
                foreach (object item in (IEnumerable)value) Visit(parts[0], item, depth + 1);
            }
            else
            {
                var add = type.GetMethod(kind == typeof(Queue<>) ? "Enqueue" : "Add", parts);
                var argument = new object[1];
                for (int i = 0; i < count; i++)
                {
                    argument[0] = Visit(parts[0], depth: depth + 1);
                    add.Invoke(value, argument);
                }
            }
            return value;
        }
    }
}
