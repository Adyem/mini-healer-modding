using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using CecilModule = Mono.Cecil.ModuleDefinition;
using CecilFieldReference = Mono.Cecil.FieldReference;
using CecilMethodReference = Mono.Cecil.MethodReference;
using CecilTypeReference = Mono.Cecil.TypeReference;

var asmPath = @"C:\Program Files (x86)\Steam\steamapps\common\Mini Healer\MiniHealer_Data\Managed\Assembly-CSharp.dll";

if (Environment.GetEnvironmentVariable("DUMP_ATLAS_IL") == "1")
{
    using var module = CecilModule.ReadModule(asmPath);
    var requestedType = Environment.GetEnvironmentVariable("DUMP_TYPE") ?? "ItemAtlasUIManager";
    var requestedMethods = (Environment.GetEnvironmentVariable("DUMP_METHODS") ?? "refreshArtifactInfoView,refreshArtifactDropInfoView,getArtifactPurchaseMat,isArtifactPurchaseAble")
        .Split(',')
        .Select(item => item.Trim())
        .Where(item => item.Length > 0)
        .ToArray();
    var type = module.Types.SelectMany(FlattenTypes).First(t => t.Name == requestedType);
    foreach (var methodName in requestedMethods)
    {
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        Console.WriteLine($"[{methodName}]");
        if (method?.Body == null)
        {
            Console.WriteLine("  <no body>");
            continue;
        }

        foreach (var instruction in method.Body.Instructions)
        {
            Console.WriteLine($"  {instruction.Offset:X4}: {instruction.OpCode} {FormatOperand(instruction.Operand)}");
        }
    }

    return;
}

if (Environment.GetEnvironmentVariable("DUMP_ENUMS") == "1")
{
    using var module = CecilModule.ReadModule(asmPath);
    var allTypes = module.Types.SelectMany(FlattenTypes);
    foreach (var type in allTypes.Where(t => t.IsEnum && (Environment.GetEnvironmentVariable("DUMP_ALL_ENUMS") == "1" || t.Fields.Any(f => f.Name.Contains("HEALER") || f.Name.Contains("HP_FLAT") || f.Name.Contains("PHYSICAL_DAMAGE")))))
    {
        Console.WriteLine($"[{type.Name}]");
        foreach (var field in type.Fields.Where(f => f.IsStatic))
        {
            Console.WriteLine($"{field.Name}={(int)field.Constant}");
        }
    }

    return;
}

static IEnumerable<Mono.Cecil.TypeDefinition> FlattenTypes(Mono.Cecil.TypeDefinition type)
{
    yield return type;
    foreach (var nested in type.NestedTypes.SelectMany(FlattenTypes))
    {
        yield return nested;
    }
}

static string FormatOperand(object? operand)
{
    return operand switch
    {
        null => string.Empty,
        CecilFieldReference field => $"{field.DeclaringType.Name}::{field.Name}",
        CecilMethodReference method => $"{method.DeclaringType.Name}::{method.Name}",
        CecilTypeReference type => type.FullName,
        string value => "\"" + value + "\"",
        _ => operand.ToString() ?? string.Empty
    };
}

using var fs = File.OpenRead(asmPath);
using var peReader = new PEReader(fs);
var reader = peReader.GetMetadataReader();

string GetTypeName(TypeDefinitionHandle handle)
{
    var typeDef = reader.GetTypeDefinition(handle);
    return $"{reader.GetString(typeDef.Namespace)}.{reader.GetString(typeDef.Name)}".Trim('.');
}

string GetMethodSig(MethodDefinitionHandle handle)
{
    var method = reader.GetMethodDefinition(handle);
    var name = reader.GetString(method.Name);
    var blob = reader.GetBlobReader(method.Signature);
    var decoder = new SignatureDecoder<string, object?>(new TypeNameProvider(reader), reader, null);
    var sig = decoder.DecodeMethodSignature(ref blob);
    var parms = string.Join(", ", sig.ParameterTypes);
    return $"{sig.ReturnType} {name}({parms})";
}

var typeDefs = reader.TypeDefinitions
    .Select(h => reader.GetTypeDefinition(h))
    .Select((td, index) => new
    {
        Handle = reader.TypeDefinitions.ElementAt(index),
        Namespace = reader.GetString(td.Namespace),
        Name = reader.GetString(td.Name)
    })
    .ToList();

Console.WriteLine($"Assembly: {Path.GetFileName(asmPath)}");
Console.WriteLine($"Type count: {typeDefs.Count}");

