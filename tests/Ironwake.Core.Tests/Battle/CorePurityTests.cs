using System.Reflection;
using System.Reflection.Emit;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// DESIGN.md section 2: the rules engine references no Console, no System.Random, no
/// file IO, and no clock. Checked by walking the IL of every method in the Core assembly
/// and every signature, so a reference hidden in a lambda or a compiler-generated
/// type is found too; the referenced-assembly list is checked as well.
/// </summary>
public class CorePurityTests
{
    private static readonly Assembly Core = typeof(BattleState).Assembly;

    private static readonly string[] ForbiddenTypes =
    {
        "System.Console", "System.Random", "System.DateTime", "System.DateTimeOffset", "System.Environment",
        "System.Diagnostics.Stopwatch", "System.Threading.Thread", "System.Threading.Tasks.Task",
    };

    private static readonly string[] ForbiddenNamespaces = { "System.IO", "System.Security.Cryptography", "System.Net", "System.Threading" };

    [Fact]
    public void CoreReferencesNoConsoleRandomIoOrClock()
    {
        var offenders = Offenders(Core.GetTypes()).ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void CoreReferencesNoConsoleOrIoAssembly()
    {
        var names = Core.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(names, name => name == "System.Console" || name.StartsWith("System.IO", StringComparison.Ordinal));
    }

    [Fact]
    public void TheWalkFindsAConsoleCallAClockAndARandom()
    {
        var offenders = Offenders(new[] { typeof(Impure) }).ToList();

        Assert.Contains(offenders, o => o.Contains("System.Console"));
        Assert.Contains(offenders, o => o.Contains("System.DateTime"));
        Assert.Contains(offenders, o => o.Contains("System.Random"));
        Assert.Contains(offenders, o => o.Contains("System.IO.File"));
        Assert.Contains(offenders, o => o.Contains("Field") && o.Contains("System.Random"));
    }

    private static class Impure
    {
        public static readonly Random Dice = new();

        public static void Shout() => Console.WriteLine(DateTime.Now.ToString() + File.Exists("x"));
    }

    private static IEnumerable<string> Offenders(IEnumerable<Type> types)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in types)
        {
            foreach (var field in type.GetFields(all))
            {
                if (IsForbidden(field.FieldType))
                {
                    yield return $"{type.FullName}.{field.Name}: Field of {field.FieldType.FullName}";
                }
            }

            foreach (var property in type.GetProperties(all))
            {
                if (IsForbidden(property.PropertyType))
                {
                    yield return $"{type.FullName}.{property.Name}: property of {property.PropertyType.FullName}";
                }
            }

            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                foreach (var parameter in method.GetParameters())
                {
                    if (IsForbidden(parameter.ParameterType))
                    {
                        yield return $"{type.FullName}.{method.Name}: parameter of {parameter.ParameterType.FullName}";
                    }
                }

                foreach (var member in ReferencedMembers(method))
                {
                    var owner = member as Type ?? member.DeclaringType;
                    if (owner == typeof(Environment) && member.Name == "get_CurrentManagedThreadId")
                    {
                        // Compiler-generated iterators read the thread id to tell a fresh enumerator from a reused one. Not a clock, not IO.
                        continue;
                    }

                    if (owner is not null && IsForbidden(owner))
                    {
                        yield return $"{type.FullName}.{method.Name}: uses {owner.FullName}.{member.Name}";
                    }
                }
            }
        }
    }

    private static bool IsForbidden(Type type)
    {
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            return IsForbidden(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                if (IsForbidden(argument))
                {
                    return true;
                }
            }
        }

        var name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
        if (name is null)
        {
            return false;
        }

        if (ForbiddenTypes.Contains(name))
        {
            return true;
        }

        var ns = type.Namespace ?? "";
        return ForbiddenNamespaces.Any(f => ns == f || ns.StartsWith(f + ".", StringComparison.Ordinal));
    }

    private static readonly Dictionary<short, OpCode> OpCodeTable = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(op => op.Value);

    /// <summary>Every field, method, and type token a method's IL refers to.</summary>
    private static IEnumerable<MemberInfo> ReferencedMembers(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            yield break;
        }

        var il = body.GetILAsByteArray()!;
        var module = method.Module;
        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var i = 0;
        while (i < il.Length)
        {
            short value = il[i];
            i++;
            if (value == 0xFE)
            {
                value = (short)(0xFE00 | il[i]);
                i++;
            }

            var op = OpCodeTable[value];
            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(il, i);
                    i += 4 + 4 * count;
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, i);
                    i += 4;
                    yield return module.ResolveMember(token, typeArguments, methodArguments)!;
                    break;
                default:
                    i += 4;
                    break;
            }
        }
    }
}
