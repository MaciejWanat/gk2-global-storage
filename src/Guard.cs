using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security;

namespace GK2GlobalStorage
{
    // Startup check that the mod's code can run on the installed game. The mod is built against one game version:
    // its methods name the game's classes, fields and methods, and the runtime looks those names up only when it
    // compiles a method, on its first call. On another game build (another store's version, an older one, or an
    // Assembly-CSharp.dll replaced by a mod) a name it lacks makes that compilation fail: the whole method throws
    // before its first line, so its own try/catch cannot help, and a patched game method then throws every time it
    // runs (that is how picking up items broke). Check compiles a hook's methods, and every method they call in the
    // same assembly, before the hook goes in (Patches.Install), so such a hook is left out instead.
    internal static class Guard
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly OpCode[] oneByte = new OpCode[0x100];
        private static readonly OpCode[] twoByte = new OpCode[0x100];

        // Per method: null when it compiled, else why not. Shared by all hooks, so a method compiles once.
        private static readonly Dictionary<MethodBase, string> compiled = new Dictionary<MethodBase, string>();
        private static readonly Dictionary<MethodBase, List<MethodBase>> calls = new Dictionary<MethodBase, List<MethodBase>>();

        // Methods compiled so far and the time it took (Stopwatch ticks), for the startup log.
        public static int Compiled => compiled.Count;

        public static long Ticks { get; private set; }

        public static bool WasChecked(MethodBase method) => compiled.ContainsKey(method);

        static Guard()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is OpCode op)
                {
                    ushort value = unchecked((ushort)op.Value);
                    if (op.Size == 1)
                    {
                        oneByte[value] = op;
                    }
                    else
                    {
                        twoByte[value & 0xFF] = op;
                    }
                }
            }
        }

        // Null when every method of these types (nested ones too: lambdas, iterators) and what they call compiles,
        // else the first that does not and why.
        public static string Check(params Type[] types)
        {
            List<MethodBase> roots = new List<MethodBase>();
            foreach (Type type in types)
            {
                AddDeclared(type, roots);
            }
            return Check(roots);
        }

        public static string Check(IEnumerable<MethodBase> roots)
        {
            long start = Stopwatch.GetTimestamp();
            try
            {
                HashSet<MethodBase> seen = new HashSet<MethodBase>();
                Queue<MethodBase> queue = new Queue<MethodBase>();
                foreach (MethodBase root in roots)
                {
                    Enqueue(root, seen, queue);
                }
                while (queue.Count > 0)
                {
                    MethodBase method = queue.Dequeue();
                    string error = Compile(method);
                    if (error != null)
                    {
                        return Describe(method) + ": " + error;
                    }
                    foreach (MethodBase callee in Callees(method))
                    {
                        Enqueue(callee, seen, queue);
                    }
                }
                return null;
            }
            finally
            {
                Ticks += Stopwatch.GetTimestamp() - start;
            }
        }

        private static void Enqueue(MethodBase method, HashSet<MethodBase> seen, Queue<MethodBase> queue)
        {
            if (method == null || method.IsAbstract || method.ContainsGenericParameters || !seen.Add(method))
            {
                return;
            }
            queue.Enqueue(method);
            Type type = method.DeclaringType;
            if (type == null)
            {
                return;
            }
            // The class's static constructor runs before any of its code.
            Enqueue(type.TypeInitializer, seen, queue);
            // Iterators: the caller only creates the state machine, the runtime later calls its MoveNext.
            if (method is ConstructorInfo && IsStateMachine(type))
            {
                List<MethodBase> body = new List<MethodBase>();
                AddDeclared(type, body);
                foreach (MethodBase m in body)
                {
                    Enqueue(m, seen, queue);
                }
            }
        }

        private static bool IsStateMachine(Type type)
        {
            return type.IsDefined(typeof(CompilerGeneratedAttribute), false)
                && (typeof(IEnumerator).IsAssignableFrom(type) || typeof(IAsyncStateMachine).IsAssignableFrom(type));
        }

        private static void AddDeclared(Type type, List<MethodBase> list)
        {
            list.AddRange(type.GetMethods(Declared));
            list.AddRange(type.GetConstructors(Declared));
            foreach (Type nested in type.GetNestedTypes(Declared))
            {
                AddDeclared(nested, list);
            }
        }

        // GetFunctionPointer compiles the method on Mono (the game), PrepareMethod on .NET (the tests).
        private static string Compile(MethodBase method)
        {
            if (compiled.TryGetValue(method, out string known))
            {
                return known;
            }
            string error = null;
            if ((method.MethodImplementationFlags & (MethodImplAttributes.InternalCall | MethodImplAttributes.Runtime)) == 0
                && (method.Attributes & MethodAttributes.PinvokeImpl) == 0)
            {
                try
                {
                    RuntimeMethodHandle handle = method.MethodHandle;
                    handle.GetFunctionPointer();
                    RuntimeHelpers.PrepareMethod(handle);
                }
                catch (Exception ex) when (CannotRun(ex))
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                }
                catch (Exception)
                {
                    // Anything else says nothing about the game version (e.g. a runtime that cannot compile ahead).
                }
            }
            compiled[method] = error;
            return error;
        }

        // What the runtime throws for code that names something the loaded assemblies do not have.
        private static bool CannotRun(Exception ex)
        {
            return ex is MissingMemberException || ex is TypeLoadException || ex is FileNotFoundException
                || ex is FileLoadException || ex is BadImageFormatException || ex is InvalidProgramException
                || ex is MemberAccessException || ex is VerificationException || ex is TypeInitializationException;
        }

        // Methods this one calls, creates or takes as a delegate (call, callvirt, newobj, ldftn, ...) that live in
        // its own assembly: the mod's code reaches the mod's code. The game's methods are checked by the compiler.
        private static List<MethodBase> Callees(MethodBase method)
        {
            if (calls.TryGetValue(method, out List<MethodBase> list))
            {
                return list;
            }
            list = new List<MethodBase>();
            calls[method] = list;
            byte[] il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (Exception)
            {
                return list;
            }
            if (il == null)
            {
                return list;
            }
            Type[] typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
            Module module = method.Module;
            try
            {
                int i = 0;
                while (i < il.Length)
                {
                    OpCode op = il[i] == 0xFE ? twoByte[il[i + 1]] : oneByte[il[i]];
                    if (op.Size == 0)
                    {
                        break;
                    }
                    i += op.Size;
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
                            i += 4 + 4 * BitConverter.ToInt32(il, i);
                            break;
                        case OperandType.InlineMethod:
                            MethodBase callee = Resolve(module, BitConverter.ToInt32(il, i), typeArgs, methodArgs);
                            if (callee != null && callee.Module == module)
                            {
                                list.Add(callee);
                            }
                            i += 4;
                            break;
                        default:
                            i += 4;
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable IL: keep what was found; compiling the method already checked its own references.
            }
            return list;
        }

        private static MethodBase Resolve(Module module, int token, Type[] typeArgs, Type[] methodArgs)
        {
            try
            {
                return module.ResolveMethod(token, typeArgs, methodArgs);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Describe(MethodBase method)
        {
            return (method.DeclaringType != null ? method.DeclaringType.FullName + "." : "") + method.Name;
        }
    }
}