if (Environment.GetEnvironmentVariable("FOCUS_ONLY") != "1")
{
var refs = reader.AssemblyReferences
    .Select(h => reader.GetAssemblyReference(h))
    .Select(r => $"{reader.GetString(r.Name)}, v{r.Version}")
    .ToList();

Console.WriteLine();
Console.WriteLine("Assembly references:");
foreach (var r in refs)
{
    Console.WriteLine($"  {r}");
}

var interesting = new[]
{
    "Save","Load","Item","Enemy","Skill","Spell","Quest","Inventory","Shop","Loot","Damage","Stat","Buff","Debuff",
    "Hero","Party","Upgrade","Config","Setting","UI","Menu","Audio","Input","Network","Steam","Achievement","Localization",
    "Chest","Dungeon","Combat","Boss","Card","Talent","Relic","Curse","Pause","Option","Dialog","Dialogue"
};

var matches = typeDefs
    .Where(t => interesting.Any(k =>
        t.Name.Contains(k, StringComparison.OrdinalIgnoreCase) ||
        t.Namespace.Contains(k, StringComparison.OrdinalIgnoreCase)))
    .ToList();

Console.WriteLine();
Console.WriteLine("Matching types:");
foreach (var t in matches.Take(300))
{
    var full = string.IsNullOrEmpty(t.Namespace) ? t.Name : $"{t.Namespace}.{t.Name}";
    Console.WriteLine($"  {full}");
}

Console.WriteLine();
Console.WriteLine("Selected methods:");
foreach (var t in matches.Take(60))
{
    var typeHandle = (TypeDefinitionHandle)t.Handle;
    var typeDef = reader.GetTypeDefinition(typeHandle);
    var methods = typeDef.GetMethods()
        .Select(h => GetMethodSig(h))
        .Where(sig => !sig.Contains(".ctor") && !sig.Contains(".cctor"))
        .Take(6)
        .ToList();
    if (methods.Count == 0) continue;

    var full = string.IsNullOrEmpty(t.Namespace) ? t.Name : $"{t.Namespace}.{t.Name}";
    Console.WriteLine($"[{full}]");
    foreach (var m in methods)
    {
        Console.WriteLine($"  {m}");
    }
}
}

string GetTypeNameFromSignature(BlobHandle signatureHandle)
{
    var blob = reader.GetBlobReader(signatureHandle);
    var sig = new SignatureDecoder<string, object?>(new TypeNameProvider(reader), reader, null).DecodeFieldSignature(ref blob);
    return sig;
}

string GetMethodSigFromHandle(MethodDefinition method)
{
    var blob = reader.GetBlobReader(method.Signature);
    var decoder = new SignatureDecoder<string, object?>(new TypeNameProvider(reader), reader, null);
    var sig = decoder.DecodeMethodSignature(ref blob);
    var parms = string.Join(", ", sig.ParameterTypes);
    return $"{sig.ReturnType} {reader.GetString(method.Name)}({parms})";
}

var focusTypes = new[]
{
    "SkillData",
    "SkillDataController",
    "Skill",
    "SkillDescription",
    "SkillSetUpItem",
    "SkillUpgradeSaveInfo",
    "SkillTreeSaveInfo",
    "SkillTreeLoadOutInfo",
    "TalentData",
    "TalentDataController",
    "Talent",
    "TalentMasteryAttribute",
    "TalentMasterySaveInfo",
    "TalentUIClassSelect",
    "TalentUINode",
    "TalentDataController",
    "TalentMasteryAttribute",
    "CostMultiItem",
    "DamageData",
    "LootTableManager",
    "GenericLootDropItem`1",
    "GenericLootDropTable`2",
    "RuneLootTable",
    "RuneLootDropItem",
    "ArtifactEleDamageLootTable",
    "ArtifactEleDamageLootDrop",
    "BossMatLootItemInfo",
    "LevelDescriptionLootItem",
    "IdleChestSeed",
    "IdleCurrencyChestSeed",
    "IdleExpChestSeed",
    "AireneTowerRewardsUIManager"
    ,"Artifact"
    ,"ArtifactsData"
    ,"ArtifactDataController"
    ,"ItemAtlasUIManager"
    ,"ArtifactItemUI"
    ,"ArtifactUI"
    ,"ArtifactDescription"
    ,"ArtifactData"
    ,"InitalStatsDataController"
    ,"ArtifactAttribute"
    ,"ArtifactSaveAttribute"
    ,"ArtifactAttributeDataController"
    ,"AttributeTierTable"
    ,"ArtifactAttributeMapRef"
    ,"Material"
    ,"MaterialData"
    ,"MaterialDataController"
    ,"StackableMaterial"
    ,"MaterialAcquiredData"
    ,"ArtifactSaveInfo"
    ,"ArtifactAttrUpgradeSaveInfo"
};

