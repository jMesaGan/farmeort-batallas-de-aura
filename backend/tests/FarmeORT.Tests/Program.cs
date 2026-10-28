using System.Reflection;
using Xunit;
var passed = 0; var skipped = 0; var failed = 0;
foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsPublic && t.IsClass))
    foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<FactAttribute>() is not null))
    {
        var fact = method.GetCustomAttribute<FactAttribute>()!;
        if (fact.Skip is not null) { Console.WriteLine($"SKIP {method.Name}: {fact.Skip}"); skipped++; continue; }
        var inputs = method.GetCustomAttributes<InlineDataAttribute>().SelectMany(a => a.GetData(method)).ToList();
        if (inputs.Count == 0) inputs.Add([]);
        foreach (var input in inputs) try
            {
                var result = method.Invoke(Activator.CreateInstance(type), input); if (result is Task task) await task;
                passed++; Console.WriteLine($"PASS {method.Name}");
            }
            catch (Exception e) { failed++; Console.WriteLine($"FAIL {method.Name}: {(e is TargetInvocationException ? e.InnerException : e)}"); }
    }
Console.WriteLine($"Passed: {passed}, skipped: {skipped}, failed: {failed}");
return failed == 0 ? 0 : 1;
