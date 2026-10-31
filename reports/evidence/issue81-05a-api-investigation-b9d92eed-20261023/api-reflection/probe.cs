using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;
var packages = "/home/donabe/.nuget/packages";
AssemblyLoadContext.Default.Resolving += (ctx, name) => {
    var file = name.Name + ".dll";
    foreach (var p in Directory.EnumerateFiles(packages, file, SearchOption.AllDirectories)) {
        try { return ctx.LoadFromAssemblyPath(p); } catch { }
    }
    return null;
};
var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath("/home/donabe/.nuget/packages/aspire.hosting/13.5.4/lib/net8.0/Aspire.Hosting.dll");
foreach (var tn in new[]{"Aspire.Hosting.ApplicationModel.ContainerResource","Aspire.Hosting.ResourceBuilderExtensions","Aspire.Hosting.ContainerResourceBuilderExtensions"}) {
  var t=asm.GetType(tn); Console.WriteLine("TYPE "+tn+" found="+(t!=null)); if(t==null) continue;
  if(tn.EndsWith("ContainerResource")) { Console.WriteLine("interfaces="+string.Join(",",t.GetInterfaces().Select(x=>x.FullName))); continue; }
  foreach(var m in t.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name.Contains("HttpEndpoint")||m.Name.Contains("EndpointCallback")||m.Name=="WithAnnotation")) {
    Console.WriteLine("METHOD "+m);
    foreach(var g in m.GetGenericArguments()) { Console.WriteLine("GENERIC "+g+" constraints="+string.Join(",",g.GetGenericParameterConstraints().Select(x=>x.FullName))+" attrs="+g.GenericParameterAttributes); }
  }
}