Console.WriteLine();
Console.WriteLine("Skill/Talent deep dive:");
foreach (var wanted in focusTypes.Distinct(StringComparer.OrdinalIgnoreCase))
{
    var hit = typeDefs.FirstOrDefault(t => string.Equals(t.Name, wanted, StringComparison.OrdinalIgnoreCase));
    if (hit is null)
        hit = typeDefs.FirstOrDefault(t => string.Equals($"{t.Namespace}.{t.Name}", wanted, StringComparison.OrdinalIgnoreCase));

    if (hit is null)
    {
        Console.WriteLine($"[{wanted}] not found");
        continue;
    }

    var full = string.IsNullOrEmpty(hit.Namespace) ? hit.Name : $"{hit.Namespace}.{hit.Name}";
    var td = reader.GetTypeDefinition((TypeDefinitionHandle)hit.Handle);

    Console.WriteLine($"[{full}]");

    var fields = td.GetFields()
        .Select(h =>
        {
            var f = reader.GetFieldDefinition(h);
            return $"{GetTypeNameFromSignature(f.Signature)} {reader.GetString(f.Name)}";
        })
        .Where(f => wanted is not "SkillDataController" || IsBalanceRelevantField(f))
        .ToList();

    if (fields.Count > 0)
    {
        Console.WriteLine("  Fields:");
        foreach (var f in fields)
        {
            Console.WriteLine($"    {f}");
        }
    }

    var methods = td.GetMethods()
        .Select(h => reader.GetMethodDefinition(h))
        .Select(m =>
        {
            var name = reader.GetString(m.Name);
            if (name.StartsWith(".ctor", StringComparison.Ordinal) || name.StartsWith(".cctor", StringComparison.Ordinal))
                return null;

            var sig = GetMethodSigFromHandle(m);
            return $"    {sig}";
        })
        .Where(x => x is not null)
        .Where(m => wanted != "SkillDataController" || IsBalanceRelevantMethod(m!))
        .Take(40)
        .Cast<string>()
        .ToList();

    if (methods.Count > 0)
    {
        Console.WriteLine("  Methods:");
        foreach (var m in methods)
        {
            Console.WriteLine(m);
        }
    }
}

bool IsBalanceRelevantField(string field)
{
    var needle = field.ToLowerInvariant();
    return needle.Contains("skill")
        || needle.Contains("talent")
        || needle.Contains("cooldown")
        || needle.Contains("cost")
        || needle.Contains("mana")
        || needle.Contains("heal")
        || needle.Contains("damage")
        || needle.Contains("dmg")
        || needle.Contains("crit")
        || needle.Contains("level")
        || needle.Contains("multi")
        || needle.Contains("chance")
        || needle.Contains("ratio")
        || needle.Contains("duration")
        || needle.Contains("effect")
        || needle.Contains("threshold")
        || needle.Contains("growth")
        || needle.Contains("reduce")
        || needle.Contains("reduction")
        || needle.Contains("increase")
        || needle.Contains("bonus")
        || needle.Contains("stack");
}

bool IsBalanceRelevantMethod(string method)
{
    var needle = method.ToLowerInvariant();
    return needle.Contains("load")
        || needle.Contains("find")
        || needle.Contains("get")
        || needle.Contains("learn")
        || needle.Contains("upgrade")
        || needle.Contains("mastery")
        || needle.Contains("description")
        || needle.Contains("proc")
        || needle.Contains("engage")
        || needle.Contains("battle");
}

sealed class TypeNameProvider : ISignatureTypeProvider<string, object?>
{
    private readonly MetadataReader _reader;
    public TypeNameProvider(MetadataReader reader) => _reader = reader;

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "bool",
        PrimitiveTypeCode.Byte => "byte",
        PrimitiveTypeCode.Char => "char",
        PrimitiveTypeCode.Double => "double",
        PrimitiveTypeCode.Int16 => "short",
        PrimitiveTypeCode.Int32 => "int",
        PrimitiveTypeCode.Int64 => "long",
        PrimitiveTypeCode.IntPtr => "nint",
        PrimitiveTypeCode.Object => "object",
        PrimitiveTypeCode.SByte => "sbyte",
        PrimitiveTypeCode.Single => "float",
        PrimitiveTypeCode.String => "string",
        PrimitiveTypeCode.UInt16 => "ushort",
        PrimitiveTypeCode.UInt32 => "uint",
        PrimitiveTypeCode.UInt64 => "ulong",
        PrimitiveTypeCode.UIntPtr => "nuint",
        PrimitiveTypeCode.Void => "void",
        _ => typeCode.ToString()
    };

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var td = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(td.Namespace);
        var name = reader.GetString(td.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var tr = reader.GetTypeReference(handle);
        var ns = reader.GetString(tr.Namespace);
        var name = reader.GetString(tr.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var decoder = new SignatureDecoder<string, object?>(this, reader, genericContext);
        var sigBlob = reader.GetBlobReader(reader.GetTypeSpecification(handle).Signature);
        var sig = decoder.DecodeType(ref sigBlob);
        return sig;
    }

    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetByReferenceType(string elementType) => $"ref {elementType}";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(", ", typeArguments)}>";
    public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
    public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', Math.Max(0, shape.Rank - 1))}]";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetTypeFromSerializedName(string name) => name;
    public string GetPrimitiveType(PrimitiveTypeCode typeCode, string? name) => GetPrimitiveType(typeCode);
    public string GetTypeFromSpecificationType(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => GetTypeFromSpecification(reader, genericContext, handle, rawTypeKind);
}
